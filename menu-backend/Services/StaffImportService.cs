using System.Text.RegularExpressions;
using menu_backend.Data;
using menu_backend.DTOs.Staff;
using menu_backend.Helpers;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using static menu_backend.Helpers.ExcelImport;

namespace menu_backend.Services;

/// <summary>
/// Bulk staff import from Excel. Follows the same rules as adding staff one by one
/// (Roles.CanManage, email + password for app users, none for payroll-only staff).
/// Existing staff (matched by email, or by name for payroll-only staff) can have their name,
/// phone, job title and salary updated; roles and passwords are never changed by an import.
/// </summary>
public class StaffImportService : IStaffImportService
{
    public const int MaxRows = 500;

    private static readonly ExcelColumn[] Columns =
    {
        new("FullName", "Full Name *", new[] { "fullname", "name", "staffname", "employeename", "employee" }, Required: true, Width: 22),
        new("Role", "Role *", new[] { "role", "position", "designation", "access", "accessrole" }, Required: true, Width: 16),
        new("Email", "Email", new[] { "email", "emailid", "emailaddress", "loginemail", "mail" }, Width: 28),
        new("Password", "Password", new[] { "password", "initialpassword", "temppassword", "pass" }, Width: 16),
        new("Phone", "Phone", new[] { "phone", "mobile", "mobilenumber", "phonenumber", "contact", "contactnumber" }, Width: 14),
        new("JobTitle", "Job Title", new[] { "jobtitle", "title", "job", "work" }, Width: 20),
        new("MonthlySalary", "Monthly Salary (₹)", new[] { "monthlysalary", "salary", "salarypermonth", "monthlypay", "pay", "wage" }, Width: 18, NumberFormat: "0"),
    };

    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$");

    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public StaffImportService(AppDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public byte[] BuildTemplate() => ExcelImport.BuildTemplate("Staff", Columns, "How to fill the staff sheet",
        new[]
        {
            "• One row per staff member. Columns marked * are required.",
            "• Role: Manager, Cashier, Captain, Waiter, Kitchen, or \"Payroll only\" (cleaners, dishwashers, security… — they don't log in).",
            "  Managers can import Cashier, Captain, Waiter, Kitchen and Payroll only staff.",
            "• Email + Password: required for everyone except Payroll only staff. The password is their first login password (min 6 characters).",
            "  ⚠ The sheet contains passwords — delete the file after uploading, and ask staff to change their password.",
            "• Phone: 10-digit mobile number (optional).",
            "• Monthly Salary: in ₹ (optional, owner only). Used by the Salary tab.",
            "• Existing staff (same email, or same name for Payroll only staff) can be updated instead of duplicated:",
            "  name, phone, job title and salary. Roles and passwords are never changed by an import.",
            "• Nothing is deleted: staff not in the sheet stay as they are.",
            $"• Up to {MaxRows} rows per file. You'll see a preview before anything is saved.",
        },
        new[]
        {
            new object[] { "Ramesh Kumar", "Waiter", "ramesh@example.com", "welcome123", "9876543210", "Head waiter", 15000 },
            new object[] { "Sunita Devi", "Cashier", "sunita@example.com", "welcome123", "9876501234", "", 18000 },
            new object[] { "Mohan Singh", "Kitchen", "mohan@example.com", "welcome123", "", "Tandoor cook", 20000 },
            new object[] { "Raju", "Payroll only", "", "", "9123456789", "Cleaner", 9000 },
        });

    public async Task<StaffImportResult> ImportAsync(Stream xlsx, bool updateExisting, bool apply, UserRole actorRole)
    {
        var rows = ReadRows(xlsx, "Staff", Columns, MaxRows, ParseRow);
        var actorIsOwner = Roles.IsOwner(actorRole);

        var staff = await _db.Users.ToListAsync(); // this restaurant only (tenant query filter)
        var byEmail = staff.Where(u => u.Email != null).ToDictionary(u => u.Email!.ToLowerInvariant());
        var payrollOnlyByName = staff.Where(u => u.Role == UserRole.Helper).GroupBy(u => Key(u.FullName)).ToDictionary(g => g.Key, g => g.ToList());

        // Login looks users up by email across all restaurants, so an email may only be used once overall
        var emailsInFile = rows.Where(r => r.Email != null).Select(r => r.Email!.ToLowerInvariant()).Distinct().ToList();
        var emailsElsewhere = emailsInFile.Count == 0 ? new HashSet<string>() : (await _db.Users.IgnoreQueryFilters()
                .Where(u => u.Email != null && !u.IsDeleted && u.TenantId != _tenantProvider.TenantId)
                .Select(u => u.Email!)
                .ToListAsync())
            .Select(e => e.ToLowerInvariant())
            .Where(e => emailsInFile.Contains(e))
            .ToHashSet();

        var seenEmail = new Dictionary<string, int>();
        var seenPayrollName = new Dictionary<string, int>();
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.FullName)) row.Errors.Add("Full name is required.");
            else if (row.FullName.Length > 100) row.Errors.Add("Full name is longer than 100 characters.");
            if (row.JobTitle?.Length > 100) row.Errors.Add("Job title is longer than 100 characters.");
            if (row.MonthlySalary < 0) row.Errors.Add("Salary cannot be negative.");
            if (row.MonthlySalary > 10_000_000) row.Errors.Add("Salary is too large.");
            if (row.MonthlySalary != null && !actorIsOwner) row.Errors.Add("Only the owner can set salaries — leave Monthly Salary blank.");

