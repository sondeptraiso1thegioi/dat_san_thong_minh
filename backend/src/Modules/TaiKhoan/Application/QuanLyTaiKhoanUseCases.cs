using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using NhaXe.Modules.TaiKhoan.Domain;
using NhaXe.SharedKernel;
using TK = NhaXe.Modules.TaiKhoan.Domain.TaiKhoan;

namespace NhaXe.Modules.TaiKhoan.Application;

public record DangKyLenh(string HoTen, string Sdt, string Email, string MatKhau);
public record TaiKhoanDaTao(int Id, string HoTen, string VaiTro);
public record QuenMatKhauLenh(string Sdt);
public record DatLaiMatKhauLenh(string Sdt, string Ma, string MatKhauMoi);
public record DoiMatKhauLenh(string MatKhauCu, string MatKhauMoi);

public class DangKyUseCase(ITaiKhoanRepository repo, IPasswordHasher hasher)
{
    public async Task<TaiKhoanDaTao> ThucHienAsync(DangKyLenh lenh, CancellationToken ct = default)
    {
        var (ten, sdt, email) = ThongTinDangKy.ChuanHoa(lenh.HoTen, lenh.Sdt, lenh.Email, lenh.MatKhau);
        if (await repo.TimTheoSdtAsync(sdt, ct) is not null || await repo.TonTaiEmailAsync(email, ct))
            throw new LoiNghiepVu(LoaiLoi.XungDot, "TAI_KHOAN_TRUNG", "Số điện thoại hoặc email đã được đăng ký.");
        var tk = TK.DangKy(ten, sdt, email, hasher.Bam(lenh.MatKhau));
        repo.Them(tk);
        await repo.LuuAsync(ct);
        return new(tk.Id, tk.HoTen, tk.VaiTro);
    }
}

public class QuenMatKhauUseCase(ITaiKhoanRepository repo, IGuiMaKhoiPhuc guiMa, IClock clock, ILogger<QuenMatKhauUseCase>? log = null)
{
    public const string ThongDiep = "Nếu tài khoản có email và đủ điều kiện, mã khôi phục sẽ được gửi. Mã có hiệu lực 15 phút.";
    public async Task ThucHienAsync(QuenMatKhauLenh lenh, CancellationToken ct = default)
    {
        var sdt = ThongTinDangKy.ChuanHoaSdt(lenh.Sdt);
        var tk = await repo.TimTheoSdtAsync(sdt, ct);
        if (tk is null || !tk.CoTheGuiMa(clock.BayGio)) return;
        var ma = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = BamMa(ma);
        var guiLucTruoc = tk.GuiMaKhoiPhucLuc;
        tk.CapMaKhoiPhuc(hash, clock.BayGio);
        await repo.LuuAsync(ct); // phải lưu mã trước: email đi rồi mà mã chưa lưu thì mã vô dụng

        try { await guiMa.GuiAsync(tk.Email!, ma, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Máy chủ email hỏng / chưa bật: huỷ mã vừa cấp để bấm lại được ngay, báo rõ 503 thay vì 500.
            log?.LogError(ex, "Không gửi được email mã khôi phục cho tài khoản {Id}", tk.Id);
            tk.HuyMaKhoiPhucChuaGui(hash, guiLucTruoc);
            try { await repo.LuuAsync(ct); }
            catch (Exception ex2) { log?.LogError(ex2, "Không huỷ được mã khôi phục chưa gửi của tài khoản {Id}", tk.Id); }
            throw new LoiNghiepVu(LoaiLoi.DichVuTamNgung, "CHUA_GUI_DUOC_EMAIL",
                "Hệ thống chưa gửi được email lúc này. Vui lòng thử lại sau ít phút.");
        }
    }

    internal static string BamMa(string? ma) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ma?.Trim() ?? "")));
}

public class DatLaiMatKhauUseCase(ITaiKhoanRepository repo, IPasswordHasher hasher, IClock clock)
{
    public async Task ThucHienAsync(DatLaiMatKhauLenh lenh, CancellationToken ct = default)
    {
        var sdt = ThongTinDangKy.ChuanHoaSdt(lenh.Sdt);
        ThongTinDangKy.KiemTraMatKhau(lenh.MatKhauMoi);
        var tk = await repo.TimTheoSdtAsync(sdt, ct) ??
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "MA_KHOI_PHUC_SAI", "Mã khôi phục không hợp lệ hoặc đã hết hạn.");
        tk.KiemTraMaKhoiPhuc(QuenMatKhauUseCase.BamMa(lenh.Ma), clock.BayGio);
        tk.DoiMatKhau(hasher.Bam(lenh.MatKhauMoi));
        await repo.LuuAsync(ct);
    }
}

public class DoiMatKhauUseCase(ITaiKhoanRepository repo, IPasswordHasher hasher)
{
    public async Task ThucHienAsync(int id, DoiMatKhauLenh lenh, CancellationToken ct = default)
    {
        ThongTinDangKy.KiemTraMatKhau(lenh.MatKhauMoi);
        var tk = await repo.TimTheoIdAsync(id, ct) ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_CO_TAI_KHOAN", "Không tìm thấy tài khoản.");
        if (string.IsNullOrEmpty(lenh.MatKhauCu) || !hasher.KiemTra(lenh.MatKhauCu, tk.MatKhauHash))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "SAI_MAT_KHAU", "Mật khẩu hiện tại không đúng.");
        tk.DoiMatKhau(hasher.Bam(lenh.MatKhauMoi));
        await repo.LuuAsync(ct);
    }
}

public class DangXuatUseCase(ITaiKhoanRepository repo)
{
    public async Task ThucHienAsync(int id, string phien, CancellationToken ct = default)
    {
        var tk = await repo.TimTheoIdAsync(id, ct);
        if (tk?.PhienHienTai != phien) return; // yêu cầu từ phiên cũ không được đóng phiên mới
        tk.DongPhien();
        await repo.LuuAsync(ct);
    }
}
