using NhaXe.Modules.GoiCho.Domain;
using NhaXe.Modules.TaiKhoan.Contracts;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.GoiCho.Application;

// Trang "Quản lý xe" của Admin: đếm xe chờ duyệt (badge), tìm / lọc / phân trang xe đã duyệt, chi tiết một xe.

/// <summary>Bộ lọc nhanh tab "Xe đã duyệt".</summary>
public static class LocXeDaDuyet
{
    public const string TatCa = "TAT_CA";
    public const string ConHan = "CON_HAN";
    /// <summary>Còn tối đa 72 giờ.</summary>
    public const string SapHet = "SAP_HET";
    /// <summary>Hết hạn hoặc chưa từng mua gói.</summary>
    public const string ChuaCo = "CHUA_CO";
    public const string TrongBai = "TRONG_BAI";
    public static readonly IReadOnlySet<string> HopLe = new HashSet<string> { TatCa, ConHan, SapHet, ChuaCo, TrongBai };
}

public record TrangXeDaDuyetDto(int Tong, int Trang, int SoDong, IReadOnlyList<XeQuanLyDto> Ds);

/// <summary>Tab "Xe đã duyệt": tìm biển số (chuẩn hoá rồi tìm "chứa"), lọc nhanh, phân trang ở DB.</summary>
public class TimXeDaDuyetUseCase
{
    public const int SoDongMacDinh = 20, SoDongToiDa = 100;
    private readonly IXeQuery _query;
    private readonly ITaiKhoanModule _taiKhoan;
    private readonly TraCuuTrongBai _trongBai;
    public TimXeDaDuyetUseCase(IXeQuery query, ITaiKhoanModule taiKhoan, TraCuuTrongBai trongBai)
    { _query = query; _taiKhoan = taiKhoan; _trongBai = trongBai; }

    public async Task<TrangXeDaDuyetDto> ThucHienAsync(string? q, string? loc, int trang = 1, int soDong = SoDongMacDinh, CancellationToken ct = default)
    {
        var l = string.IsNullOrWhiteSpace(loc) ? LocXeDaDuyet.TatCa : loc.Trim().ToUpperInvariant();
        if (!LocXeDaDuyet.HopLe.Contains(l))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "BO_LOC_SAI", "Bộ lọc là TAT_CA, CON_HAN, SAP_HET, CHUA_CO hoặc TRONG_BAI.");
        var sd = Math.Clamp(soDong, 1, SoDongToiDa);
        var tr = Math.Max(1, trang);
        var (tong, ds) = await _query.TimXeDaDuyetAsync(BienSo.ChuanHoaTim(q), l, (tr - 1) * sd, sd, ct);
        var ten = await _taiKhoan.LayHoTenAsync(ds.Select(x => x.ChuXeId), ct);
        var trongBai = await _trongBai.LayAsync(ds.Select(x => x.XeId), ct);
        foreach (var x in ds)
        {
            x.TenChuXe = ten.GetValueOrDefault(x.ChuXeId, "?");
            x.DangTrongBai = trongBai.ContainsKey(x.XeId);
        }
        return new TrangXeDaDuyetDto(tong, tr, sd, ds);
    }
}

/// <summary>Số xe đang chờ duyệt cho badge trên menu "Quản lý xe".</summary>
public class DemXeChoDuyetUseCase
{
    private readonly IXeQuery _query;
    public DemXeChoDuyetUseCase(IXeQuery query) => _query = query;
    public Task<int> ThucHienAsync(CancellationToken ct = default) => _query.DemChoDuyetAsync(ct);
}

// ---------------- Chi tiết một xe ----------------

public record ThongTinXeAdminDto(int XeId, string BienSo, int LoaiXeId, string TrangThaiDuyet, bool CoAnh, bool ChiaSeNhom,
                                 DateTime TaoLuc, DateTime? DuyetLuc, string? TenAdminDuyet);
/// <summary>Chủ xe: không có mật khẩu hay thông tin nhạy cảm khác.</summary>
public record ChuXeAdminDto(int Id, string HoTen, string Sdt, string? Email, DateTime? TaoLuc);
/// <summary>VaoLuc, Khu: GoiCho không biết (của CongRaVao / TimXe), tầng API ghép thêm qua Contracts của các module đó.</summary>
public record TrongBaiAdminDto(bool DangTrongBai, DateTime? VaoLuc, string? Khu);
public record GoiHienTaiDto(int DangKyChoId, string TenGoi, decimal SoTien, DateTime NgayBatDau, DateTime NgayKetThuc,
                            bool ConHan, int SoNgayConLai, int SoNgayQuaHan, decimal PhiQuaHan);
