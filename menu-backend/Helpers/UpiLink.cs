using System.Globalization;

namespace menu_backend.Helpers;

/// <summary>
/// Builds UPI payment links (NPCI "upi://pay" format) that any UPI app can scan.
/// </summary>
public static class UpiLink
{
    /// <param name="upiId">Payee VPA, e.g. shop@okicici.</param>
    /// <param name="payeeName">Name shown in the payer's app.</param>
    /// <param name="amount">Rupees; omitted when null or ≤ 0 so the payer types it.</param>
    /// <param name="note">Shown to the payer and on the restaurant's statement (kept short).</param>
    public static string Build(string upiId, string payeeName, decimal? amount, string? note)
    {
        var parts = new List<string>
        {
            $"pa={upiId}",
            $"pn={Uri.EscapeDataString(Truncate(payeeName, 50))}"
        };
        if (amount is > 0)
            parts.Add($"am={Math.Round(amount.Value, 2).ToString("0.00", CultureInfo.InvariantCulture)}");
        parts.Add("cu=INR");
        if (!string.IsNullOrWhiteSpace(note))
            parts.Add($"tn={Uri.EscapeDataString(Truncate(note.Trim(), 50))}");

        return "upi://pay?" + string.Join("&", parts);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
