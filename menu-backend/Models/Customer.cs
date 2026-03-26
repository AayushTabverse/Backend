using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public class Customer : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string CustomerMobile { get; set; } = string.Empty;
}
