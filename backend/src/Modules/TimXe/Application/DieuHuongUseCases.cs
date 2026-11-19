using NhaXe.Modules.BaiXe.Contracts;
using NhaXe.Modules.CongRaVao.Contracts;
using NhaXe.Modules.GoiCho.Contracts;
using NhaXe.Modules.TimXe.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.TimXe.Application;

// "Sơ đồ động & điều hướng trong bãi": MỞ RỘNG luồng chọn khu + giữ suất của TimXe (không phải module mới).
// Phiên điều hướng = suất giữ (giu_cho_khu). Bắt đầu khi xe máy qua cổng (NgheCongTimXe), kết thúc khi camera VÀO của khu
// thấy xe (XuLyCameraKhuUseCase), hết giờ giữ (HetHanGiuChoUseCase) hoặc xe ra cổng. Không dùng GPS.

public record DiemBanDoDto(double X, double Y, string? Ten);
/// <summary>Đường đi, toạ độ theo tỉ lệ ảnh sơ đồ (0..1) để app vẽ đè lên ảnh.</summary>
public record DuongDiBanDoDto(int DenKhuId, double TongMet, IReadOnlyList<DiemBanDoDto> Diem);
public record KhuThucTeDto(int Id, string Ten, DateTime VaoLuc);
public record CanhBaoKhuDto(int KhuId, string Ten, DateTime Luc);
public record PhienDieuHuongDto(int Id, int LuotGuiId, int XeId, string BienSo, string TrangThai, DateTime BatDauLuc, DateTime HetHanLuc,
                                KhuChoDto KhuDich, int KhuGoiYId, KhuThucTeDto? KhuThucTe, DuongDiBanDoDto? Duong, CanhBaoKhuDto? CanhBao);
public record HetChoDto(string BienSo, DateTime Luc);
public record DieuHuongCuaToiDto(DateTime BayGio, PhienDieuHuongDto? Phien, HetChoDto? HetCho);
public record DoiKhuDichLenh(int KhuId);

internal static class DuongBanDo
{
    public static DuongDiBanDoDto Tu(DuongDiDto d) =>
        new(d.KhuId, d.TongMet, d.Diem.Select(p => new DiemBanDoDto(p.X, p.Y, string.IsNullOrEmpty(p.Ten) ? null : p.Ten)).ToList());
}

/// <summary>Đường từ Cổng vào X tới khu, chỉ đọc (không tạo phiên, không giữ suất). Cùng đồ thị + Dijkstra với chọn khu.</summary>
public class LayDuongDiUseCase(IBaiXeModule baiXe)
{
    public async Task<DuongDiBanDoDto> ThucHienAsync(int denKhuId, CancellationToken ct = default) =>
        await baiXe.TimDuongAsync(denKhuId, ct) is { } d ? DuongBanDo.Tu(d)
            : throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_CO_DUONG_DI", "Khu này chưa có đường đi trên sơ đồ.");
}

/// <summary>
/// Phiên điều hướng gần nhất của các xe máy tài khoản được dùng (đang trong bãi), kèm đường đi khi đang dẫn.
/// hetCho: xe máy vừa qua cổng (≤ 10 phút) mà không có suất nào (mọi khu đã đầy lúc vào) để app báo "Bãi xe máy đã hết chỗ".
/// </summary>
public class LayDieuHuongCuaToiUseCase(IGoiChoModule goiCho, ICongRaVaoModule cong, IBaiXeModule baiXe, ITimXeRepository repo,
                                       LayMucChoUseCase mucCho, IClock clock)
{
    private static readonly TimeSpan HetChoTrong = TimeSpan.FromMinutes(10);

    public async Task<DieuHuongCuaToiDto> ThucHienAsync(int taiKhoanId, CancellationToken ct = default)
    {
        var bayGio = clock.BayGio;
        var xe = await goiCho.LayXeDuocDungAsync(taiKhoanId, ct);
        var tt = await goiCho.LayTinhTrangXeAsync(xe.Select(x => x.XeId), ct);
        var xeMay = tt.Values.Where(t => t.LoaiXe == LoaiXeMa.XeMay).ToDictionary(t => t.XeId);
        if (xeMay.Count == 0) return new(bayGio, null, null);
        var luot = (await cong.LayLuotGuiDangMoCuaXeAsync(xeMay.Keys, ct)).Values.ToList();
        var giu = await repo.LayGiuGanNhatAsync(luot.Select(l => l.LuotGuiId).ToList(), ct);

        var g = giu.Values.OrderByDescending(x => x.BatDauLuc).ThenByDescending(x => x.Id).FirstOrDefault();
        PhienDieuHuongDto? phien = null;
        if (g is not null)
        {
            var l = luot.First(x => x.LuotGuiId == g.LuotGuiId);
            phien = await TaoDtoAsync(g, l.XeId, xeMay[l.XeId].BienSo, bayGio, ct);
        }
        var het = luot.Where(l => !giu.ContainsKey(l.LuotGuiId) && l.ViTriKhuId is null && bayGio - l.ThoiGianVao <= HetChoTrong)
                      .OrderByDescending(l => l.ThoiGianVao).FirstOrDefault();
        return new(bayGio, phien, het is null ? null : new HetChoDto(xeMay[het.XeId].BienSo, het.ThoiGianVao));
    }

    internal async Task<PhienDieuHuongDto> TaoDtoAsync(GiuChoKhu g, int xeId, string bienSo, DateTime bayGio, CancellationToken ct)
    {
        var khu = (await mucCho.ThucHienAsync(false, ct: ct)).ToDictionary(k => k.Id);
        var trangThai = TrangThaiPhien.Tu(g, bayGio);
        KhuChoDto KhuCho(int id) => khu.GetValueOrDefault(id) ?? new KhuChoDto(id, $"Khu #{id}", 0, 0, 0, 0, 0, MucCho.Day, null);
        var dangDan = trangThai == TrangThaiPhien.DangDan;
        var duong = dangDan && await baiXe.TimDuongAsync(g.KhuId, ct) is { } d ? DuongBanDo.Tu(d) : null;
        return new PhienDieuHuongDto(g.Id, g.LuotGuiId, xeId, bienSo, trangThai, g.BatDauLuc, g.HetHanLuc, KhuCho(g.KhuId), g.KhuGoiYId ?? g.KhuId,
            g.KhuThucTeId is int t && g.KetThucLuc is DateTime luc ? new KhuThucTeDto(t, KhuCho(t).Ten, luc) : null,
            duong,
            dangDan && g.CanhBaoKhuId is int c && g.CanhBaoLuc is DateTime cl ? new CanhBaoKhuDto(c, KhuCho(c).Ten, cl) : null);
    }
}

