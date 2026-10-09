using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public enum AttendanceStatus
{
    Present = 0,
    Absent = 1,
    HalfDay = 2,
    Leave = 3 // Paid leave
}

public class StaffAttendance : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Calendar day (date only, stored as MySQL DATE).
    /// </summary>
    public DateTime Date { get; set; }

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    [MaxLength(300)]
    public string? Notes { get; set; }

    [MaxLength(200)]
    public string? MarkedBy { get; set; }
}
