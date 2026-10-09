using menu_backend.Data;
using menu_backend.DTOs.Auth;
using menu_backend.Helpers;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly JwtHelper _jwt;

    public AuthService(AppDbContext db, JwtHelper jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request)
    {
        // Check if email already exists globally
        var exists = await _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == request.AdminEmail && !u.IsDeleted);

        if (exists)
            throw new InvalidOperationException("Email already registered.");

        var tenantId = Guid.NewGuid().ToString();

        var tenant = new Tenant
        {
            TenantId = tenantId,
            Name = request.RestaurantName,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.AdminEmail
        };

        var admin = new User
        {
            TenantId = tenantId,
            FullName = request.AdminName,
            Email = request.AdminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
            Role = UserRole.RestaurantAdmin,
            IsActive = true
        };

        _db.Tenants.Add(tenant);
        _db.Users.Add(admin);
        await _db.SaveChangesAsync();

        var token = _jwt.GenerateToken(admin);

        return new AuthResponse
        {
            Token = token,
            UserId = admin.Id.ToString(),
            TenantId = tenantId,
            Email = admin.Email ?? string.Empty,
            FullName = admin.FullName,
            Role = admin.Role.ToString(),
            ExpiresAt = _jwt.GetExpiry()
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted && u.IsActive);

        if (user == null || user.Role == UserRole.Helper || user.PasswordHash == null
            || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = _jwt.GenerateToken(user);

        return new AuthResponse
        {
            Token = token,
            UserId = user.Id.ToString(),
            TenantId = user.TenantId,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            ExpiresAt = _jwt.GetExpiry()
        };
    }

    public async Task<AuthResponse> RegisterUserAsync(RegisterRequest request, UserRole actorRole)
    {
        if (!Enum.TryParse<UserRole>(request.Role, true, out var role) || !Enum.IsDefined(role))
            throw new ArgumentException("Invalid role.");

        if (!Roles.CanManage(actorRole, role))
            throw new UnauthorizedAccessException($"You are not allowed to add {role} accounts.");

        // Payroll-only staff never log in, so they get no email or password
        var payrollOnly = role == UserRole.Helper;
        var email = payrollOnly ? null : request.Email?.Trim();

        if (!payrollOnly && (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(request.Password)))
            throw new ArgumentException("Email and password are required for staff who use the app.");

        if (email != null)
        {
            var exists = await _db.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Email == email && u.TenantId == request.TenantId && !u.IsDeleted);

            if (exists)
                throw new InvalidOperationException("Email already registered for this tenant.");
        }

        var user = new User
        {
            TenantId = request.TenantId,
            FullName = request.FullName,
            Email = email,
            PasswordHash = payrollOnly ? null : BCrypt.Net.BCrypt.HashPassword(request.Password),
            Phone = request.Phone,
            JobTitle = string.IsNullOrWhiteSpace(request.JobTitle) ? null : request.JobTitle.Trim(),
            Role = role,
            IsActive = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return new AuthResponse
        {
            Token = payrollOnly ? string.Empty : _jwt.GenerateToken(user),
            UserId = user.Id.ToString(),
            TenantId = user.TenantId,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            ExpiresAt = _jwt.GetExpiry()
        };
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<string> ForgotPasswordAsync(string email)
    {
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted && u.IsActive);

        if (user == null || user.Role == UserRole.Helper)
            throw new KeyNotFoundException("No account found with this email.");

        // Generate a temporary password
        var tempPassword = $"Reset{new Random().Next(1000, 9999)}!";
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // In production: send email with tempPassword
        // For demo: return it directly
        return tempPassword;
    }

    public async Task<List<StaffResponse>> GetStaffAsync(string tenantId)
    {
        return await _db.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted)
            .OrderBy(u => u.Role)
            .ThenBy(u => u.FullName)
            .Select(u => new StaffResponse
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                Role = u.Role.ToString(),
                JobTitle = u.JobTitle,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            })
            .ToListAsync();
    }

    public async Task ToggleUserActiveAsync(Guid userId, string tenantId, Guid actorId, UserRole actorRole)
    {
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId && !u.IsDeleted)
            ?? throw new KeyNotFoundException("User not found.");

        EnsureCanManage(user, actorId, actorRole);

        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteUserAsync(Guid userId, string tenantId, Guid actorId, UserRole actorRole)
    {
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId && !u.IsDeleted)
            ?? throw new KeyNotFoundException("User not found.");

        EnsureCanManage(user, actorId, actorRole);

        user.IsDeleted = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private static void EnsureCanManage(User target, Guid actorId, UserRole actorRole)
    {
        if (target.Id == actorId)
            throw new UnauthorizedAccessException("You cannot change your own account here.");
        if (!Roles.CanManage(actorRole, target.Role))
            throw new UnauthorizedAccessException($"You are not allowed to manage {target.Role} accounts.");
    }
}