/// <summary>
/// Người dùng tự chọn khu khác khi đang được dẫn đường (khu hệ thống chọn chỉ là GỢI Ý). Khu mới phải là khu xe máy đang dùng
/// và còn chỗ thực tế (đã trừ suất đang giữ). Suất giữ CHUYỂN sang khu mới, GIỮ NGUYÊN thời hạn còn lại.
/// Khu mới là khu đích chính thức: camera VÀO của khu đó thấy xe thì phiên thành DA_TOI.
/// </summary>
public class DoiKhuDichUseCase(IGoiChoModule goiCho, ICongRaVaoModule cong, ITimXeRepository repo, LayMucChoUseCase mucCho,
                               LayDieuHuongCuaToiUseCase dieuHuong, IClock clock)
{
    public async Task<PhienDieuHuongDto> ThucHienAsync(int taiKhoanId, int khuId, CancellationToken ct = default)
    {
        var bayGio = clock.BayGio;
        var xe = await goiCho.LayXeDuocDungAsync(taiKhoanId, ct);
        var luot = await cong.LayLuotGuiDangMoCuaXeAsync(xe.Select(x => x.XeId), ct);
        GiuChoKhu? g = null; LuotGuiDangMo? l = null;
        foreach (var x in luot.Values.OrderByDescending(v => v.ThoiGianVao))
            if (await repo.TimGiuDangGiuAsync(x.LuotGuiId, ct) is { } c && c.ConGiu(bayGio)) { g = c; l = x; break; }
        if (g is null || l is null)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "PHIEN_DA_KET_THUC", "Không có phiên dẫn đường nào đang chạy để đổi khu.");

        var tt = (await goiCho.LayTinhTrangXeAsync(new[] { l.XeId }, ct))[l.XeId];
        var khu = (await mucCho.ThucHienAsync(false, ct: ct)).FirstOrDefault(k => k.Id == khuId)
                  ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_TIM_THAY_KHU", "Không tìm thấy khu.");
        if (khu.LoaiXeId != tt.LoaiXeId || !khu.DangDung)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "SAI_LOAI_KHU", $"{khu.Ten} không nhận xe máy lúc này.");
        if (khuId != g.KhuId)
        {
            if (khu.ConLai <= 0)
                throw new LoiNghiepVu(LoaiLoi.XungDot, "KHU_DA_DAY", $"{khu.Ten} đã đầy, hãy chọn khu khác.");
            g.DoiKhuDich(khuId, bayGio);
            await repo.LuuAsync(ct);
        }
        return await dieuHuong.TaoDtoAsync(g, l.XeId, tt.BienSo, bayGio, ct);
    }
}

public record DiSaiKhuDto(long Id, string BienSo, int ChuXeId, string? KhuChiDinh, string KhuThucTe, bool KhuDay, DateTime Luc);
/// <summary>TenChuXe: tầng API ghép (TimXe không hỏi module Tài khoản).</summary>
public record DemDiSaiDto(int ChuXeId, int SoLan, int SoLanKhuDay, DateTime LanCuoi);
public record DiSaiKhuAdminDto(IReadOnlyList<DiSaiKhuDto> GanDay, IReadOnlyList<DemDiSaiDto> TheoChuXe);

/// <summary>Admin (trang Quản lý bãi): các lần đi sai khu gần đây (cảnh báo) và ai hay đi sai. Chưa phạt tự động.</summary>
public class LayDiSaiKhuUseCase(ITimXeRepository repo, IBaiXeModule baiXe)
{
    public async Task<DiSaiKhuAdminDto> ThucHienAsync(int soDong = 50, CancellationToken ct = default)
    {
        var khu = (await baiXe.LayKhuAsync(ct)).ToDictionary(k => k.Id, k => k.Ten);
        string Ten(int id) => khu.GetValueOrDefault(id) ?? $"Khu #{id}";
        var gan = (await repo.LayDiSaiGanDayAsync(Math.Clamp(soDong, 1, 200), ct))
            .Select(d => new DiSaiKhuDto(d.Id, d.BienSo, d.ChuXeId, d.KhuChiDinhId is int c ? Ten(c) : null, Ten(d.KhuThucTeId), d.KhuDay, d.Luc))
            .ToList();
        var dem = (await repo.DemDiSaiTheoChuXeAsync(ct)).Select(d => new DemDiSaiDto(d.ChuXeId, d.SoLan, d.SoLanKhuDay, d.LanCuoi)).ToList();
        return new DiSaiKhuAdminDto(gan, dem);
    }
}
