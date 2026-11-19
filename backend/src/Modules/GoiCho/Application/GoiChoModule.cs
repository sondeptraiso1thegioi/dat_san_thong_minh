using NhaXe.Modules.GoiCho.Contracts;
using NhaXe.Modules.GoiCho.Domain;
using NhaXe.Modules.NhomGiaDinh.Contracts;

namespace NhaXe.Modules.GoiCho.Application;

/// <summary>Cài đặt cổng công khai IGoiChoModule. Module khác chỉ thấy interface, không thấy class này.</summary>
public class GoiChoModule : IGoiChoModule
{
    private readonly ISucChuaQuery _sucChua;
    private readonly IXeQuery _xe;
    private readonly IXeRepository _xeRepo;
    private readonly TraCuuTrongBai _trongBai;
    private readonly INhomGiaDinhModule _nhom;

    private readonly IDoanhThuQuery? _doanhThu;

    public GoiChoModule(ISucChuaQuery sucChua, IXeQuery xe, IXeRepository xeRepo, TraCuuTrongBai trongBai, INhomGiaDinhModule nhom,
                        IDoanhThuQuery? doanhThu = null)
    { _sucChua = sucChua; _xe = xe; _xeRepo = xeRepo; _trongBai = trongBai; _nhom = nhom; _doanhThu = doanhThu; }

    public Task<DoanhThu> LayDoanhThuAsync(DateTime tu, DateTime den, CancellationToken ct = default) =>
        _doanhThu?.LayAsync(tu, den, ct) ?? Task.FromResult(new DoanhThu(0, 0, 0, 0));

    public Task<IReadOnlyList<SucChuaLoaiXe>> LaySucChuaAsync(CancellationToken ct = default) => _sucChua.LaySucChuaAsync(ct);

    public async Task<IReadOnlyDictionary<int, TinhTrangXeGoi>> LayTinhTrangXeAsync(IEnumerable<int> xeIds, CancellationToken ct = default)
    {
        var ids = xeIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, TinhTrangXeGoi>();
        var ds = await _xe.LayTinhTrangXeAsync(ids, ct);
        var trongBai = await _trongBai.LayAsync(ids, ct); // nợ chỉ có khi xe đang trong bãi
        return ds.ToDictionary(r => r.XeId, r =>
        {
            var t = trongBai.GetValueOrDefault(r.XeId);
            return new TinhTrangXeGoi(r.XeId, r.BienSo, r.LoaiXeId, r.LoaiXe, r.ChuXeId, r.HetHanLuc, t?.SoNgayNo ?? 0, t?.SoTienNo ?? 0);
        });
    }

    public async Task<XeTaiCong?> TimXeTaiCongAsync(string bienSo, CancellationToken ct = default)
    {
        var bs = BienSo.Tao(bienSo); // sai định dạng → LoiNghiepVu DuLieuKhongHopLe
        var r = await _xe.TimXeTaiCongAsync(bs.GiaTri, ct);
        if (r is null) return null;
        var no = (await _trongBai.LayAsync(new[] { r.XeId }, ct)).GetValueOrDefault(r.XeId);
        return new XeTaiCong(r.XeId, r.BienSo, r.LoaiXe, r.ChuXeId, r.TrangThaiDuyet == TrangThaiDuyet.DaDuyet,
                             r.CoGoiHieuLuc, r.HetHanLuc, no?.SoTienNo ?? 0);
    }

    public async Task<KetQuaKhopGanDung> TimXeGanDungAsync(string bienSo, IReadOnlyCollection<int>? chiTrongXeIds = null,
                                                           CancellationToken ct = default)
    {
        var bs = (bienSo ?? "").Trim().ToUpperInvariant().Replace("-", "").Replace(".", "").Replace(" ", "");
        if (bs.Length == 0) return new(null, Array.Empty<string>());
        IEnumerable<XeQuanLyDto> ds = await _xe.LayXeDaDuyetAsync(ct);
        if (chiTrongXeIds is not null) { var set = chiTrongXeIds.ToHashSet(); ds = ds.Where(x => set.Contains(x.XeId)); }
        var (khop, _, ungVien) = NhaXe.SharedKernel.KhopBienSo.Tim(bs, ds, x => x.BienSo);
        if (khop is null) return new(null, ungVien.Select(x => x.BienSo).ToList());
        return new(await TimXeTaiCongAsync(khop.BienSo, ct), new[] { khop.BienSo });
    }

    public async Task<decimal> LaySoTienNoAsync(int xeId, CancellationToken ct = default) =>
        (await _trongBai.LayAsync(new[] { xeId }, ct)).GetValueOrDefault(xeId)?.SoTienNo ?? 0;

    public async Task<IReadOnlyList<int>> LayNguoiDungXeAsync(int xeId, CancellationToken ct = default)
    {
        var xe = await _xeRepo.TimAsync(xeId, ct);
        if (xe is null) return Array.Empty<int>();
        var cungNhom = xe.ChiaSeNhom ? await _nhom.LayNguoiCungNhomAsync(xe.ChuXeId, ct) : Array.Empty<int>();
        return NguoiDungXe.Tinh(xe.ChuXeId, xe.ChiaSeNhom, cungNhom);
    }

    public async Task<IReadOnlyList<XeTomTat>> LayXeDuocDungAsync(int taiKhoanId, CancellationToken ct = default)
    {
        var cungNhom = await _nhom.LayNguoiCungNhomAsync(taiKhoanId, ct);
        return (await _xe.LayXeDuocDungAsync(taiKhoanId, cungNhom, ct))
            .Select(x => new XeTomTat(x.XeId, x.BienSo, x.LoaiXeId, x.ChuXeId)).ToList();
    }

    public async Task<IReadOnlyList<XeTomTat>> LayXeDaDuyetAsync(CancellationToken ct = default) =>
        (await _xe.LayXeDaDuyetAsync(ct)).Select(x => new XeTomTat(x.XeId, x.BienSo, x.LoaiXeId, x.ChuXeId)).ToList();
}
