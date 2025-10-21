﻿using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using VnDocSign.Application.Contracts.Interfaces.Security;
using System.Linq;

namespace VnDocSign.Infrastructure.Security;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _cfg;
    public JwtTokenService(IConfiguration cfg) => _cfg = cfg;

    public string CreateToken(Guid userId, string username, IEnumerable<string> roles)
    {
        var keyStr = _cfg["Jwt:Key"] ?? throw new InvalidOperationException("Missing Jwt:Key");
        // Key tối thiểu 32 bytes cho HS256 (>= 256-bit)
        if (Encoding.UTF8.GetByteCount(keyStr) < 32)
            throw new InvalidOperationException("Jwt:Key must be >= 32 bytes.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, username)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var jwt = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"],
            audience: _cfg["Jwt:Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
