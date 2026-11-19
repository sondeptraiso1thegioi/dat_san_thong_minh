using NhaXe.Modules.CongRaVao.Contracts;
using NhaXe.Modules.GoiCho.Domain;
using NhaXe.Modules.NhomGiaDinh.Contracts;
using NhaXe.Modules.ThongBao.Contracts;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.GoiCho.Application;

// ---------------------------------------------------------------------------------------------
// 1. GoiCho NGHE sự kiện của CongRaVao để tự giữ bản sao "xe nào đang trong bãi" (bảng xe_trong_bai).
//    Nhờ vậy GoiCho không gọi ICongRaVaoModule: chiều gọi giữa hai module chỉ còn CongRaVao → GoiCho.
// ---------------------------------------------------------------------------------------------

/// <summary>Bản sao "xe đang trong bãi" của GoiCho. Chỉ GoiCho đọc / ghi bảng xe_trong_bai.</summary>
public interface IXeTrongBaiStore
{
    /// <summary>xeId → id lượt gửi đang mở. Xe không có trong kết quả = không ở trong bãi.</summary>
    Task<IReadOnlyDictionary<int, int>> LayAsync(IReadOnlyCollection<int> xeIds, CancellationToken ct);
    /// <summary>Ghi "xe đã vào". Gọi lại lần hai với cùng lượt gửi thì không đổi gì (idempotent).</summary>
    Task GhiVaoAsync(int xeId, int luotGuiId, DateTime vaoLuc, CancellationToken ct);
    /// <summary>Xoá "xe đang trong bãi" của đúng lượt gửi đó. Không có thì thôi.</summary>
    Task GhiRaAsync(int xeId, int luotGuiId, CancellationToken ct);
}

public class NgheXeVaoRa : INghe<XeDaVao>, INghe<XeDaRa>
{
    private readonly IXeTrongBaiStore _store;
    public NgheXeVaoRa(IXeTrongBaiStore store) => _store = store;

    public Task XuLyAsync(XeDaVao e, CancellationToken ct) => _store.GhiVaoAsync(e.XeId, e.LuotGuiId, e.Luc, ct);
    public Task XuLyAsync(XeDaRa e, CancellationToken ct) => _store.GhiRaAsync(e.XeId, e.LuotGuiId, ct);
}

// ---------------------------------------------------------------------------------------------
// 2. Nhắc gia hạn trước 72 giờ. Nằm ở GoiCho vì GoiCho biết hạn gói; ThongBao chỉ lo gửi.
//    (Nếu đặt ở ThongBao thì ThongBao phải hỏi GoiCho, trong khi GoiCho đã gọi ThongBao → vòng phụ thuộc.)
// ---------------------------------------------------------------------------------------------

public interface INhacHanStore
{
    /// <summary>
    /// Tìm các gói còn hạn sẽ hết trong vòng <paramref name="soGio"/> giờ, chưa nhắc, không có gói gia hạn nối tiếp;
    /// ĐÁNH DẤU đã nhắc và trả về đúng các gói vừa đánh dấu, trong MỘT câu lệnh UPDATE.
    /// Chạy 2 instance API cùng lúc thì mỗi gói chỉ một instance lấy được, nên không nhắc trùng.
    /// </summary>
    Task<IReadOnlyList<GoiCanNhacRow>> DanhDauGoiCanNhacAsync(int soGio, CancellationToken ct);

    /// <summary>Gói vừa hết hạn (trong 7 ngày qua), chưa nhắc "đã hết hạn", không có gói nối tiếp: đánh dấu và trả về.</summary>
    Task<IReadOnlyList<GoiCanNhacRow>> DanhDauGoiVuaHetHanAsync(CancellationToken ct);

    /// <summary>Xe đang trong bãi mà gói mới nhất đã hết hạn, hôm nay chưa nhắc phí: đánh dấu ngày nhắc và trả về.</summary>
    Task<IReadOnlyList<GoiCanNhacRow>> DanhDauNhacQuaHanHomNayAsync(CancellationToken ct);
}

