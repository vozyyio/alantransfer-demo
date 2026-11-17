using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AlanTransfer.Api.Domain;
using Microsoft.IdentityModel.Tokens;

namespace AlanTransfer.Api.Auth;

public class JwtService
{
    private readonly IConfiguration _cfg;
    public JwtService(IConfiguration cfg) => _cfg = cfg;

    public string CreateToken(AppUser user)
    {
        var key = _cfg["Jwt:Key"] ?? "demo-super-secret-key-change-in-prod-123456789";
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
        };
        if (user.AgencyId is not null) claims.Add(new("agencyId", user.AgencyId.ToString()!));
        if (user.DriverId is not null) claims.Add(new("driverId", user.DriverId.ToString()!));

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
