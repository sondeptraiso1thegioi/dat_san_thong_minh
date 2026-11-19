using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using NhaXe.Modules.TaiKhoan.Application;
using TK = NhaXe.Modules.TaiKhoan.Domain.TaiKhoan;

namespace NhaXe.Modules.TaiKhoan.Infrastructure;

public record JwtOptions(string Key, string Issuer, int SoGioHetHan);

public class JwtTokenService : ITokenService
{
    public const string ClaimPhien = "phien";
    private readonly JwtOptions _opt;
    public JwtTokenService(JwtOptions opt) => _opt = opt;

    public string TaoToken(TK tk, string phien)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, tk.Id.ToString()),
            new Claim("name", tk.HoTen),
            new Claim("role", tk.VaiTro),
            new Claim(ClaimPhien, phien)
        };
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_opt.Issuer, _opt.Issuer, claims, expires: DateTime.UtcNow.AddHours(_opt.SoGioHetHan), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