public record ChiTietXeAdminDto(ThongTinXeAdminDto Xe, ChuXeAdminDto ChuXe, TrongBaiAdminDto TrongBai, GoiHienTaiDto? GoiHienTai,
                                IReadOnlyList<DangKyChoDto> LichSuGoi);

public static class GoiHienTai
{
    /// <summary>
    /// Gói "hiện tại" để Admin xem: gói đang có hiệu lực lúc này (bắt đầu ≤ bây giờ &lt; kết thúc); không có thì gói sắp bắt đầu sớm nhất
    /// (mua trước); không có nữa thì gói kết thúc gần nhất (đã hết hạn). Gói đã huỷ bỏ qua. null: chưa từng mua gói.
    /// Số ngày làm tròn lên (còn 1 giờ vẫn là "còn 1 ngày").
    /// </summary>
    public static GoiHienTaiDto? Tinh(IEnumerable<DangKyChoDto> lichSu, DateTime bayGio, decimal phiQuaHan)
    {
        var ds = lichSu.Where(d => d.TrangThai != "HUY").ToList();
        var g = ds.Where(d => d.NgayBatDau <= bayGio && bayGio < d.NgayKetThuc).OrderByDescending(d => d.NgayBatDau).FirstOrDefault()
                ?? ds.Where(d => d.NgayBatDau > bayGio).OrderBy(d => d.NgayBatDau).FirstOrDefault()
                ?? ds.OrderByDescending(d => d.NgayKetThuc).FirstOrDefault();
        if (g is null) return null;
        var conHan = g.NgayKetThuc > bayGio;
        static int Ngay(TimeSpan t) => (int)Math.Ceiling(t.TotalDays);
        return new GoiHienTaiDto(g.Id, g.TenGoi, g.SoTien, g.NgayBatDau, g.NgayKetThuc, conHan,
            conHan ? Ngay(g.NgayKetThuc - bayGio) : 0, conHan ? 0 : Ngay(bayGio - g.NgayKetThuc), phiQuaHan);
    }
}

/// <summary>Mọi thứ cho khung chi tiết xe (Admin): xe, chủ xe, gói hiện tại, lịch sử gói, đang trong bãi, phí quá hạn.</summary>
public class LayChiTietXeAdminUseCase
{
    private readonly IXeRepository _xe;
    private readonly IXeQuery _query;
    private readonly ITaiKhoanModule _taiKhoan;
    private readonly TraCuuTrongBai _trongBai;
    private readonly IClock _clock;
    public LayChiTietXeAdminUseCase(IXeRepository xe, IXeQuery query, ITaiKhoanModule taiKhoan, TraCuuTrongBai trongBai, IClock clock)
    { _xe = xe; _query = query; _taiKhoan = taiKhoan; _trongBai = trongBai; _clock = clock; }

    public async Task<ChiTietXeAdminDto> ThucHienAsync(int xeId, CancellationToken ct = default)
    {
        var xe = await _xe.TimAsync(xeId, ct)
                 ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_TIM_THAY_XE", "Không tìm thấy xe.");
        var ids = new List<int> { xe.ChuXeId };
        if (xe.DuyetBoiId is int a) ids.Add(a);
        var lienHe = await _taiKhoan.LayLienHeAsync(ids, ct);
        var chu = lienHe.GetValueOrDefault(xe.ChuXeId);
        var lichSu = await _query.LayLichSuGoiAsync(xeId, ct);
        var tb = (await _trongBai.LayAsync(new[] { xeId }, ct)).GetValueOrDefault(xeId);
        return new ChiTietXeAdminDto(
            new ThongTinXeAdminDto(xe.Id, xe.BienSo, xe.LoaiXeId, xe.TrangThaiDuyet, !string.IsNullOrEmpty(xe.AnhGiayDangKy), xe.ChiaSeNhom,
                xe.TaoLuc, xe.DuyetLuc, xe.DuyetBoiId is int ad ? lienHe.GetValueOrDefault(ad)?.HoTen : null),
            new ChuXeAdminDto(xe.ChuXeId, chu?.HoTen ?? "?", chu?.Sdt ?? "", chu?.Email, chu?.TaoLuc),
            new TrongBaiAdminDto(tb is not null, null, null),
            GoiHienTai.Tinh(lichSu, _clock.BayGio, tb?.SoTienNo ?? 0),
            lichSu);
    }
}