public class NhacSapHetHanUseCase
{
    public const string KhoaCauHinh = "nhac_truoc_het_han_gio";
    private readonly INhacHanStore _store;
    private readonly ICauHinh _cauHinh;
    private readonly INhomGiaDinhModule _nhom;
    private readonly IThongBaoModule _thongBao;

    private readonly TraCuuTrongBai? _trongBai;

    public NhacSapHetHanUseCase(INhacHanStore store, ICauHinh cauHinh, INhomGiaDinhModule nhom, IThongBaoModule thongBao,
                                TraCuuTrongBai? trongBai = null)
    { _store = store; _cauHinh = cauHinh; _nhom = nhom; _thongBao = thongBao; _trongBai = trongBai; }

    /// <returns>Số gói đã nhắc trong lần chạy này.</returns>
    public async Task<int> ThucHienAsync(CancellationToken ct = default)
    {
        var soGio = await _cauHinh.LaySoNguyenAsync(KhoaCauHinh, 72, ct);
        var ds = await _store.DanhDauGoiCanNhacAsync(soGio, ct);
        foreach (var g in ds)
        {
            var nguoiNhan = NguoiDungXe.Tinh(g.ChuXeId, g.ChiaSeNhom,
                g.ChiaSeNhom ? await _nhom.LayNguoiCungNhomAsync(g.ChuXeId, ct) : Array.Empty<int>());
            await _thongBao.GuiNhieuAsync(nguoiNhan, LoaiThongBao.SapHetHan, NoiDungThongBao.SapHetHan(g.TenGoi, g.BienSo, g.HetHanLuc),
                new DichThongBao(HanhDongThongBao.MuaGoi, g.XeId), ct);
        }
        return ds.Count + await NhacHetHanVaQuaHanAsync(ct);
    }

    /// <summary>Đề cương v10: nhắc lúc gói hết hạn (kèm mức phí nếu xe đang trong bãi) và mỗi ngày quá hạn.</summary>
    private async Task<int> NhacHetHanVaQuaHanAsync(CancellationToken ct)
    {
        var hetHan = await _store.DanhDauGoiVuaHetHanAsync(ct);
        var quaHan = await _store.DanhDauNhacQuaHanHomNayAsync(ct);
        var ids = hetHan.Concat(quaHan).Select(g => g.XeId).Distinct().ToList();
        var trongBai = _trongBai is null || ids.Count == 0
            ? new Dictionary<int, TinhTrangTrongBai>() : await _trongBai.LayAsync(ids, ct);
        foreach (var g in hetHan)
        {
            var no = trongBai.GetValueOrDefault(g.XeId);
            await _thongBao.GuiNhieuAsync(await NguoiNhanAsync(g, ct), LoaiThongBao.HetHan,
                NoiDungThongBao.DaHetHan(g.TenGoi, g.BienSo, no is null ? null : no.SoTienNo),
                new DichThongBao(HanhDongThongBao.MuaGoi, g.XeId), ct);
        }
        foreach (var g in quaHan)
        {
            var no = trongBai.GetValueOrDefault(g.XeId);
            if (no is null || no.SoTienNo <= 0) continue;
            await _thongBao.GuiNhieuAsync(await NguoiNhanAsync(g, ct), LoaiThongBao.QuaHan,
                NoiDungThongBao.NhacQuaHan(g.BienSo, no.SoNgayNo, no.SoTienNo),
                new DichThongBao(HanhDongThongBao.TraPhi, g.XeId), ct);
        }
        return hetHan.Count + quaHan.Count;
    }

    private async Task<IReadOnlyList<int>> NguoiNhanAsync(GoiCanNhacRow g, CancellationToken ct) =>
        NguoiDungXe.Tinh(g.ChuXeId, g.ChiaSeNhom,
            g.ChiaSeNhom ? await _nhom.LayNguoiCungNhomAsync(g.ChuXeId, ct) : Array.Empty<int>());
}
