using System.Globalization;
using System.Security.Claims;
using System.Security;
using System.Text;
using System.Text.Json;
using menu_backend.Data;
using menu_backend.DTOs.Accounting;
using menu_backend.Helpers;
using menu_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

/// <summary>
/// Accounting for the restaurant's CA: payment accounts and ledger names, money received per account,
/// purchase bills, daily summaries and a Tally XML export (ledgers once, then daily vouchers).
/// </summary>
public class AccountingService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AccountingService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    private string? StaffName => _http.HttpContext?.User.FindFirstValue(ClaimTypes.Name);

    // ── Settings ──

    public static AccountingSettings ReadSettings(Tenant tenant)
    {
        AccountingSettings? settings = null;
        try { if (!string.IsNullOrWhiteSpace(tenant.AccountingJson)) settings = JsonSerializer.Deserialize<AccountingSettings>(tenant.AccountingJson, Json); }
        catch (JsonException) { /* fall back to defaults */ }
        settings ??= new AccountingSettings();
        settings.Ledgers ??= new AccountingLedgers();
        if (settings.PaymentAccounts.Count == 0)
            settings.PaymentAccounts = new()
            {
                new() { Key = "cash", Label = "Cash", Kind = "Cash", Ledger = "Cash" },
                new() { Key = "upi", Label = "UPI", Kind = "Upi", Ledger = "Bank (UPI)" },
                new() { Key = "card", Label = "Card", Kind = "Card", Ledger = "Bank (Card)" }
            };
        settings.Gstin = tenant.Gstin;
        return settings;
    }

    public async Task<AccountingSettings> GetSettingsAsync(string tenantId) =>
        ReadSettings(await _db.Tenants.FirstAsync(t => t.TenantId == tenantId));

    public async Task<AccountingSettings> SaveSettingsAsync(string tenantId, AccountingSettings settings)
    {
        var accounts = settings.PaymentAccounts.Where(a => !string.IsNullOrWhiteSpace(a.Key)).ToList();
        if (accounts.Count == 0) throw new ArgumentException("Keep at least one payment account.");
        if (accounts.Select(a => a.Key.Trim().ToLowerInvariant()).Distinct().Count() != accounts.Count)
            throw new ArgumentException("Two payment accounts have the same key.");
        if (!accounts.Any(a => a.Kind == "Cash"))
            throw new ArgumentException("Keep a cash account (cash bills and refunds go there).");
        foreach (var a in accounts)
        {
            a.Key = a.Key.Trim().ToLowerInvariant();
            a.Label = a.Label.Trim();
            a.Ledger = a.Ledger.Trim();
            if (a.Kind is not ("Cash" or "Upi" or "Card" or "Bank"))
                throw new ArgumentException($"Unknown account type for {a.Label}.");
        }
        settings.PaymentAccounts = accounts;

        var tenant = await _db.Tenants.FirstAsync(t => t.TenantId == tenantId);
        tenant.Gstin = string.IsNullOrWhiteSpace(settings.Gstin) ? null : settings.Gstin.Trim().ToUpperInvariant();
        settings.Gstin = null; // stored in its own column
        tenant.AccountingJson = JsonSerializer.Serialize(settings, Json);
        await _db.SaveChangesAsync();
        return ReadSettings(tenant);
    }

    private static string CashKey(AccountingSettings s) => s.PaymentAccounts.First(a => a.Kind == "Cash").Key;

    // ── Money received ──

    /// <summary>
    /// Records how a settled bill was paid. With no split given, the whole paid amount goes to cash.
    /// Returns the amount paid now (sum of the split) for the bill/udhaar calculation.
    /// </summary>
    public decimal RecordBillPayments(Tenant tenant, string billNumber, List<PaymentSplit>? splits, decimal paidAmount)
    {
        var settings = ReadSettings(tenant);
        var rows = (splits ?? new()).Where(p => p.Amount > 0).ToList();
        if (rows.Count == 0)
            rows.Add(new PaymentSplit { AccountKey = CashKey(settings), Amount = paidAmount });

        foreach (var p in rows)
        {
            if (!settings.PaymentAccounts.Any(a => a.Key == p.AccountKey))
                throw new InvalidOperationException($"Unknown payment account '{p.AccountKey}'.");
            _db.BillPayments.Add(new BillPayment
            {
                TenantId = tenant.TenantId,
                BillNumber = billNumber,
                AccountKey = p.AccountKey,
                Amount = Math.Round(p.Amount, 2),
                Source = MoneyInSource.Bill,
                RecordedBy = StaffName
            });
        }
        return rows.Sum(p => Math.Round(p.Amount, 2));
    }

    public async Task RecordUdhaarPaymentAsync(CustomerDue due, decimal amount, string? accountKey)
    {
        var tenant = await _db.Tenants.FirstAsync(t => t.TenantId == due.TenantId);
        var settings = ReadSettings(tenant);
        var key = string.IsNullOrWhiteSpace(accountKey) ? CashKey(settings) : accountKey;
        if (!settings.PaymentAccounts.Any(a => a.Key == key))
            throw new InvalidOperationException($"Unknown payment account '{key}'.");
        _db.BillPayments.Add(new BillPayment
        {
            TenantId = due.TenantId, BillNumber = due.BillNumber, AccountKey = key, Amount = amount,
            Source = MoneyInSource.Udhaar, CustomerDueId = due.Id, RecordedBy = StaffName
        });
    }

    /// <summary>Money handed back after a bill was reduced (from the cash drawer).</summary>
    public async Task RecordRefundAsync(string tenantId, string billNumber, decimal refund)
    {
        if (refund <= 0) return;
        var tenant = await _db.Tenants.FirstAsync(t => t.TenantId == tenantId);
        _db.BillPayments.Add(new BillPayment
        {
            TenantId = tenantId, BillNumber = billNumber, AccountKey = CashKey(ReadSettings(tenant)),
            Amount = -refund, Source = MoneyInSource.Refund, RecordedBy = StaffName
        });
    }

    // ── Purchases ──

    public async Task<List<PurchaseBillResponse>> GetPurchasesAsync(DateOnly from, DateOnly to)
    {
        var (start, end) = (from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MinValue).AddDays(1));
        var rows = await _db.PurchaseBills.Where(p => p.InvoiceDate >= start && p.InvoiceDate < end)
            .OrderByDescending(p => p.InvoiceDate).ThenBy(p => p.SupplierName).ToListAsync();
        return rows.Select(MapPurchase).ToList();
    }

    public async Task<PurchaseBillResponse> SavePurchaseAsync(string tenantId, Guid? id, PurchaseBillRequest r)
    {
        var tenant = await _db.Tenants.FirstAsync(t => t.TenantId == tenantId);
        var settings = ReadSettings(tenant);
        if (!string.IsNullOrWhiteSpace(r.PaidAccountKey) && !settings.PaymentAccounts.Any(a => a.Key == r.PaidAccountKey))
            throw new InvalidOperationException($"Unknown payment account '{r.PaidAccountKey}'.");

        var supplier = r.SupplierName.Trim();
        var invoice = r.InvoiceNumber.Trim();
        var duplicate = await _db.PurchaseBills.AnyAsync(p => p.Id != (id ?? Guid.Empty)
            && p.SupplierName == supplier && p.InvoiceNumber == invoice);
        if (duplicate)
            throw new InvalidOperationException($"{supplier} invoice {invoice} is already entered.");

        PurchaseBill bill;
        if (id is Guid existingId)
        {
            bill = await _db.PurchaseBills.FirstOrDefaultAsync(p => p.Id == existingId)
                ?? throw new KeyNotFoundException("Purchase bill not found.");
        }
        else
        {
            bill = new PurchaseBill { TenantId = tenantId, RecordedBy = StaffName };
            _db.PurchaseBills.Add(bill);
        }

        bill.SupplierName = r.SupplierName.Trim();
        bill.SupplierGstin = string.IsNullOrWhiteSpace(r.SupplierGstin) ? null : r.SupplierGstin.Trim().ToUpperInvariant();
        bill.InvoiceNumber = r.InvoiceNumber.Trim();
        bill.InvoiceDate = r.InvoiceDate.ToDateTime(TimeOnly.MinValue);
        bill.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        bill.ExpenseLedger = string.IsNullOrWhiteSpace(r.ExpenseLedger) ? null : r.ExpenseLedger.Trim();
        bill.TaxableAmount = Math.Round(r.TaxableAmount, 2);
        bill.Cgst = Math.Round(r.Cgst, 2);
        bill.Sgst = Math.Round(r.Sgst, 2);
        bill.Igst = Math.Round(r.Igst, 2);
        bill.TotalAmount = bill.TaxableAmount + bill.Cgst + bill.Sgst + bill.Igst;
        if (!string.IsNullOrWhiteSpace(r.PaidAccountKey))
        {
            bill.PaidAccountKey = r.PaidAccountKey;
            bill.PaidAt ??= bill.InvoiceDate;
        }
        else if (id == null)
        {
            bill.PaidAccountKey = null;
            bill.PaidAt = null;
        }
        await _db.SaveChangesAsync();
        return MapPurchase(bill);
    }

    public async Task<PurchaseBillResponse> PayPurchaseAsync(string tenantId, Guid id, PayPurchaseRequest r)
    {
        var tenant = await _db.Tenants.FirstAsync(t => t.TenantId == tenantId);
        if (!ReadSettings(tenant).PaymentAccounts.Any(a => a.Key == r.AccountKey))
            throw new InvalidOperationException($"Unknown payment account '{r.AccountKey}'.");
        var bill = await _db.PurchaseBills.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new KeyNotFoundException("Purchase bill not found.");
        bill.PaidAccountKey = r.AccountKey;
        bill.PaidAt = (r.PaidOn ?? BusinessClock.Today).ToDateTime(TimeOnly.MinValue);
        await _db.SaveChangesAsync();
        return MapPurchase(bill);
    }

    public async Task DeletePurchaseAsync(Guid id)
    {
        var bill = await _db.PurchaseBills.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new KeyNotFoundException("Purchase bill not found.");
        bill.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    private static PurchaseBillResponse MapPurchase(PurchaseBill p) => new()
    {
        Id = p.Id, SupplierName = p.SupplierName, SupplierGstin = p.SupplierGstin, InvoiceNumber = p.InvoiceNumber,
        InvoiceDate = DateOnly.FromDateTime(p.InvoiceDate), Description = p.Description, ExpenseLedger = p.ExpenseLedger,
        TaxableAmount = p.TaxableAmount, Cgst = p.Cgst, Sgst = p.Sgst, Igst = p.Igst, TotalAmount = p.TotalAmount,
        PaidAccountKey = p.PaidAccountKey, PaidAt = p.PaidAt
    };

    // ── Books: one day at a time ──

    /// <summary>One ledger line of a voucher. Debit positive, credit negative.</summary>
    private record Line(string Ledger, decimal Amount);

    private record Voucher(string RemoteId, string Type, DateOnly Date, string Number, string Narration, List<Line> Lines);

    private record Books(List<AccountingDay> Days, List<Voucher> Vouchers, Dictionary<string, string> Ledgers, List<string> Warnings,
        Dictionary<string, string> Gstins);

    /// <summary>Builds each day's figures and Tally vouchers from bills, payments, salaries and purchases.</summary>
    private async Task<Books> BuildBooksAsync(string tenantId, DateOnly from, DateOnly to)
    {
        var tenant = await _db.Tenants.FirstAsync(t => t.TenantId == tenantId);
        var settings = ReadSettings(tenant);
        var L = settings.Ledgers;
        var accounts = settings.PaymentAccounts.ToDictionary(a => a.Key);
        var cash = accounts[CashKey(settings)];
        string AccountLedger(string key) => accounts.TryGetValue(key, out var a) ? a.Ledger : cash.Ledger;
        string AccountLabel(string key) => accounts.TryGetValue(key, out var a) ? a.Label : key;

        var (startUtc, endUtc) = (BusinessClock.StartOfDayUtc(from), BusinessClock.StartOfDayUtc(to.AddDays(1)));

        var orders = await _db.Orders
            .Where(o => o.BillNumber != null && o.Status != OrderStatus.Cancelled
                        && o.CompletedAt >= startUtc && o.CompletedAt < endUtc)
            .Select(o => new { o.BillNumber, o.CompletedAt, o.SubTotal, o.PackagingCharge, o.LoyaltyDiscount, o.Tax, o.DiscountAmount, o.TotalAmount })
            .ToListAsync();
        var billNumbers = orders.Select(o => o.BillNumber!).Distinct().ToHashSet();

        // Payments for these bills (any date: a later refund belongs to its bill), and udhaar received in range
        var allPayments = await _db.BillPayments.ToListAsync();
        var billPayments = allPayments.Where(p => p.BillNumber != null && billNumbers.Contains(p.BillNumber) && p.Source != MoneyInSource.Udhaar).ToList();
        var udhaarPayments = allPayments.Where(p => p.Source == MoneyInSource.Udhaar && p.PaidAt >= startUtc && p.PaidAt < endUtc).ToList();
        var dues = await _db.CustomerDues.Where(d => d.BillNumber != null).ToListAsync();
        var dueByBill = dues.GroupBy(d => d.BillNumber!).ToDictionary(g => g.Key, g => g.First());
        var dueById = dues.ToDictionary(d => d.Id);

        var staffPayments = await _db.StaffPayments
            .Where(p => p.PaidOn >= from.ToDateTime(TimeOnly.MinValue) && p.PaidOn < to.ToDateTime(TimeOnly.MinValue).AddDays(1))
            .ToListAsync();
        var staffNames = await _db.Users.IgnoreQueryFilters().Where(u => u.TenantId == tenantId).ToDictionaryAsync(u => u.Id, u => u.FullName);
        var purchases = await _db.PurchaseBills.ToListAsync();

        var ledgers = new Dictionary<string, string>(); // name → Tally group
        var gstins = new Dictionary<string, string>();  // supplier ledger → GSTIN
        void Use(string name, string group) => ledgers.TryAdd(name, group);
        string Debtor(CustomerDue d)
        {
            var name = string.IsNullOrWhiteSpace(d.CustomerMobile) ? d.CustomerName : $"{d.CustomerName} ({d.CustomerMobile})";
            Use(name, "Sundry Debtors");
            return name;
        }
        string Supplier(PurchaseBill p)
        {
            Use(p.SupplierName, "Sundry Creditors");
            if (p.SupplierGstin != null) gstins.TryAdd(p.SupplierName, p.SupplierGstin);
            return p.SupplierName;
        }
        string Account(string key)
        {
            var name = AccountLedger(key);
            Use(name, accounts.TryGetValue(key, out var a) && a.Kind == "Cash" ? "Cash-in-Hand" : "Bank Accounts");
            return name;
        }

        var taxShare = tenant.CgstPercent + tenant.SgstPercent + tenant.ServiceChargePercent;
        var days = new List<AccountingDay>();
        var vouchers = new List<Voucher>();
        var warnings = new List<string>();

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var (ds, de) = BusinessClock.DayRangeUtc(day);
            var dayOrders = orders.Where(o => o.CompletedAt >= ds && o.CompletedAt < de).ToList();
            var dayBills = dayOrders.Select(o => o.BillNumber!).Distinct().ToList();
            var d = new AccountingDay { Date = day, Bills = dayBills.Count };

            // ── Sales voucher (all bills settled this day) ──
            if (dayBills.Count > 0)
            {
                d.Sales = dayOrders.Sum(o => o.SubTotal);
                d.Packing = dayOrders.Sum(o => o.PackagingCharge);
                d.LoyaltyDiscount = dayOrders.Sum(o => o.LoyaltyDiscount);
                d.Discounts = dayOrders.Sum(o => o.DiscountAmount);
                d.BillTotal = dayOrders.Sum(o => o.TotalAmount);
                var tax = dayOrders.Sum(o => o.Tax);
                d.Cgst = taxShare > 0 ? Math.Round(tax * tenant.CgstPercent / taxShare, 2) : 0;
                d.Sgst = taxShare > 0 ? Math.Round(tax * tenant.SgstPercent / taxShare, 2) : 0;
                d.ServiceCharge = tax - d.Cgst - d.Sgst;

                var lines = new List<Line>
                {
                    new(L.Sales, -d.Sales), new(L.Packing, -d.Packing),
                    new(L.Cgst, -d.Cgst), new(L.Sgst, -d.Sgst), new(L.ServiceCharge, -d.ServiceCharge),
                    new(L.LoyaltyDiscount, d.LoyaltyDiscount), new(L.Discount, d.Discounts)
                };
                Use(L.Sales, "Sales Accounts"); Use(L.Packing, "Sales Accounts");
                Use(L.Cgst, "Duties & Taxes"); Use(L.Sgst, "Duties & Taxes"); Use(L.ServiceCharge, "Indirect Incomes");
                Use(L.LoyaltyDiscount, "Indirect Expenses"); Use(L.Discount, "Indirect Expenses");

                foreach (var bill in dayBills)
                {
                    var billTotal = dayOrders.Where(o => o.BillNumber == bill).Sum(o => o.TotalAmount);
                    var paid = billPayments.Where(p => p.BillNumber == bill).ToList();
                    dueByBill.TryGetValue(bill, out var due);
                    if (paid.Count == 0)
                    {
                        // Bills from before payment modes were recorded: what wasn't udhaar was cash
                        var owed = due == null ? 0 : due.BillAmount - (due.PaidAmount - udhaarPaidSoFar(due));
                        paid.Add(new BillPayment { AccountKey = cash.Key, Amount = billTotal - Math.Max(0, owed), Source = MoneyInSource.Bill });
                    }
                    foreach (var p in paid)
                    {
                        lines.Add(new(Account(p.AccountKey), p.Amount));
                        if (p.Source == MoneyInSource.Refund) d.Refunds += -p.Amount;
                        else d.Received[AccountLabel(p.AccountKey)] = d.Received.GetValueOrDefault(AccountLabel(p.AccountKey)) + p.Amount;
                    }
                    var unpaid = Math.Round(billTotal - paid.Sum(p => p.Amount), 2);
                    if (unpaid > 0.01m && due != null)
                    {
                        lines.Add(new(Debtor(due), unpaid));
                        d.UdhaarGiven += unpaid;
                    }
                }

                d.RoundOff = Math.Round(-lines.Sum(l => l.Amount), 2);
                if (d.RoundOff != 0)
                {
                    lines.Add(new(L.RoundOff, d.RoundOff));
                    Use(L.RoundOff, "Indirect Expenses");
                    if (Math.Abs(d.RoundOff) > 1)
                        warnings.Add($"{day:dd MMM yyyy}: sales don't match payments by ₹{d.RoundOff:0.00} (booked to {L.RoundOff}). Check bills edited after printing.");
                }
                vouchers.Add(new Voucher($"sales-{day:yyyyMMdd}", "Sales", day, $"TV-S-{day:yyyyMMdd}",
                    $"Tabverse sales {day:dd-MM-yyyy}: {dayBills.Count} bills ({dayBills.Min()} to {dayBills.Max()})", Clean(lines)));
            }

            // ── Receipt voucher: udhaar paid this day ──
            var received = udhaarPayments.Where(p => p.PaidAt >= ds && p.PaidAt < de).ToList();
            if (received.Count > 0)
            {
                var lines = new List<Line>();
                foreach (var p in received)
                {
                    lines.Add(new(Account(p.AccountKey), p.Amount));
                    var due = p.CustomerDueId is Guid id && dueById.TryGetValue(id, out var x) ? x : null;
                    lines.Add(new(due != null ? Debtor(due) : Debtor(new CustomerDue { CustomerName = "Udhaar customers" }), -p.Amount));
                    d.UdhaarReceived += p.Amount;
                }
                vouchers.Add(new Voucher($"receipt-{day:yyyyMMdd}", "Receipt", day, $"TV-R-{day:yyyyMMdd}", $"Tabverse udhaar received {day:dd-MM-yyyy}", Clean(lines)));
            }

            // ── Payment voucher: salaries paid this day ──
            var salaries = staffPayments.Where(p => DateOnly.FromDateTime(p.PaidOn) == day).ToList();
            if (salaries.Count > 0)
            {
                var lines = new List<Line>();
                foreach (var p in salaries)
                {
                    var key = p.Mode == StaffPaymentMode.Cash ? cash.Key : settings.SalaryBankAccountKey ?? cash.Key;
                    lines.Add(new(L.Salary, p.Amount));
                    lines.Add(new(Account(key), -p.Amount));
                    d.Salaries += p.Amount;
                }
                Use(L.Salary, "Indirect Expenses");
                var names = string.Join(", ", salaries.Select(p => staffNames.GetValueOrDefault(p.UserId, "Staff")).Distinct());
                vouchers.Add(new Voucher($"salary-{day:yyyyMMdd}", "Payment", day, $"TV-P-{day:yyyyMMdd}", $"Salaries/advances {day:dd-MM-yyyy}: {names}", Clean(lines)));
            }

            // ── Purchase vouchers (one per supplier invoice) and supplier payments ──
            foreach (var p in purchases.Where(p => DateOnly.FromDateTime(p.InvoiceDate) == day))
            {
                var expense = p.ExpenseLedger ?? L.Purchases;
                Use(expense, "Purchase Accounts");
                var lines = new List<Line> { new(expense, p.TaxableAmount), new(Supplier(p), -p.TotalAmount) };
                if (p.Cgst > 0) { lines.Add(new(L.InputCgst, p.Cgst)); Use(L.InputCgst, "Duties & Taxes"); }
                if (p.Sgst > 0) { lines.Add(new(L.InputSgst, p.Sgst)); Use(L.InputSgst, "Duties & Taxes"); }
                if (p.Igst > 0) { lines.Add(new(L.InputIgst, p.Igst)); Use(L.InputIgst, "Duties & Taxes"); }
                d.Purchases += p.TotalAmount;
                vouchers.Add(new Voucher($"purchase-{p.Id}", "Purchase", day, p.InvoiceNumber,
                    $"{p.SupplierName} invoice {p.InvoiceNumber}{(p.Description != null ? ": " + p.Description : "")}", Clean(lines)));
            }
            foreach (var p in purchases.Where(p => p.PaidAccountKey != null && p.PaidAt != null && DateOnly.FromDateTime(p.PaidAt.Value) == day))
            {
                d.SupplierPayments += p.TotalAmount;
                vouchers.Add(new Voucher($"supplier-payment-{p.Id}", "Payment", day, $"TV-SP-{p.InvoiceNumber}",
                    $"Paid {p.SupplierName} for invoice {p.InvoiceNumber}",
                    Clean(new() { new(Supplier(p), p.TotalAmount), new(Account(p.PaidAccountKey!), -p.TotalAmount) })));
            }

            days.Add(d);
        }

        return new Books(days, vouchers, ledgers, warnings, gstins);

        // Udhaar received after the bill (recorded as payments), to work out what was owed at billing
        decimal udhaarPaidSoFar(CustomerDue due) =>
            allPayments.Where(p => p.Source == MoneyInSource.Udhaar && p.CustomerDueId == due.Id).Sum(p => p.Amount);
    }

    /// <summary>Merges lines per ledger and drops zero lines.</summary>
    private static List<Line> Clean(List<Line> lines) =>
        lines.GroupBy(l => l.Ledger)
            .Select(g => new Line(g.Key, Math.Round(g.Sum(l => l.Amount), 2)))
            .Where(l => l.Amount != 0)
            .OrderByDescending(l => l.Amount > 0)
            .ToList();

    public async Task<AccountingSummaryResponse> GetSummaryAsync(string tenantId, DateOnly from, DateOnly to)
    {
        var books = await BuildBooksAsync(tenantId, from, to);
        var total = new AccountingDay { Date = to };
        foreach (var d in books.Days)
        {
            total.Bills += d.Bills; total.Sales += d.Sales; total.Packing += d.Packing; total.Cgst += d.Cgst; total.Sgst += d.Sgst;
            total.ServiceCharge += d.ServiceCharge; total.Discounts += d.Discounts; total.LoyaltyDiscount += d.LoyaltyDiscount;
            total.BillTotal += d.BillTotal; total.UdhaarGiven += d.UdhaarGiven; total.UdhaarReceived += d.UdhaarReceived;
            total.Refunds += d.Refunds; total.Salaries += d.Salaries; total.Purchases += d.Purchases;
            total.SupplierPayments += d.SupplierPayments; total.RoundOff += d.RoundOff;
            foreach (var (k, v) in d.Received) total.Received[k] = total.Received.GetValueOrDefault(k) + v;
        }
        return new AccountingSummaryResponse { Days = books.Days, Total = total, Warnings = books.Warnings };
    }

    // ── Tally XML ──

    /// <summary>Ledgers used in the period, to import once (Gateway of Tally → Import → Masters).</summary>
    public async Task<string> TallyMastersXmlAsync(string tenantId, DateOnly from, DateOnly to)
    {
        var books = await BuildBooksAsync(tenantId, from, to);
        var sb = Envelope("All Masters", out var close);
        foreach (var (name, group) in books.Ledgers.OrderBy(l => l.Value).ThenBy(l => l.Key))
        {
            sb.Append($"<TALLYMESSAGE xmlns:UDF=\"TallyUDF\"><LEDGER NAME=\"{X(name)}\" ACTION=\"Create\">");
            sb.Append($"<NAME.LIST TYPE=\"String\"><NAME>{X(name)}</NAME></NAME.LIST><PARENT>{X(group)}</PARENT>");
            sb.Append(group is "Sundry Debtors" or "Sundry Creditors" ? "<ISBILLWISEON>No</ISBILLWISEON>" : "");
            // Supplier's GSTIN lets the CA match input tax credit (GSTR-2B)
            if (books.Gstins.TryGetValue(name, out var gstin))
                sb.Append($"<GSTREGISTRATIONTYPE>Regular</GSTREGISTRATIONTYPE><PARTYGSTIN>{X(gstin)}</PARTYGSTIN>");
            sb.Append("</LEDGER></TALLYMESSAGE>\n");
        }
        sb.Append(close);
        return sb.ToString();
    }

    /// <summary>
    /// Vouchers for the period (Gateway of Tally → Import → Transactions). Each has a fixed REMOTEID,
    /// so re-importing a period with "Replace" updates vouchers instead of duplicating them.
    /// </summary>
    public async Task<string> TallyVouchersXmlAsync(string tenantId, DateOnly from, DateOnly to)
    {
        var books = await BuildBooksAsync(tenantId, from, to);
        var sb = Envelope("Vouchers", out var close);
        foreach (var v in books.Vouchers)
        {
            sb.Append($"<TALLYMESSAGE xmlns:UDF=\"TallyUDF\"><VOUCHER REMOTEID=\"tabverse-{X(tenantId)}-{X(v.RemoteId)}\" VCHTYPE=\"{v.Type}\" ACTION=\"Create\" OBJVIEW=\"Accounting Voucher View\">");
            sb.Append($"<DATE>{v.Date:yyyyMMdd}</DATE><VOUCHERTYPENAME>{v.Type}</VOUCHERTYPENAME><VOUCHERNUMBER>{X(v.Number)}</VOUCHERNUMBER>");
            sb.Append($"<NARRATION>{X(v.Narration)}</NARRATION><PERSISTEDVIEW>Accounting Voucher View</PERSISTEDVIEW>");
            foreach (var l in v.Lines)
            {
                // Tally: debit lines are "deemed positive" with a negative amount
                var debit = l.Amount > 0;
                sb.Append($"<ALLLEDGERENTRIES.LIST><LEDGERNAME>{X(l.Ledger)}</LEDGERNAME><ISDEEMEDPOSITIVE>{(debit ? "Yes" : "No")}</ISDEEMEDPOSITIVE>");
                sb.Append($"<AMOUNT>{(-l.Amount).ToString("0.00", Inv)}</AMOUNT></ALLLEDGERENTRIES.LIST>");
            }
            sb.Append("</VOUCHER></TALLYMESSAGE>\n");
        }
        sb.Append(close);
        return sb.ToString();
    }

    private static StringBuilder Envelope(string report, out string close)
    {
        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<ENVELOPE><HEADER><TALLYREQUEST>Import Data</TALLYREQUEST></HEADER><BODY><IMPORTDATA>");
        sb.Append($"<REQUESTDESC><REPORTNAME>{report}</REPORTNAME><STATICVARIABLES><SVCURRENTCOMPANY>##SVCURRENTCOMPANY</SVCURRENTCOMPANY></STATICVARIABLES></REQUESTDESC><REQUESTDATA>\n");
        close = "</REQUESTDATA></IMPORTDATA></BODY></ENVELOPE>\n";
        return sb;
    }

    private static string X(string s) => SecurityElement.Escape(s) ?? "";

    // ── CSV reports for the CA ──

    public async Task<(string FileName, string Csv)> ReportCsvAsync(string tenantId, string report, DateOnly from, DateOnly to)
    {
        var sb = new StringBuilder();
        string N(decimal v) => v.ToString("0.00", Inv);
        var period = $"{from:yyyyMMdd}-{to:yyyyMMdd}";

        switch (report)
        {
            case "daily":
            {
                var s = await GetSummaryAsync(tenantId, from, to);
                var accounts = s.Total.Received.Keys.OrderBy(k => k).ToList();
                sb.AppendLine("Date,Bills,Food sales,Packing,Loyalty discount,Other discounts,CGST,SGST,Service charge,Bill total,"
                    + string.Join(",", accounts.Select(a => Q($"Received: {a}"))) + ",Refunds,Udhaar given,Udhaar received,Salaries,Purchases,Supplier payments,Round off");
                foreach (var d in s.Days.Append(s.Total))
                    sb.AppendLine(string.Join(",", d == s.Total ? "TOTAL" : d.Date.ToString("yyyy-MM-dd"), d.Bills, N(d.Sales), N(d.Packing), N(d.LoyaltyDiscount), N(d.Discounts),
                        N(d.Cgst), N(d.Sgst), N(d.ServiceCharge), N(d.BillTotal), string.Join(",", accounts.Select(a => N(d.Received.GetValueOrDefault(a)))),
                        N(d.Refunds), N(d.UdhaarGiven), N(d.UdhaarReceived), N(d.Salaries), N(d.Purchases), N(d.SupplierPayments), N(d.RoundOff)));
                return ($"daily-summary-{period}.csv", sb.ToString());
            }
            case "gst":
            {
                var s = await GetSummaryAsync(tenantId, from, to);
                var tenant = await _db.Tenants.FirstAsync(t => t.TenantId == tenantId);
                sb.AppendLine($"GSTIN,{Q(tenant.Gstin ?? "")}");
                sb.AppendLine($"Rate,CGST {tenant.CgstPercent}% + SGST {tenant.SgstPercent}%");
                sb.AppendLine("Date,Bills,Taxable value,CGST,SGST,Total GST,Invoice value");
                foreach (var d in s.Days.Where(d => d.Bills > 0).Append(s.Total))
                {
                    var taxable = d.Sales + d.Packing - d.LoyaltyDiscount;
                    sb.AppendLine(string.Join(",", d == s.Total ? "TOTAL" : d.Date.ToString("yyyy-MM-dd"), d.Bills, N(taxable), N(d.Cgst), N(d.Sgst), N(d.Cgst + d.Sgst), N(d.BillTotal)));
                }
                return ($"gst-summary-{period}.csv", sb.ToString());
            }
            case "udhaar":
            {
                var today = BusinessClock.Today;
                sb.AppendLine("Customer,Mobile,Bill,Bill date,Bill amount,Paid,Due,Days old,Bucket");
                foreach (var d in await _db.CustomerDues.Where(d => !d.IsSettled && d.DueAmount > 0).OrderBy(d => d.CreatedAt).ToListAsync())
                {
                    var age = today.DayNumber - DateOnly.FromDateTime(BusinessClock.ToLocal(d.CreatedAt)).DayNumber;
                    var bucket = age <= 30 ? "0-30" : age <= 60 ? "31-60" : age <= 90 ? "61-90" : "90+";
                    sb.AppendLine(string.Join(",", Q(d.CustomerName), d.CustomerMobile, d.BillNumber, BusinessClock.ToLocal(d.CreatedAt).ToString("yyyy-MM-dd"),
                        N(d.BillAmount), N(d.PaidAmount), N(d.DueAmount), age, bucket));
                }
                return ($"udhaar-ageing-{today:yyyyMMdd}.csv", sb.ToString());
            }
            case "purchases":
            {
                var settings = await GetSettingsAsync(tenantId);
                sb.AppendLine("Invoice date,Supplier,Supplier GSTIN,Invoice no,Description,Ledger,Taxable,CGST,SGST,IGST,Total,Paid from,Paid on");
                foreach (var p in (await GetPurchasesAsync(from, to)).OrderBy(p => p.InvoiceDate))
                    sb.AppendLine(string.Join(",", p.InvoiceDate.ToString("yyyy-MM-dd"), Q(p.SupplierName), p.SupplierGstin, Q(p.InvoiceNumber), Q(p.Description ?? ""),
                        Q(p.ExpenseLedger ?? settings.Ledgers.Purchases), N(p.TaxableAmount), N(p.Cgst), N(p.Sgst), N(p.Igst), N(p.TotalAmount),
                        Q(p.PaidAccountKey == null ? "On credit" : settings.PaymentAccounts.FirstOrDefault(a => a.Key == p.PaidAccountKey)?.Label ?? p.PaidAccountKey),
                        p.PaidAt?.ToString("yyyy-MM-dd")));
                return ($"purchase-register-{period}.csv", sb.ToString());
            }
            default:
                throw new ArgumentException("Unknown report.");
        }
    }

    private static string Q(string s) => s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}
