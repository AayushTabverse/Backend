using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using menu_backend.Models;
using Microsoft.IdentityModel.Tokens;

namespace menu_backend.Helpers;

public class JwtHelper
{
    private readonly IConfiguration _config;

    public JwtHelper(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"] ?? "YourSuperSecretKeyMustBeAtLeast32CharsLong!!")
        );

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("tenant_id", user.TenantId)
        };

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddHours(
            double.Parse(_config["Jwt:ExpiryHours"] ?? "24")
        );

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "menu-backend",
            audience: _config["Jwt:Audience"] ?? "menu-frontend",
            claims: claims,
            expires: expiry,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public DateTime GetExpiry() => DateTime.UtcNow.AddHours(
        double.Parse(_config["Jwt:ExpiryHours"] ?? "24")
    );
}
