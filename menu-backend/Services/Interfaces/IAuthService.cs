using menu_backend.DTOs.Auth;

namespace menu_backend.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RegisterUserAsync(RegisterRequest request);
    Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task<string> ForgotPasswordAsync(string email);
    Task<List<StaffResponse>> GetStaffAsync(string tenantId);
    Task ToggleUserActiveAsync(Guid userId, string tenantId);
    Task DeleteUserAsync(Guid userId, string tenantId);
}
