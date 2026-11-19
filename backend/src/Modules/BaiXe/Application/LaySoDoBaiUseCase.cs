using NhaXe.Modules.BaiXe.Domain;
using NhaXe.Modules.CongRaVao.Contracts;
using NhaXe.Modules.GoiCho.Contracts;
using NhaXe.Modules.TaiKhoan.Contracts;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.BaiXe.Application;

/// <summary>
/// Ghép sơ đồ bãi: khu → các ô → xe đang đỗ ở ô đó. Mỗi phần dữ liệu hỏi đúng module sở hữu nó (qua Contracts):
///   - khu, ô:                         IBaiXeQuery (module này)
///   - xe nào đang trong bãi, ở đâu:   ICongRaVaoModule (bảng luot_gui)
///   - sức chứa, biển số, hạn gói, nợ: IGoiChoModule
///   - họ tên chủ xe:                  ITaiKhoanModule
/// Phần ghép nằm ở đây (không nằm trong SQL) nên unit test được bằng dữ liệu giả.
/// </summary>
public class LaySoDoBaiUseCase
{
    private readonly IBaiXeQuery _query;
    private readonly ICongRaVaoModule _congRaVao;
    private readonly IGoiChoModule _goiCho;
    private readonly ITaiKhoanModule _taiKhoan;
    private readonly IClock _clock;
    private readonly ICauHinhBaiQuery _trangThai;

    public LaySoDoBaiUseCase(IBaiXeQuery query, ICongRaVaoModule congRaVao, IGoiChoModule goiCho, ITaiKhoanModule taiKhoan, IClock clock,
                             ICauHinhBaiQuery trangThai)
    { _query = query; _congRaVao = congRaVao; _goiCho = goiCho; _taiKhoan = taiKhoan; _clock = clock; _trangThai = trangThai; }

    public async Task<SoDoBaiDto> ThucHienAsync(CancellationToken ct = default)
    {
        var khu = await _query.LayKhuAsync(ct);
        var o = await _query.LayODoAsync(ct);
        var luot = await _congRaVao.LayLuotGuiDangMoAsync(ct);
        var sucChuaGoiCho = await _goiCho.LaySucChuaAsync(ct);
        var thongTinXe = await _goiCho.LayTinhTrangXeAsync(luot.Select(v => v.XeId), ct);
        var ten = await _taiKhoan.LayHoTenAsync(thongTinXe.Values.Select(x => x.ChuXeId), ct);
        var bayGio = _clock.BayGio;
        var trangThai = (await _trangThai.LayTrangThaiOAsync(ct)).ToDictionary(t => t.OId);

        var tenKhu = khu.ToDictionary(k => k.Id, k => k.Ten);
        var maO = o.ToDictionary(od => od.Id, od => od.MaO);
        var tenLoaiXe = sucChuaGoiCho.ToDictionary(s => s.LoaiXeId, s => s.LoaiXe);
        var loaiCuaKhu = khu.ToDictionary(k => k.Id, k => k.LoaiXeId);

        var xe = luot.Select(v =>
        {
            var t = thongTinXe.GetValueOrDefault(v.XeId);
            return new XeTrongBaiDto
            {
                LuotGuiId = v.LuotGuiId, XeId = v.XeId, ThoiGianVao = v.ThoiGianVao,
                KhuId = v.ViTriKhuId, Khu = v.ViTriKhuId is int k ? tenKhu.GetValueOrDefault(k) : null,
                OId = v.ViTriOId, MaO = v.ViTriOId is int oi ? maO.GetValueOrDefault(oi) : null, ViTriLuc = v.ViTriLuc,
                BienSo = t?.BienSo ?? "?", LoaiXeId = t?.LoaiXeId ?? 0, LoaiXe = t?.LoaiXe ?? "",
                ChuXeId = t?.ChuXeId ?? 0, TenChuXe = t is null ? "?" : ten.GetValueOrDefault(t.ChuXeId, "?"),
                HetHanLuc = t?.HetHanLuc, SoNgayNo = t?.SoNgayNo ?? 0, SoTienNo = t?.SoTienNo ?? 0,
                TinhTrangGoi = TinhTrangGoiTrenSoDo.Tinh(t?.HetHanLuc, bayGio),
            };
        }).ToList();

        var sucChua = sucChuaGoiCho.Select(s => new SucChuaBaiDto(
            s.LoaiXeId, s.LoaiXe, s.SucChua, s.SoXeGiuSuat, s.SoXeQuaHanTrongBai, s.SoChoConBan,
            SoXeTrongBai: xe.Count(x => x.LoaiXeId == s.LoaiXeId),
            SoODo: o.Count(od => loaiCuaKhu.GetValueOrDefault(od.KhuId) == s.LoaiXeId))).ToList();

        var dsKhu = khu.OrderBy(k => k.Ten).Select(k =>
        {
            var xeTrongKhu = xe.Where(x => x.KhuId == k.Id).ToList();
            var dsO = o.Where(od => od.KhuId == k.Id).OrderBy(od => od.MaO)
                       .Select(od => new ODoDto(od.Id, od.MaO, xeTrongKhu.FirstOrDefault(x => x.OId == od.Id),
                                                TrangThaiO.HienThi(trangThai.GetValueOrDefault(od.Id), bayGio))).ToList();
            var chuaRoO = xeTrongKhu.Where(x => x.OId is null).ToList();
            return new KhuDto(k.Id, k.Ten, k.LoaiXeId, tenLoaiXe.GetValueOrDefault(k.LoaiXeId, ""), dsO.Count,
                              dsO.Count(d => d.Xe is null), xeTrongKhu.Count, dsO, chuaRoO);
        }).ToList();

        return new SoDoBaiDto(bayGio, sucChua, dsKhu, xe.Where(x => x.KhuId is null).ToList(),
                              xe.OrderBy(x => x.Khu ?? "~").ThenBy(x => x.MaO).ToList());
    }
}
