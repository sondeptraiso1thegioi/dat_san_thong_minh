using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DatSan.Api.Config;
using DatSan.Api.DTOs;
using DatSan.Api.Models;
using DatSan.Api.Services.Interface;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DatSan.Api.Services.Implements;

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : IJwtTokenService
{
    public AuthResponse CreateSession(User user)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(settings.LifetimeMinutes);
        var profile = UserDto.From(user);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, profile.Id),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim("role", profile.Role),
            new Claim("name", profile.Name)
        };
        var token = new JwtSecurityToken(
            settings.Issuer, settings.Audience, claims, now.UtcDateTime, expiresAt.UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
                SecurityAlgorithms.HmacSha256));
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAt, profile);
    }
}
