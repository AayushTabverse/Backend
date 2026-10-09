using menu_backend.Data;
using menu_backend.DTOs.Staff;
using menu_backend.Helpers;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

public class StaffService : IStaffService
{
    private readonly AppDbContext _db;

    public StaffService(AppDbContext db)
    {
        _db = db;
    }

    // ── Attendance ──

    public async Task<List<DailyAttendanceResponse>> GetAttendanceAsync(DateOnly date, UserRole actorRole)
    {
        var day = date.ToDateTime(TimeOnly.MinValue);

        var records = await _db.StaffAttendances
            .Where(a => a.Date == day)
            .ToDictionaryAsync(a => a.UserId);

        // Active staff, plus anyone since deactivated who already has a record for this day
        var staff = (await GetPayrollStaffAsync())
            .Where(u => Roles.CanManage(actorRole, u.Role))
            .Where(u => u.IsActive || records.ContainsKey(u.Id));

        return staff.Select(u =>
        {
            records.TryGetValue(u.Id, out var record);
            return new DailyAttendanceResponse
            {
                UserId = u.Id,
                FullName = u.FullName,
                Role = u.Role.ToString(),
                JobTitle = u.JobTitle,
                Status = record?.Status.ToString(),
                Notes = record?.Notes,
                MarkedBy = record?.MarkedBy
            };
        }).ToList();
    }

