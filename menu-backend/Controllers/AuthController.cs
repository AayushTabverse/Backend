using System.Security.Claims;
using menu_backend.DTOs;
using menu_backend.DTOs.Auth;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Register a new restaurant (tenant) with an admin account.
    /// </summary>
    [HttpPost("register-tenant")]
    public async Task<IActionResult> RegisterTenant([FromBody] RegisterTenantRequest request)
    {
        try
        {
            var result = await _authService.RegisterTenantAsync(request);
            return Ok(ApiResponse<AuthResponse>.Ok(result, "Tenant registered successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Login with email and password.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _authService.LoginAsync(request);
            return Ok(ApiResponse<AuthResponse>.Ok(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Register a new staff user (requires RestaurantAdmin role).
    /// </summary>
    [HttpPost("register-user")]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
    public async Task<IActionResult> RegisterUser([FromBody] RegisterRequest request)
    {
        try
        {
            var result = await _authService.RegisterUserAsync(request);
            return Ok(ApiResponse<AuthResponse>.Ok(result, "User registered successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Change password for the current user.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _authService.ChangePasswordAsync(userId, request);
            return Ok(ApiResponse.Ok("Password changed successfully."));
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Forgot password — resets to a temporary password.
    /// In demo mode returns the temp password; in production would email it.
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            var tempPassword = await _authService.ForgotPasswordAsync(request.Email);
            var response = new ForgotPasswordResponse
            {
                Message = "Password has been reset. Use the temporary password below to login, then change your password.",
                TempPassword = tempPassword
            };
            return Ok(ApiResponse<ForgotPasswordResponse>.Ok(response));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get all staff members for the current tenant.
    /// </summary>
    [HttpGet("staff")]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
    public async Task<IActionResult> GetStaff()
    {
        var tenantId = User.FindFirstValue("tenant_id")!;
        var staff = await _authService.GetStaffAsync(tenantId);
        return Ok(ApiResponse<List<StaffResponse>>.Ok(staff));
    }

    /// <summary>
    /// Toggle a staff member's active status.
    /// </summary>
    [HttpPost("staff/{userId}/toggle-active")]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
    public async Task<IActionResult> ToggleUserActive(Guid userId)
    {
        try
        {
            var tenantId = User.FindFirstValue("tenant_id")!;
            await _authService.ToggleUserActiveAsync(userId, tenantId);
            return Ok(ApiResponse.Ok("User status toggled."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Delete (soft) a staff member.
    /// </summary>
    [HttpDelete("staff/{userId}")]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        try
        {
            var tenantId = User.FindFirstValue("tenant_id")!;
            await _authService.DeleteUserAsync(userId, tenantId);
            return Ok(ApiResponse.Ok("User deleted."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }
}
