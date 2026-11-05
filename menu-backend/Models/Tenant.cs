using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace menu_backend.Models;

public class Tenant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(36)]
    [Column("tenant_id")]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    [MaxLength(500)]
    public string? InstagramUrl { get; set; }

    [MaxLength(500)]
    public string? FacebookUrl { get; set; }

    [MaxLength(500)]
    public string? TwitterUrl { get; set; }

    [MaxLength(500)]
    public string? WebsiteUrl { get; set; }

    [MaxLength(500)]
    public string? GoogleMapsUrl { get; set; }

    [MaxLength(1000)]
    public string? UpiQrCodeUrl { get; set; }

    /// <summary>UPI ID (VPA, e.g. shop@okicici). When set, bills get a QR with the bill amount filled in.</summary>
    [MaxLength(100)]
    public string? UpiId { get; set; }

    /// <summary>Name shown in the customer's UPI app. Defaults to the restaurant name.</summary>
    [MaxLength(100)]
    public string? UpiPayeeName { get; set; }

    [MaxLength(20)]
    public string? PrinterWidth { get; set; } = "standard";

    public bool DirectPrint { get; set; } = false;

    public decimal CgstPercent { get; set; } = 2.5m;

    public decimal SgstPercent { get; set; } = 2.5m;

    public decimal ServiceChargePercent { get; set; } = 0m;

    public decimal MaxDiscountPercent { get; set; } = 0m;

    public bool SpinWheelEnabled { get; set; } = false;

    /// <summary>Let walk-ins add themselves to the waitlist by scanning the entrance QR.</summary>
    public bool WaitlistSelfCheckInEnabled { get; set; } = false;

    /// <summary>Called parties who don't arrive within this many minutes become no-shows. 0 = off.</summary>
    public int WaitlistNoShowMinutes { get; set; } = 15;

    /// <summary>Show waiting customers a live board of table numbers and their status (no names).</summary>
    public bool WaitlistShowTableStatus { get; set; } = false;

    /// <summary>Accept table reservation requests from the restaurant website.</summary>
    public bool OnlineReservationsEnabled { get; set; } = false;

    /// <summary>Confirmed reservations not seated this many minutes after their time become no-shows. 0 = off.</summary>
    public int ReservationGraceMinutes { get; set; } = 30;


    /// <summary>Print a KOT automatically at the counter Print Station for every new order.</summary>
    public bool AutoPrintKot { get; set; } = false;

    /// <summary>When KOTs auto-print: OnOrder (as soon as it is placed) or OnAccept (when the kitchen accepts it).</summary>
    [MaxLength(20)]
    public string KotPrintTrigger { get; set; } = "OnOrder";

    /// <summary>Copies of each KOT (e.g. 2: kitchen + counter).</summary>
    public int KotCopies { get; set; } = 1;

    /// <summary>Customers can order takeaway by scanning the counter QR.</summary>
    public bool TakeawayEnabled { get; set; } = false;

    /// <summary>Flat packing fee added once to every takeaway order (on top of per-dish packing charges).</summary>
    public decimal TakeawayPackingFee { get; set; } = 0m;

    /// <summary>Table/takeaway QRs only take orders during opening hours (the website's operating hours). The menu can still be browsed.</summary>
    public bool QrOrderingHoursEnabled { get; set; } = false;

    /// <summary>Stop taking QR orders this many minutes before closing.</summary>
    public int LastOrderMinutes { get; set; } = 0;

    /// <summary>Restaurant location for the QR ordering distance check.</summary>
    public double? Latitude { get; set; }

    /// <summary>Restaurant location for the QR ordering distance check.</summary>
    public double? Longitude { get; set; }

    /// <summary>QR orders are only accepted from phones within this distance of the restaurant. 0 = off.</summary>
    public int GeofenceRadiusMeters { get; set; } = 0;

    /// <summary>Restaurant's GSTIN, printed on tax invoices and used in accounting exports.</summary>
    [MaxLength(15)]
    public string? Gstin { get; set; }

    /// <summary>Accounting setup as JSON: payment accounts (cash, UPI/bank, card) and Tally ledger names.</summary>
    public string? AccountingJson { get; set; }

    /// <summary>Staff opened QR ordering outside hours ("open now") until this time (UTC).</summary>
    public DateTime? OrderingOpenUntil { get; set; }

    /// <summary>Hotel features: rooms, check-in/out, room service and room bills.</summary>
    public bool HotelModeEnabled { get; set; } = false;

    /// <summary>Customers earn loyalty points on paid bills (by mobile number) and redeem them at the counter.</summary>
    public bool LoyaltyEnabled { get; set; } = false;

    /// <summary>Points earned per ₹100 paid (before tax).</summary>
    public decimal LoyaltyPointsPer100 { get; set; } = 5m;

    /// <summary>Rupee value of one point when redeemed.</summary>
    public decimal LoyaltyPointValue { get; set; } = 1m;

    /// <summary>Points a customer needs before they can redeem.</summary>
    public int LoyaltyMinRedeemPoints { get; set; } = 100;

    /// <summary>Most of a bill (before tax, %) that points can pay.</summary>
    public decimal LoyaltyMaxRedeemPercent { get; set; } = 20m;

    /// <summary>Points expire after this many months without a visit. 0 = never.</summary>
    public int LoyaltyExpiryMonths { get; set; } = 12;

    /// <summary>Bonus points on a customer's first bill. 0 = off.</summary>
    public int LoyaltyWelcomeBonus { get; set; } = 0;

    /// <summary>Bonus points on a bill during the customer's birthday week. 0 = off.</summary>
    public int LoyaltyBirthdayBonus { get; set; } = 0;

    /// <summary>Most points one mobile number can earn per day (stops misuse). 0 = no limit.</summary>
    public int LoyaltyDailyEarnCap { get; set; } = 0;

    [MaxLength(100)]
    public string? Subdomain { get; set; }

    /// <summary>
    /// Outcome of the last DNS record attempt for <see cref="Subdomain"/>:
    /// "created", "failed" or "not_configured" (no DNS API token on the server). Null = never attempted.
    /// </summary>
    [MaxLength(20)]
    public string? SubdomainDnsStatus { get; set; }

    /// <summary>Owner-safe reason when the DNS step failed.</summary>
    [MaxLength(500)]
    public string? SubdomainDnsError { get; set; }

    public DateTime? SubdomainDnsUpdatedAt { get; set; }

    // ── Bring-your-own domain (e.g. www.spicegarden.in) ──

    /// <summary>Restaurant's own domain, lowercase ASCII (punycode for non-Latin names).</summary>
    [MaxLength(253)]
    public string? CustomDomain { get; set; }

    /// <summary>Random token the owner publishes as a TXT record to prove they control the domain.</summary>
    [MaxLength(64)]
    public string? CustomDomainToken { get; set; }

    public DateTime? CustomDomainAddedAt { get; set; }

    /// <summary>When the ownership TXT record was first seen. Only verified domains route to the site.</summary>
    public DateTime? CustomDomainVerifiedAt { get; set; }

    /// <summary>When the domain first served the website (DNS + hosting + HTTPS all working).</summary>
    public DateTime? CustomDomainActiveAt { get; set; }

    [MaxLength(50)]
    public string? CurrencyCode { get; set; } = "INR";

    [MaxLength(50)]
    public string? TimeZone { get; set; } = "Asia/Kolkata";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<RestaurantTable> Tables { get; set; } = new List<RestaurantTable>();
    public ICollection<MenuCategory> MenuCategories { get; set; } = new List<MenuCategory>();
}
