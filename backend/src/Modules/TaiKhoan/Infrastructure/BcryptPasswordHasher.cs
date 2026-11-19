using NhaXe.Modules.TaiKhoan.Application;

namespace NhaXe.Modules.TaiKhoan.Infrastructure;

public class BcryptPasswordHasher : IPasswordHasher
{
    public string Bam(string matKhau) => BCrypt.Net.BCrypt.HashPassword(matKhau, workFactor: 12);
    public bool KiemTra(string matKhau, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(matKhau, hash); }
        catch (BCrypt.Net.SaltParseException) { return false; } // hash hỏng thì coi như sai mật khẩu
    }
}
