using NhaXe.Modules.TaiKhoan.Domain;
using NhaXe.SharedKernel;
using TK = NhaXe.Modules.TaiKhoan.Domain.TaiKhoan;

namespace NhaXe.Modules.TaiKhoan.Application;

public record TaoAdminDauTienLenh(string HoTen, string Sdt, string Email, string MatKhau);

/// <summary>
/// Tạo tài khoản ADMIN khi hệ thống CHƯA có admin nào (CSDL mới tạo). Đã có admin thì không làm gì:
/// đổi mật khẩu admin sau đó bằng trang Đổi mật khẩu, không phải bằng cấu hình.
/// Kiểm tra họ tên / SĐT / email / mật khẩu y như đăng ký thường.
/// </summary>
public class TaoAdminDauTienUseCase(ITaiKhoanRepository repo, IPasswordHasher hasher)
{
    /// <returns>true nếu vừa tạo; false nếu đã có admin (kể cả do instance API khác vừa tạo cùng lúc).</returns>
    public async Task<bool> ThucHienAsync(TaoAdminDauTienLenh lenh, CancellationToken ct = default)
    {
        if ((await repo.LayTheoVaiTroAsync(VaiTro.Admin, ct)).Count > 0) return false;
        var (ten, sdt, email) = ThongTinDangKy.ChuanHoa(lenh.HoTen, lenh.Sdt, lenh.Email, lenh.MatKhau);
        if (await repo.TimTheoSdtAsync(sdt, ct) is not null || await repo.TonTaiEmailAsync(email, ct))
            throw new LoiNghiepVu(LoaiLoi.XungDot, "TAI_KHOAN_TRUNG",
                "SĐT hoặc email cấu hình cho admin đầu tiên đã thuộc một tài khoản người dùng khác.");
        repo.Them(TK.TaoAdmin(ten, sdt, email, hasher.Bam(lenh.MatKhau)));
        try { await repo.LuuAsync(ct); }
        catch (LoiNghiepVu e) when (e.Ma == "TAI_KHOAN_TRUNG") { return false; } // 2 instance API khởi động cùng lúc: bản kia đã tạo
        return true;
    }
}
