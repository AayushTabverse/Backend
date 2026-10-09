using menu_backend.DTOs.Staff;
using menu_backend.Models;

namespace menu_backend.Services.Interfaces;

public interface IStaffService
{
    Task<List<DailyAttendanceResponse>> GetAttendanceAsync(DateOnly date, UserRole actorRole);
    Task<List<DailyAttendanceResponse>> SaveAttendanceAsync(SaveAttendanceRequest request, UserRole actorRole, string? markedBy);

    Task<List<MonthlySalaryResponse>> GetMonthlySalaryAsync(int year, int month);
    Task UpdateSalaryAsync(Guid userId, decimal monthlySalary);

    Task<List<StaffPaymentResponse>> GetPaymentsAsync(int year, int month, Guid? userId = null);
    Task<StaffPaymentResponse> CreatePaymentAsync(CreateStaffPaymentRequest request, string? recordedBy);
    Task DeletePaymentAsync(Guid id);
}