    public async Task<List<DailyAttendanceResponse>> SaveAttendanceAsync(SaveAttendanceRequest request, UserRole actorRole, string? markedBy)
    {
        if (request.Date > BusinessClock.Today)
            throw new ArgumentException("Cannot mark attendance for a future date.");

        var day = request.Date.ToDateTime(TimeOnly.MinValue);
        var userIds = request.Entries.Select(e => e.UserId).Distinct().ToList();

        var validUserIds = (await GetPayrollStaffAsync())
            .Where(u => Roles.CanManage(actorRole, u.Role))
            .Select(u => u.Id)
            .ToHashSet();
        if (!userIds.All(validUserIds.Contains))
            throw new ArgumentException("One or more staff members were not found.");

        var existing = await _db.StaffAttendances
            .Where(a => a.Date == day)
            .ToDictionaryAsync(a => a.UserId);

        foreach (var entry in request.Entries)
        {
            existing.TryGetValue(entry.UserId, out var record);

            if (string.IsNullOrEmpty(entry.Status))
            {
                // Clearing a day removes the record so the unique (user, date) index stays free
                if (record != null) _db.StaffAttendances.Remove(record);
                continue;
            }

            if (!Enum.TryParse<AttendanceStatus>(entry.Status, true, out var status))
                throw new ArgumentException($"Invalid attendance status: {entry.Status}");

            if (record == null)
            {
                _db.StaffAttendances.Add(new StaffAttendance
                {
                    UserId = entry.UserId,
                    Date = day,
                    Status = status,
                    Notes = entry.Notes,
                    MarkedBy = markedBy
                });
            }
            else if (record.Status != status || record.Notes != entry.Notes)
            {
                record.Status = status;
                record.Notes = entry.Notes;
                record.MarkedBy = markedBy;
                record.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
        return await GetAttendanceAsync(request.Date, actorRole);
    }

    // ── Salary ──

    public async Task<List<MonthlySalaryResponse>> GetMonthlySalaryAsync(int year, int month)
    {
        ValidateMonth(year, month);

        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var daysInMonth = DateTime.DaysInMonth(year, month);

        var attendance = await _db.StaffAttendances
            .Where(a => a.Date >= monthStart && a.Date < monthEnd)
            .GroupBy(a => new { a.UserId, a.Status })
            .Select(g => new { g.Key.UserId, g.Key.Status, Count = g.Count() })
            .ToListAsync();

        var payments = await _db.StaffPayments
            .Where(p => p.Year == year && p.Month == month)
            .GroupBy(p => new { p.UserId, p.Type })
            .Select(g => new { g.Key.UserId, g.Key.Type, Total = g.Sum(p => p.Amount) })
            .ToListAsync();

        var userIdsWithActivity = attendance.Select(a => a.UserId)
            .Concat(payments.Select(p => p.UserId))
            .ToHashSet();

        var staff = (await GetPayrollStaffAsync())
            .Where(u => u.IsActive || userIdsWithActivity.Contains(u.Id));

        return staff.Select(u =>
        {
            int Days(AttendanceStatus s) =>
                attendance.FirstOrDefault(a => a.UserId == u.Id && a.Status == s)?.Count ?? 0;
            decimal Paid(StaffPaymentType t) =>
                payments.FirstOrDefault(p => p.UserId == u.Id && p.Type == t)?.Total ?? 0;

            var present = Days(AttendanceStatus.Present);
            var absent = Days(AttendanceStatus.Absent);
            var half = Days(AttendanceStatus.HalfDay);
            var leave = Days(AttendanceStatus.Leave);

            var perDay = u.MonthlySalary / daysInMonth;
            var deduction = Math.Round(perDay * (absent + 0.5m * half), 2);
            var earned = u.MonthlySalary - deduction;
            var advance = Paid(StaffPaymentType.Advance);
            var salary = Paid(StaffPaymentType.Salary);

            return new MonthlySalaryResponse
            {
                UserId = u.Id,
                FullName = u.FullName,
                Role = u.Role.ToString(),
                JobTitle = u.JobTitle,
                IsActive = u.IsActive,
                MonthlySalary = u.MonthlySalary,
                DaysInMonth = daysInMonth,
                PresentDays = present,
                AbsentDays = absent,
                HalfDays = half,
                LeaveDays = leave,
                UnmarkedDays = daysInMonth - present - absent - half - leave,
                Deduction = deduction,
                Earned = earned,
                AdvancePaid = advance,
                SalaryPaid = salary,
                BonusPaid = Paid(StaffPaymentType.Bonus),
                Balance = earned - advance - salary
            };
        }).ToList();
    }

    public async Task UpdateSalaryAsync(Guid userId, decimal monthlySalary)
    {
        if (monthlySalary < 0)
            throw new ArgumentException("Salary cannot be negative.");

        var user = await PayrollStaffQuery().FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("Staff member not found.");

        user.MonthlySalary = monthlySalary;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── Payments ──

    public async Task<List<StaffPaymentResponse>> GetPaymentsAsync(int year, int month, Guid? userId = null)
    {
        ValidateMonth(year, month);

        var query = _db.StaffPayments.Where(p => p.Year == year && p.Month == month);
        if (userId.HasValue)
            query = query.Where(p => p.UserId == userId.Value);

        var payments = await query
            .OrderByDescending(p => p.PaidOn)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync();

        var names = (await GetPayrollStaffAsync()).ToDictionary(u => u.Id, u => u.FullName);

        return payments.Select(p => MapPayment(p, names.GetValueOrDefault(p.UserId, "Removed staff"))).ToList();
    }

    public async Task<StaffPaymentResponse> CreatePaymentAsync(CreateStaffPaymentRequest request, string? recordedBy)
    {
        ValidateMonth(request.Year, request.Month);

        if (!Enum.TryParse<StaffPaymentType>(request.Type, true, out var type))
            throw new ArgumentException($"Invalid payment type: {request.Type}");
        if (!Enum.TryParse<StaffPaymentMode>(request.Mode, true, out var mode))
            throw new ArgumentException($"Invalid payment mode: {request.Mode}");
        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.");

        var user = await PayrollStaffQuery().FirstOrDefaultAsync(u => u.Id == request.UserId)
            ?? throw new KeyNotFoundException("Staff member not found.");

        var payment = new StaffPayment
        {
            UserId = user.Id,
            Year = request.Year,
            Month = request.Month,
            Type = type,
            Amount = request.Amount,
            Mode = mode,
            PaidOn = (request.PaidOn ?? BusinessClock.Today).ToDateTime(TimeOnly.MinValue),
            Notes = request.Notes,
            RecordedBy = recordedBy
        };

        _db.StaffPayments.Add(payment);
        await _db.SaveChangesAsync();

        return MapPayment(payment, user.FullName);
    }

    public async Task DeletePaymentAsync(Guid id)
    {
        var payment = await _db.StaffPayments.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new KeyNotFoundException("Payment not found.");

        payment.IsDeleted = true;
        payment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Staff on payroll: everyone except owners (restaurant admins / super admins).
    /// </summary>
    private IQueryable<User> PayrollStaffQuery() =>
        _db.Users.Where(u => u.Role != UserRole.RestaurantAdmin && u.Role != UserRole.SuperAdmin);

    private Task<List<User>> GetPayrollStaffAsync() =>
        PayrollStaffQuery()
            .OrderBy(u => u.Role)
            .ThenBy(u => u.FullName)
            .ToListAsync();

    private static void ValidateMonth(int year, int month)
    {
        if (year < 2000 || year > 2100 || month < 1 || month > 12)
            throw new ArgumentException("Invalid month.");
    }

    private static StaffPaymentResponse MapPayment(StaffPayment p, string fullName) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        FullName = fullName,
        Year = p.Year,
        Month = p.Month,
        Type = p.Type.ToString(),
        Amount = p.Amount,
        Mode = p.Mode.ToString(),
        PaidOn = DateOnly.FromDateTime(p.PaidOn),
        Notes = p.Notes,
        RecordedBy = p.RecordedBy,
        CreatedAt = p.CreatedAt
    };
}
