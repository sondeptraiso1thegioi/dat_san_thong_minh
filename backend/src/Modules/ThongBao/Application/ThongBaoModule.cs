using NhaXe.Modules.TaiKhoan.Contracts;
using NhaXe.Modules.ThongBao.Contracts;
using NhaXe.SharedKernel;
using LoaiTB = NhaXe.Modules.ThongBao.Domain.LoaiThongBao;

namespace NhaXe.Modules.ThongBao.Application;

/// <summary>Một thông báo đẩy tới máy đang mở app: cùng nội dung vừa lưu, kèm id và nơi tới khi bấm.</summary>
public record TinDay(int TaiKhoanId, int Id, string Loai, string NoiDung, string? HanhDong, int? ThamChieuId);

/// <summary>
/// Đẩy thông báo tới máy đang mở app ngay lúc lưu (đề cương v10: SignalR, để không phụ thuộc độ trễ thông báo đẩy).
/// Cài đặt ở Api (SignalR Hub). Đẩy lỗi / máy không kết nối thì thôi: thông báo vẫn nằm trong hộp thư.
/// FCM (thông báo đẩy khi app đóng) chỉ dự kiến, cài đặt mới của interface này là đủ (OCP).
/// </summary>
public interface IDayThoiGianThuc
{
    Task DayAsync(IReadOnlyList<TinDay> ds, CancellationToken ct);
}

/// <summary>Cài đặt cổng công khai IThongBaoModule. Danh sách Admin hỏi module TaiKhoan qua ITaiKhoanModule.</summary>
public class ThongBaoModule : IThongBaoModule
{
    /// <summary>
    /// Cùng một thông báo (cùng người nhận, loại, nội dung, tham chiếu) gửi lại trong 30 giây thì bỏ qua:
    /// camera tự quét / Admin bấm chụp lại / 2 bản API cùng xử lý một xe không làm người dùng nhận 2 lần.
    /// </summary>
    public const int ChongLapGiay = 30;

    private readonly IThongBaoStore _store;
    private readonly ITaiKhoanModule _taiKhoan;
    private readonly IDayThoiGianThuc? _day;
    public ThongBaoModule(IThongBaoStore store, ITaiKhoanModule taiKhoan, IDayThoiGianThuc? day = null)
    { _store = store; _taiKhoan = taiKhoan; _day = day; }

    public Task GuiAsync(int taiKhoanId, string loai, string noiDung, DichThongBao? dich = null, CancellationToken ct = default) =>
        GuiNhieuAsync(new[] { taiKhoanId }, loai, noiDung, dich, ct);

    public async Task GuiNhieuAsync(IEnumerable<int> taiKhoanIds, string loai, string noiDung, DichThongBao? dich = null,
                                    CancellationToken ct = default)
    {
        var nd = LoaiTB.ChuanHoaNoiDung(loai, noiDung);
        KiemTraDich(dich);
        var ds = taiKhoanIds.Distinct().ToList(); // một người không nhận hai lần cùng một tin
        if (ds.Count == 0) return;
        await LuuVaDayAsync(ds, loai, nd, dich, ct);
    }

    public async Task GuiChoMoiAdminAsync(string loai, string noiDung, DichThongBao? dich = null, CancellationToken ct = default)
    {
        var nd = LoaiTB.ChuanHoaNoiDung(loai, noiDung);
        KiemTraDich(dich);
        var admin = await _taiKhoan.LayAdminIdsAsync(ct);
        if (admin.Count == 0) return;
        await LuuVaDayAsync(admin.ToList(), loai, nd, dich, ct);
    }

    private static void KiemTraDich(DichThongBao? dich)
    {
        if (dich is not null && !HanhDongThongBao.TatCa.Contains(dich.HanhDong))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "HANH_DONG_SAI", $"Hành động thông báo không hợp lệ: {dich.HanhDong}.");
    }

    private async Task LuuVaDayAsync(IReadOnlyList<int> ds, string loai, string nd, DichThongBao? dich, CancellationToken ct)
    {
        var daLuu = await _store.ThemAsync(ds, loai, nd, dich?.HanhDong, dich?.ThamChieuId, ChongLapGiay, ct);
        if (_day is null || daLuu.Count == 0) return;
        try { await _day.DayAsync(daLuu.Select(t => new TinDay(t.TaiKhoanId, t.Id, loai, nd, dich?.HanhDong, dich?.ThamChieuId)).ToList(), ct); }
        catch (Exception e) when (e is not OperationCanceledException) { } // đã lưu hộp thư, đẩy hỏng không sao
    }
}
