using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public class WaiterTableAssignment : BaseEntity
{
    [Required]
    public Guid WaiterId { get; set; }

    [Required]
    public Guid TableId { get; set; }

    // Navigation
    public User? Waiter { get; set; }
    public RestaurantTable? Table { get; set; }
}
