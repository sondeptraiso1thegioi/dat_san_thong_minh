using NhaXe.Modules.TaiKhoan.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.TaiKhoan.Application;

/// <summary>MaDay: mã đẩy thông báo của máy đang đăng nhập (app điện thoại gửi; web để trống).</summary>
public record DangNhapLenh(string Sdt, string MatKhau, string? MaDay = null);
public record DangNhapKetQua(string Token, int Id, string HoTen, string VaiTro);

/// <summary>Use case Đăng nhập: chỉ điều phối nghiệp vụ, không biết dữ liệu nằm ở SQL Server hay token là JWT.</summary>
public class DangNhapUseCase
{
    private readonly ITaiKhoanRepository _repo;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _token;
    private readonly IClock _clock;

    private readonly Lazy<string> HashGia;

    public DangNhapUseCase(ITaiKhoanRepository repo, IPasswordHasher hasher, ITokenService token, IClock clock)
    {
        _repo = repo; _hasher = hasher; _token = token; _clock = clock;
        HashGia = new(() => HashGiaChung ??= hasher.Bam(Guid.NewGuid().ToString()));
    }
    private static string? HashGiaChung;

    public async Task<DangNhapKetQua> ThucHienAsync(DangNhapLenh lenh, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(lenh.Sdt) || string.IsNullOrWhiteSpace(lenh.MatKhau))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "THIEU_THONG_TIN", "Nhập số điện thoại và mật khẩu.");

        var tk = await _repo.TimTheoSdtAsync(ThongTinDangKy.BoKyTuPhanCach(lenh.Sdt), ct); // "0912 345 678", "+84912345678" đều tìm ra
        var bayGio = _clock.BayGio;
        // Cùng một thông báo cho "sai số điện thoại" và "sai mật khẩu" để không lộ số nào đã đăng ký.
        var saiDangNhap = Domain.TaiKhoan.LoiDangNhap();
        if (tk is null)
        {
            _hasher.KiemTra(lenh.MatKhau, HashGia.Value); // vẫn tốn thời gian bcrypt như tài khoản thật: không dò được SĐT qua thời gian phản hồi
            throw saiDangNhap;
        }
        // Lớp 3 chống dò mật khẩu: khoá theo TÀI KHOẢN (lớp 1 NGINX, lớp 2 .NET Rate Limiting chặn theo IP)
        tk.KiemTraChoThuDangNhap(bayGio);
        if (!_hasher.KiemTra(lenh.MatKhau, tk.MatKhauHash))
        {
            tk.GhiSaiMatKhau(bayGio);
            try { await _repo.LuuAsync(ct); }
            catch (LoiNghiepVu e) when (e.Ma == "TAI_KHOAN_DA_DOI") { } // hai lần sai cùng lúc: bỏ qua một lần đếm, không lộ lỗi
            throw saiDangNhap;
        }

        var phien = Guid.NewGuid().ToString("N");
        tk.MoPhienMoi(phien, bayGio, lenh.MaDay);
        await _repo.LuuAsync(ct);

        return new DangNhapKetQua(_token.TaoToken(tk, phien), tk.Id, tk.HoTen, tk.VaiTro);
    }
}