            var role = row.Role is null ? (UserRole?)null : Enum.Parse<UserRole>(row.Role);
            if (role is { } r && !Roles.CanManage(actorRole, r))
                row.Errors.Add($"You can't add {RoleLabel(r)} staff.");

            var payrollOnly = role == UserRole.Helper;
            if (payrollOnly)
            {
                if (row.Email != null || row.HasPassword)
                    row.Errors.Add("Payroll only staff don't log in — leave Email and Password blank.");
            }
            else if (role != null)
            {
                if (row.Email == null) row.Errors.Add("Email is required for staff who use the app.");
                else if (!EmailPattern.IsMatch(row.Email) || row.Email.Length > 200) row.Errors.Add($"\"{row.Email}\" is not a valid email.");
            }

            // ── Find an existing staff member ──
            User? existing = null;
            if (!payrollOnly && row.Email != null)
            {
                var email = row.Email.ToLowerInvariant();
                if (seenEmail.TryGetValue(email, out var first)) row.Errors.Add($"Duplicate of row {first} — each email can appear only once.");
                else seenEmail[email] = row.RowNumber;

                if (byEmail.TryGetValue(email, out var u)) existing = u;
                else if (emailsElsewhere.Contains(email)) row.Errors.Add("This email is already used by another Tabverse account.");
            }
            else if (payrollOnly && !string.IsNullOrWhiteSpace(row.FullName))
            {
                var key = Key(row.FullName);
                if (seenPayrollName.TryGetValue(key, out var first)) row.Errors.Add($"Duplicate of row {first} — payroll only staff are matched by name, so names must be unique.");
                else seenPayrollName[key] = row.RowNumber;

                if (payrollOnlyByName.TryGetValue(key, out var matches))
                {
                    if (matches.Count > 1) row.Errors.Add($"{matches.Count} payroll only staff are named \"{row.FullName}\"; rename them so the match is unambiguous.");
                    else existing = matches[0];
                }
            }

            if (existing == null)
            {
                if (!payrollOnly && role != null && !row.HasPassword) row.Errors.Add("Password is required for new staff who use the app.");
                else if (!payrollOnly && row.Password?.Length < 6) row.Errors.Add("Password must be at least 6 characters.");
            }
            else
            {
                if (role != null && existing.Role != role)
                    row.Errors.Add($"Already on your team as {RoleLabel(existing.Role)}. Change roles from the Team tab, not by import.");
                else if (!Roles.CanManage(actorRole, existing.Role))
                    row.Errors.Add($"You can't change {RoleLabel(existing.Role)} staff.");
            }

            if (row.Errors.Count == 0 && existing != null)
            {
                DescribeChanges(row, existing);
                row.Action = !updateExisting ? "Skip" : row.Changes.Count == 0 ? "Unchanged" : "Update";
            }

