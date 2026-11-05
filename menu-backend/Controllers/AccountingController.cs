using System.Text;
using menu_backend.Data;
using menu_backend.DTOs;
using menu_backend.DTOs.Accounting;
using menu_backend.Helpers;
using menu_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

/// <summary>Accounting for the restaurant's CA: setup, purchases, summaries, CSV reports and Tally export.</summary>
[ApiController]
[Route("api/[controller]")]
// Roles are set per endpoint (a class-level role list would also apply to, and block, wider endpoints)
[Authorize]
public class AccountingController : ControllerBase
{
    private readonly AccountingService _accounting;
    private readonly ITenantProvider _tenantProvider;

    public AccountingController(AccountingService accounting, ITenantProvider tenantProvider)
    {
        _accounting = accounting;
        _tenantProvider = tenantProvider;
    }

    private string TenantId => _tenantProvider.TenantId!;

    [HttpGet("settings")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> GetSettings() =>
        Ok(ApiResponse<AccountingSettings>.Ok(await _accounting.GetSettingsAsync(TenantId)));

    [HttpPut("settings")]
    [Authorize(Roles = Roles.Owner)]
    public Task<IActionResult> SaveSettings([FromBody] AccountingSettings settings) =>
        Run(async () => ApiResponse<AccountingSettings>.Ok(await _accounting.SaveSettingsAsync(TenantId, settings), "Accounting settings saved."));

    /// <summary>Payment accounts for the bill screens (cash, UPI, card…).</summary>
    [HttpGet("payment-accounts")]
    [Authorize(Roles = Roles.AllStaff)]
    public async Task<IActionResult> PaymentAccounts() =>
        Ok(ApiResponse<List<PaymentAccount>>.Ok((await _accounting.GetSettingsAsync(TenantId)).PaymentAccounts));

    [HttpGet("summary")]
    [Authorize(Roles = Roles.Management)]
    public Task<IActionResult> Summary([FromQuery] DateOnly from, [FromQuery] DateOnly to) =>
        Run(async () => { Check(from, to); return ApiResponse<AccountingSummaryResponse>.Ok(await _accounting.GetSummaryAsync(TenantId, from, to)); });

    [HttpGet("tally/masters")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> TallyMasters([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        Check(from, to);
        return Xml(await _accounting.TallyMastersXmlAsync(TenantId, from, to), $"tally-ledgers-{from:yyyyMMdd}-{to:yyyyMMdd}.xml");
    }

    [HttpGet("tally/vouchers")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> TallyVouchers([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        Check(from, to);
        return Xml(await _accounting.TallyVouchersXmlAsync(TenantId, from, to), $"tally-vouchers-{from:yyyyMMdd}-{to:yyyyMMdd}.xml");
    }

    /// <summary>CSV for the CA: daily, gst, udhaar, purchases.</summary>
    [HttpGet("report/{report}")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> Report(string report, [FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        Check(from, to);
        try
        {
            var (name, csv) = await _accounting.ReportCsvAsync(TenantId, report, from, to);
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(), "text/csv", name);
        }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }

    // ── Purchase bills ──

    [HttpGet("purchases")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> Purchases([FromQuery] DateOnly from, [FromQuery] DateOnly to) =>
        Ok(ApiResponse<List<PurchaseBillResponse>>.Ok(await _accounting.GetPurchasesAsync(from, to)));

    [HttpPost("purchases")]
    [Authorize(Roles = Roles.Management)]
    public Task<IActionResult> AddPurchase([FromBody] PurchaseBillRequest request) =>
        Run(async () => ApiResponse<PurchaseBillResponse>.Ok(await _accounting.SavePurchaseAsync(TenantId, null, request), "Purchase bill saved."));

    [HttpPut("purchases/{id}")]
    [Authorize(Roles = Roles.Management)]
    public Task<IActionResult> UpdatePurchase(Guid id, [FromBody] PurchaseBillRequest request) =>
        Run(async () => ApiResponse<PurchaseBillResponse>.Ok(await _accounting.SavePurchaseAsync(TenantId, id, request), "Purchase bill updated."));

    [HttpPost("purchases/{id}/pay")]
    [Authorize(Roles = Roles.Management)]
    public Task<IActionResult> PayPurchase(Guid id, [FromBody] PayPurchaseRequest request) =>
        Run(async () => ApiResponse<PurchaseBillResponse>.Ok(await _accounting.PayPurchaseAsync(TenantId, id, request), "Marked as paid."));

    [HttpDelete("purchases/{id}")]
    [Authorize(Roles = Roles.Management)]
    public Task<IActionResult> DeletePurchase(Guid id) =>
        Run(async () => { await _accounting.DeletePurchaseAsync(id); return ApiResponse.Ok("Purchase bill deleted."); });

    private static void Check(DateOnly from, DateOnly to)
    {
        if (to < from) throw new ArgumentException("The end date is before the start date.");
        if (to.DayNumber - from.DayNumber > 366) throw new ArgumentException("Pick at most a year at a time.");
    }

    private FileContentResult Xml(string xml, string name) =>
        File(Encoding.UTF8.GetBytes(xml), "application/xml", name);

    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }
}
