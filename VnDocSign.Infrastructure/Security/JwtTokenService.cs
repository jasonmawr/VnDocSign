using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using VnDocSign.Application.Contracts.Interfaces.Security;

namespace VnDocSign.Infrastructure.Security;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _cfg;

    public JwtTokenService(IConfiguration cfg)
    {
        _cfg = cfg;
    }

    public string CreateToken(Guid userId, string username, IEnumerable<string> roles)
    {
        // ===== Read JWT key =====
        var keyStr = _cfg["Jwt:Key"]
                     ?? throw new InvalidOperationException("Missing 'Jwt:Key' in configuration.");

        if (Encoding.UTF8.GetByteCount(keyStr) < 32)
            throw new InvalidOperationException("Jwt:Key must contain at least 32 bytes.");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // ===== Claims =====
        var claims = new List<Claim>
        {
            new Claim("uid", userId.ToString()),                  // custom user id
            new Claim("uname", username),                        // custom username
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, username)
        };

        // add roles
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        // ===== Token expiry =====
        var minutes = _cfg.GetValue<int?>("Jwt:AccessTokenMinutes") ?? 480;

        var jwt = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"],
            audience: _cfg["Jwt:Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
