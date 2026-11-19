using NhaXe.SharedKernel;

namespace NhaXe.Modules.TaiKhoan.Application;

public record TaiKhoanAdminDto(int Id, string HoTen, string Sdt, string? Email, string VaiTro, string TrangThai,
    DateTime? DangNhapLuc, bool DangTamKhoaDangNhap, DateTime TaoLuc);

/// <summary>UC-A: Admin xem danh sách tài khoản (v10, module 1).</summary>
public class DanhSachTaiKhoanUseCase(ITaiKhoanRepository repo, IClock clock)
{
    public async Task<IReadOnlyList<TaiKhoanAdminDto>> ThucHienAsync(string? tuKhoa, string? trangThai, CancellationToken ct = default)
    {
        if (trangThai is not null and not ("HOAT_DONG" or "KHOA"))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "TRANG_THAI_SAI", "Trạng thái chỉ là HOAT_DONG hoặc KHOA.");
        var bayGio = clock.BayGio;
        return (await repo.TimKiemAsync(tuKhoa, trangThai, ct))
            .Select(t => new TaiKhoanAdminDto(t.Id, t.HoTen, t.Sdt, t.Email, t.VaiTro, t.TrangThai, t.DangNhapLuc,
                t.KhoaDangNhapDen > bayGio, t.TaoLuc))
            .ToList();
    }
}

/// <summary>UC-A: Admin khoá / mở khoá tài khoản. Khoá thì máy đang đăng nhập bị đăng xuất ngay (phiên bị xoá).</summary>
public class KhoaTaiKhoanUseCase(ITaiKhoanRepository repo)
{
    public async Task ThucHienAsync(int adminId, int taiKhoanId, bool khoa, CancellationToken ct = default)
    {
        if (adminId == taiKhoanId)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "KHONG_TU_KHOA", "Không tự khoá tài khoản của mình.");
        var tk = await repo.TimTheoIdAsync(taiKhoanId, ct)
            ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_CO_TAI_KHOAN", "Không tìm thấy tài khoản.");
        if (khoa) tk.KhoaBoiAdmin(); else tk.MoKhoaBoiAdmin();
        await repo.LuuAsync(ct);
    }
}
