namespace NhaXe.Modules.TaiKhoan.Application;

using NhaXe.Modules.TaiKhoan.Domain;

// Các interface nhỏ, mỗi cái một việc (ISP). Tầng Infrastructure cài đặt chúng (DIP).

public interface ITaiKhoanRepository
{
    Task<TaiKhoan?> TimTheoSdtAsync(string sdt, CancellationToken ct);
    Task<TaiKhoan?> TimTheoIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<TaiKhoan>> LayNhieuAsync(IEnumerable<int> ids, CancellationToken ct);
    Task<IReadOnlyList<TaiKhoan>> LayTheoVaiTroAsync(string vaiTro, CancellationToken ct);
    Task<bool> TonTaiEmailAsync(string email, CancellationToken ct);
    /// <summary>Danh sách tài khoản cho Admin, lọc theo từ khoá (họ tên / SĐT / email) và trạng thái.</summary>
    Task<IReadOnlyList<TaiKhoan>> TimKiemAsync(string? tuKhoa, string? trangThai, CancellationToken ct);
    void Them(TaiKhoan taiKhoan);
    Task LuuAsync(CancellationToken ct);
}

public interface IPasswordHasher
{
    string Bam(string matKhau);
    bool KiemTra(string matKhau, string hash);
}

public interface IGuiMaKhoiPhuc
{
    Task GuiAsync(string email, string ma, CancellationToken ct);
}

public interface ITokenService
{
    string TaoToken(TaiKhoan tk, string phien);
}