            if (row.Errors.Count > 0) row.Action = "Error";
        }

        var result = new StaffImportResult { Rows = rows };
        result.Tally();

        if (!apply || result.CreateCount + result.UpdateCount == 0)
            return result;

        // ── Apply: one SaveChanges, so the import is all-or-nothing ──
        var tenantId = _tenantProvider.TenantId!;
        foreach (var row in rows.Where(r => r.Action is "Create" or "Update"))
        {
            if (row.Action == "Update")
            {
                var user = row.Email != null
                    ? byEmail[row.Email.ToLowerInvariant()]
                    : payrollOnlyByName[Key(row.FullName)].Single();
                if (row.Email != null) user.FullName = row.FullName.Trim(); // payroll only staff are matched by name
                if (row.Provided.Contains("Phone")) user.Phone = row.Phone;
                if (row.Provided.Contains("JobTitle")) user.JobTitle = row.JobTitle;
                if (row.Provided.Contains("MonthlySalary")) user.MonthlySalary = row.MonthlySalary!.Value;
                user.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var role = Enum.Parse<UserRole>(row.Role!);
                var payrollOnly = role == UserRole.Helper;
                _db.Users.Add(new User
                {
                    TenantId = tenantId,
                    FullName = row.FullName.Trim(),
                    Email = payrollOnly ? null : row.Email,
                    PasswordHash = payrollOnly ? null : BCrypt.Net.BCrypt.HashPassword(row.Password),
                    Phone = row.Phone,
                    JobTitle = row.JobTitle,
                    Role = role,
                    MonthlySalary = row.MonthlySalary ?? 0,
                    IsActive = true
                });
            }
        }

        await _db.SaveChangesAsync();
        result.Applied = true;
        return result;
    }

    private static StaffImportRow ParseRow(SheetRow s)
    {
        var r = new StaffImportRow
        {
            RowNumber = s.RowNumber,
            FullName = s.Text("FullName"),
            Email = s.TextOrNull("Email")?.ToLowerInvariant(),
            Password = s.TextOrNull("Password"),
            JobTitle = s.TextOrNull("JobTitle")
        };
        r.HasPassword = r.Password != null;
        foreach (var field in new[] { "Phone", "JobTitle", "MonthlySalary" })
            if (s.Has(field)) r.Provided.Add(field);

        var roleText = s.Text("Role");
        if (roleText.Length == 0) r.Errors.Add("Role is required.");
        else if (Key(roleText) is "owner" or "admin" or "restaurantadmin" or "superadmin")
            r.Errors.Add("Owner accounts can't be imported.");
        else if (ParseRole(roleText) is { } role) r.Role = role.ToString();
        else r.Errors.Add($"Role \"{roleText}\" isn't recognized. Use Manager, Cashier, Captain, Waiter, Kitchen or Payroll only.");

        var phone = s.Text("Phone");
        if (phone.Length > 0)
        {
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length == 12 && digits.StartsWith("91")) digits = digits[2..];
            else if (digits.Length == 11 && digits.StartsWith('0')) digits = digits[1..];
            if (digits.Length == 10) r.Phone = digits;
            else r.Errors.Add($"Phone \"{phone}\" must be a 10-digit mobile number.");
        }

        var salary = s.Decimal("MonthlySalary", "Monthly salary", r.Errors);
        r.MonthlySalary = salary is null ? null : Math.Round(salary.Value, 2);
        return r;
    }

    private static UserRole? ParseRole(string text) => Key(text) switch
    {
        "manager" or "restaurantmanager" or "floormanager" => UserRole.Manager,
        "cashier" or "billing" or "biller" => UserRole.Cashier,
        "captain" or "headwaiter" or "seniorwaiter" => UserRole.Captain,
        "waiter" or "server" or "steward" or "waitress" => UserRole.Waiter,
        "kitchen" or "chef" or "cook" or "kitchenstaff" => UserRole.Kitchen,
        "payrollonly" or "payroll" or "noappaccess" or "noaccess" or "helper" or "none" or "support" => UserRole.Helper,
        _ => null
    };

    private static string RoleLabel(UserRole role) => role switch
    {
        UserRole.RestaurantAdmin => "Owner",
        UserRole.Helper => "Payroll only",
        _ => role.ToString()
    };

    private static void DescribeChanges(StaffImportRow row, User user)
    {
        // Blank cells keep the current value — show that value in the preview, too
        if (!row.Provided.Contains("Phone")) row.Phone = user.Phone;
        if (!row.Provided.Contains("JobTitle")) row.JobTitle = user.JobTitle;
        if (!row.Provided.Contains("MonthlySalary")) row.MonthlySalary = user.MonthlySalary;

        if (row.Email != null && row.FullName.Trim() != user.FullName) row.Changes.Add($"Name {user.FullName} → {row.FullName.Trim()}");
        if (row.Phone != user.Phone) row.Changes.Add($"Phone {user.Phone ?? "—"} → {row.Phone ?? "—"}");
        if (row.JobTitle != user.JobTitle) row.Changes.Add($"Job title {user.JobTitle ?? "—"} → {row.JobTitle ?? "—"}");
        if (row.MonthlySalary != user.MonthlySalary) row.Changes.Add($"Salary ₹{user.MonthlySalary:0.##} → ₹{row.MonthlySalary:0.##}");
    }
}
