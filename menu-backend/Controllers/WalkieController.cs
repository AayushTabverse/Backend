using System.Security.Claims;
using menu_backend.Data;
using menu_backend.DTOs;
using menu_backend.DTOs.Walkie;
using menu_backend.Helpers;
using menu_backend.Hubs;
using menu_backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Controllers;

/// <summary>
/// Staff walkie-talkie: push-to-talk voice clips and quick calls, to a channel or one person.
/// Messages are announced over SignalR ("WalkieMessage"); devices fetch the audio and play it.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.AllStaff)]
public class WalkieController : ControllerBase
{
    public static readonly string[] Channels = { "All", "Kitchen", "Floor", "Counter", "Hotel", "Direct" };
    private const int MaxAudioBytes = 400 * 1024;   // ~60 s of Opus; clips are capped at 15 s on the device
    private static readonly TimeSpan KeepFor = TimeSpan.FromHours(24);

    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;
    private readonly ITenantProvider _tenantProvider;

    public WalkieController(AppDbContext db, IHubContext<OrderHub> hub, ITenantProvider tenantProvider)
    {
        _db = db;
        _hub = hub;
        _tenantProvider = tenantProvider;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Send a voice clip (multipart: audio file + channel + optional recipient).</summary>
    [HttpPost("voice")]
    [RequestSizeLimit(MaxAudioBytes + 64 * 1024)]
    public async Task<IActionResult> SendVoice(IFormFile? audio, [FromForm] string channel, [FromForm] Guid? recipientUserId, [FromForm] int durationMs)
    {
        if (audio == null || audio.Length == 0) return BadRequest(ApiResponse.Fail("No recording received."));
        if (audio.Length > MaxAudioBytes) return BadRequest(ApiResponse.Fail("Recording is too long."));

        using var ms = new MemoryStream();
        await audio.CopyToAsync(ms);
        var mime = (audio.ContentType ?? "audio/webm").Split(';')[0];
        if (!mime.StartsWith("audio/") && mime != "video/webm" && mime != "video/mp4")
            return BadRequest(ApiResponse.Fail("Not an audio recording."));

        return await SaveAndAnnounceAsync(channel, recipientUserId, m =>
        {
            m.Audio = ms.ToArray();
            m.AudioMime = mime.Replace("video/", "audio/");
            m.DurationMs = Math.Clamp(durationMs, 0, 60_000);
        });
    }

    /// <summary>Send a quick call ("Table 5 food ready"); devices read it aloud in their own language.</summary>
    [HttpPost("quick")]
    public Task<IActionResult> SendQuick([FromBody] QuickCallRequest request) =>
        SaveAndAnnounceAsync(request.Channel, request.RecipientUserId, m =>
        {
            m.QuickKey = request.QuickKey.Trim();
            m.QuickTable = string.IsNullOrWhiteSpace(request.QuickTable) ? null : request.QuickTable.Trim();
        });

    /// <summary>Messages from the last 24 hours this person can hear (channels + their own direct ones).</summary>
    [HttpGet("recent")]
    public async Task<IActionResult> Recent()
    {
        var me = UserId;
        var since = DateTime.UtcNow - KeepFor;
        var rows = await _db.VoiceMessages
            .Where(m => m.CreatedAt >= since
                        && (m.Channel != "Direct" || m.RecipientUserId == me || m.SenderUserId == me))
            .OrderByDescending(m => m.CreatedAt)
            .Take(50)
            // Projected in SQL so the list doesn't load every clip's audio
            .Select(m => new WalkieMessageResponse
            {
                Id = m.Id, SenderUserId = m.SenderUserId, SenderName = m.SenderName, SenderRole = m.SenderRole,
                Channel = m.Channel, RecipientUserId = m.RecipientUserId, RecipientName = m.RecipientName,
                QuickKey = m.QuickKey, QuickTable = m.QuickTable, HasAudio = m.Audio != null,
                DurationMs = m.DurationMs, CreatedAt = m.CreatedAt
            })
            .ToListAsync();
        return Ok(ApiResponse<List<WalkieMessageResponse>>.Ok(rows));
    }

    [HttpGet("{id}/audio")]
    public async Task<IActionResult> Audio(Guid id)
    {
        var me = UserId;
        var m = await _db.VoiceMessages.FirstOrDefaultAsync(x => x.Id == id);
        if (m?.Audio == null) return NotFound();
        if (m.Channel == "Direct" && m.RecipientUserId != me && m.SenderUserId != me) return Forbid();
        return File(m.Audio, m.AudioMime ?? "audio/webm");
    }

    /// <summary>Staff who can log in, for "talk to one person".</summary>
    [HttpGet("contacts")]
    public async Task<IActionResult> Contacts()
    {
        var me = UserId;
        var people = await _db.Users
            .Where(u => u.IsActive && u.Role != UserRole.Helper && u.Id != me)
            .OrderBy(u => u.FullName)
            .Select(u => new WalkieContact { Id = u.Id, Name = u.FullName, Role = u.Role.ToString() })
            .ToListAsync();
        return Ok(ApiResponse<List<WalkieContact>>.Ok(people));
    }

    private async Task<IActionResult> SaveAndAnnounceAsync(string channel, Guid? recipientUserId, Action<VoiceMessage> fill)
    {
        if (!Channels.Contains(channel)) return BadRequest(ApiResponse.Fail("Unknown channel."));

        string? recipientName = null;
        if (channel == "Direct")
        {
            if (recipientUserId == null) return BadRequest(ApiResponse.Fail("Choose who to talk to."));
            recipientName = await _db.Users.Where(u => u.Id == recipientUserId && u.IsActive).Select(u => u.FullName).FirstOrDefaultAsync();
            if (recipientName == null) return BadRequest(ApiResponse.Fail("That staff member was not found."));
        }

        var message = new VoiceMessage
        {
            TenantId = _tenantProvider.TenantId!,
            SenderUserId = UserId,
            SenderName = User.FindFirstValue(ClaimTypes.Name) ?? "Staff",
            SenderRole = User.FindFirstValue(ClaimTypes.Role) ?? "",
            Channel = channel,
            RecipientUserId = channel == "Direct" ? recipientUserId : null,
            RecipientName = recipientName
        };
        fill(message);
        _db.VoiceMessages.Add(message);
        await _db.SaveChangesAsync();

        var payload = Map(message);
        if (channel == "Direct")
            await _hub.Clients.Groups($"user-{message.RecipientUserId}", $"user-{message.SenderUserId}").SendAsync("WalkieMessage", payload);
        else
            await _hub.Clients.Group(message.TenantId).SendAsync("WalkieMessage", payload);

        return Ok(ApiResponse<WalkieMessageResponse>.Ok(payload));
    }

    private static WalkieMessageResponse Map(VoiceMessage m) => new()
    {
        Id = m.Id,
        SenderUserId = m.SenderUserId,
        SenderName = m.SenderName,
        SenderRole = m.SenderRole,
        Channel = m.Channel,
        RecipientUserId = m.RecipientUserId,
        RecipientName = m.RecipientName,
        QuickKey = m.QuickKey,
        QuickTable = m.QuickTable,
        HasAudio = m.Audio != null,
        DurationMs = m.DurationMs,
        CreatedAt = m.CreatedAt
    };
}
