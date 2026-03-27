using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace menu_backend.Models;

public class SocialMediaConnection : BaseEntity
{
    [Required]
    [MaxLength(20)]
    public string Platform { get; set; } = string.Empty; // facebook, instagram, google

    [Column(TypeName = "text")]
    public string? AccessToken { get; set; }

    [Column(TypeName = "text")]
    public string? RefreshToken { get; set; }

    [MaxLength(200)]
    public string? PageId { get; set; }

    [MaxLength(200)]
    public string? PageName { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool IsConnected { get; set; } = false;
}
