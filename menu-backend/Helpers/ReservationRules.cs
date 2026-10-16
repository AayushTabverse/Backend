namespace menu_backend.Helpers;

public static class ReservationRules
{
    /// <summary>
    /// A confirmed booking holds its tables this long before its time: the table shows as reserved,
    /// isn't offered to walk-ins, and the booking counts as a party ahead in waitlist estimates.
    /// </summary>
    public const int HoldMinutes = 60;
}
