using NhaXe.Modules.CongRaVao.Domain;
using NhaXe.Modules.GoiCho.Contracts;
using NhaXe.Modules.TaiKhoan.Contracts;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.CongRaVao.Application;

/// <summary>Ghép yêu cầu đang chờ với biển số (hỏi GoiCho) và họ tên (hỏi TaiKhoan). Dùng chung cho hai màn hình.</summary>
public class GhepYeuCau
{
    private readonly IGoiChoModule _goiCho;
    private readonly ITaiKhoanModule _taiKhoan;
    public GhepYeuCau(IGoiChoModule goiCho, ITaiKhoanModule taiKhoan) { _goiCho = goiCho; _taiKhoan = taiKhoan; }

    public async Task<IReadOnlyList<YeuCauDto>> ThucHienAsync(IReadOnlyList<(YeuCauXacNhan YeuCau, int XeId)> ds, DateTime bayGio, CancellationToken ct)
    {
        if (ds.Count == 0) return Array.Empty<YeuCauDto>();
        var xe = await _goiCho.LayTinhTrangXeAsync(ds.Select(d => d.XeId), ct);
        var ids = ds.SelectMany(d => new[] { d.YeuCau.ChuXeTraLoiId, d.YeuCau.QuanLyId }).OfType<int>()
                    .Concat(xe.Values.Select(x => x.ChuXeId));
        var ten = await _taiKhoan.LayHoTenAsync(ids.Distinct(), ct);
        string? Ten(int? id) => id is int i ? ten.GetValueOrDefault(i, $"#{i}") : null;

        return ds.OrderBy(d => d.YeuCau.HetHanLuc).Select(d =>
        {
            var y = d.YeuCau;
            var x = xe.GetValueOrDefault(d.XeId);
            return new YeuCauDto(y.Id, y.LuotGuiId, d.XeId, x?.BienSo ?? $"#{d.XeId}", x?.ChuXeId ?? 0, Ten(x?.ChuXeId) ?? "",
                                 y.TaoLuc, y.HetHanLuc, y.SoGiayConLai(bayGio),
                                 y.ChuXeDongY, Ten(y.ChuXeTraLoiId), y.QuanLyDongY, Ten(y.QuanLyId));
        }).ToList();
    }
}

/// <summary>Màn hình cổng của quản lý (trang gọi lại mỗi 2 giây).</summary>
public class LayManHinhCongUseCase
{
    private readonly ICongQuery _q;
    private readonly ILuotGuiQuery _luot;
    private readonly IGoiChoModule _goiCho;
    private readonly GhepYeuCau _ghep;
    private readonly IClock _clock;

    public LayManHinhCongUseCase(ICongQuery q, ILuotGuiQuery luot, IGoiChoModule goiCho, GhepYeuCau ghep, IClock clock)
    { _q = q; _luot = luot; _goiCho = goiCho; _ghep = ghep; _clock = clock; }

    public async Task<ManHinhCongDto> ThucHienAsync(CancellationToken ct = default)
    {
        var bayGio = _clock.BayGio;
        var dangCho = await _ghep.ThucHienAsync(await _q.LayYeuCauDangChoAsync(null, ct), bayGio, ct);
        var ganDay = (await _q.LaySuKienGanDayAsync(30, ct))
            .Select(s => new SuKienCongDto(s.Id, s.Loai, s.BienSo, s.DoTinCay, s.KetQua, s.LyDo, s.ThoiGian)).ToList();
        var trongBai = (await _luot.LayDangMoAsync(null, ct)).Select(l => l.XeId).ToHashSet();
        var xe = (await _goiCho.LayXeDaDuyetAsync(ct)).OrderBy(x => x.BienSo)
            .Select(x => new XeChonNhanhDto(x.XeId, x.BienSo, trongBai.Contains(x.XeId))).ToList();
        var canXuLy = await _q.LayLuotCanXuLyAsync(ct);
        var bien = canXuLy.Count == 0 ? new Dictionary<int, TinhTrangXeGoi>()
            : await _goiCho.LayTinhTrangXeAsync(canXuLy.Select(c => c.Luot.XeId), ct);
        var dsXuLy = canXuLy.Select(c => new LuotCanXuLyDto(c.Luot.Id, c.Luot.XeId,
            bien.GetValueOrDefault(c.Luot.XeId)?.BienSo ?? $"#{c.Luot.XeId}", c.Luot.ThoiGianVao, c.Luot.TrangThai, c.LyDo)).ToList();
        return new ManHinhCongDto(bayGio, dangCho, ganDay, xe, dsXuLy);
    }
}

/// <summary>Trang "Ra vào" của khách: yêu cầu cần mình xác nhận + lịch sử của các xe mình được dùng.</summary>
public class LayRaVaoCuaToiUseCase
{
    private readonly ICongQuery _q;
    private readonly IGoiChoModule _goiCho;
    private readonly GhepYeuCau _ghep;
    private readonly IClock _clock;

    public LayRaVaoCuaToiUseCase(ICongQuery q, IGoiChoModule goiCho, GhepYeuCau ghep, IClock clock)
    { _q = q; _goiCho = goiCho; _ghep = ghep; _clock = clock; }

    public async Task<RaVaoCuaToiDto> ThucHienAsync(int taiKhoanId, CancellationToken ct = default)
    {
        var bayGio = _clock.BayGio;
        var xe = (await _goiCho.LayXeDuocDungAsync(taiKhoanId, ct)).ToDictionary(x => x.XeId);
        if (xe.Count == 0) return new RaVaoCuaToiDto(bayGio, Array.Empty<YeuCauDto>(), Array.Empty<LuotGuiDto>());
        var ids = xe.Keys.ToList();
        var dangCho = await _ghep.ThucHienAsync(await _q.LayYeuCauDangChoAsync(ids, ct), bayGio, ct);
        var luot = await _q.LayLuotCuaXeAsync(ids, 30, ct);
        // lượt đang chờ thanh toán: kèm số tiền để trang Ra vào hiện nút "Trả phí"
        var phi = new Dictionary<int, decimal>();
        foreach (var l in luot.Where(l => l.TrangThai == TrangThaiLuot.ChoThanhToan))
            phi[l.Id] = await _goiCho.LaySoTienNoAsync(l.XeId, ct);
        var yc = await _q.LayYeuCauGanNhatAsync(luot.Select(l => l.Id).ToList(), ct);
        var lichSu = luot.Select(l => new LuotGuiDto(l.Id, l.XeId, xe[l.XeId].BienSo, l.ThoiGianVao, l.ThoiGianRa, l.TrangThai,
                                                     phi.TryGetValue(l.Id, out var p) ? p : null,
                                                     yc.GetValueOrDefault(l.Id)?.KetQua, yc.GetValueOrDefault(l.Id)?.HetHanLuc)).ToList();
        return new RaVaoCuaToiDto(bayGio, dangCho, lichSu);
    }
}
