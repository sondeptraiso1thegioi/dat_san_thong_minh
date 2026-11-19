using NhaXe.Modules.GoiCho.Contracts;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NhaXe.Modules.GoiCho.Application;
using NhaXe.Modules.GoiCho.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.GoiCho.Infrastructure;

/// <summary>
/// Đổi mã lỗi THROW trong thủ tục SQL thành lỗi nghiệp vụ. OCP: thêm mã lỗi mới chỉ cần thêm một dòng vào bảng,
/// không phải sửa if/else ở nơi gọi.
/// </summary>
public static class SqlLoiMapper
{
    private static readonly Dictionary<int, (LoaiLoi Loai, string Ma)> Bang = new()
    {
        [50001] = (LoaiLoi.KhongCoQuyen, "KHONG_PHAI_CHU_XE"),
        [50002] = (LoaiLoi.XungDot, "XE_CHUA_DUYET"),
        [50003] = (LoaiLoi.DuLieuKhongHopLe, "GOI_KHONG_HOP_LE"),
        [50004] = (LoaiLoi.XungDot, "HET_CHO"),
        [50005] = (LoaiLoi.XungDot, "CON_NO_QUA_HAN"),
        [50010] = (LoaiLoi.XungDot, "CHONG_NGAY"),
        [50020] = (LoaiLoi.KhongTimThay, "KHONG_CO_LUOT_GUI"),
        [50021] = (LoaiLoi.KhongCoQuyen, "KHONG_CO_QUYEN_DUNG_XE"),
        [50022] = (LoaiLoi.XungDot, "KHONG_CON_NO"),
        [50030] = (LoaiLoi.XungDot, "GOI_HET_HIEU_LUC"),
        [50031] = (LoaiLoi.XungDot, "XE_TRONG_BAI"),
        [50032] = (LoaiLoi.XungDot, "KHONG_PHAI_GOI_CUOI"),
        [50034] = (LoaiLoi.DuLieuKhongHopLe, "TIEN_HOAN_SAI"),
        [50040] = (LoaiLoi.XungDot, "KHONG_GO_DUOC"),
        [50041] = (LoaiLoi.XungDot, "XE_TRONG_BAI"),
        [50042] = (LoaiLoi.KhongCoQuyen, "KHONG_PHAI_ADMIN"),
        [50043] = (LoaiLoi.DuLieuKhongHopLe, "THIEU_LY_DO"),
        [50035] = (LoaiLoi.DuLieuKhongHopLe, "GOP_Y_QUA_DAI"),
        [50050] = (LoaiLoi.XungDot, "SUC_CHUA_QUA_THAP"),
        [50051] = (LoaiLoi.KhongTimThay, "LOAI_XE_SAI"),
    };

    public static Exception Doi(SqlException ex)
    {
        if (!Bang.TryGetValue(ex.Number, out var l)) return ex;
        // ex.Message gộp cả các cảnh báo SQL Server gửi kèm, nên lấy riêng thông điệp của đúng lỗi THROW
        var thongDiep = ex.Errors.Cast<SqlError>().FirstOrDefault(e => e.Number == ex.Number)?.Message ?? ex.Message;
        return new LoiNghiepVu(l.Loai, l.Ma, thongDiep);
    }
}

public partial class DangKyChoStore : IDangKyChoStore
{
    private readonly GoiChoDbContext _db;
    public DangKyChoStore(GoiChoDbContext db) => _db = db;

    public async Task<DangKyChoKetQua> MuaGoiAsync(int taiKhoanId, int xeId, int goiId, CancellationToken ct)
    {
        var outId = new SqlParameter("@id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "EXEC dbo.sp_mua_goi @tai_khoan_id = @tk, @xe_id = @xe, @goi_cho_id = @goi, @dang_ky_cho_id = @id OUTPUT",
                new object[] { new SqlParameter("@tk", taiKhoanId), new SqlParameter("@xe", xeId), new SqlParameter("@goi", goiId), outId }, ct);
        }
        catch (SqlException ex) { throw SqlLoiMapper.Doi(ex); }

        var id = (int)outId.Value;
        var r = (await _db.Database.SqlQuery<DangKyChoDto>($"""
            SELECT d.id AS Id, g.ten AS TenGoi, d.ngay_bat_dau AS NgayBatDau, d.ngay_ket_thuc AS NgayKetThuc,
                   d.so_tien AS SoTien, d.trang_thai AS TrangThai, d.mua_luc AS MuaLuc,
                   d.ly_do_huy AS LyDoHuy, d.gop_y AS GopY, d.huy_luc AS HuyLuc, CAST(0 AS BIT) AS HuyBoiAdmin
            FROM dbo.dang_ky_cho d JOIN dbo.goi_cho g ON g.id = d.goi_cho_id WHERE d.id = {id}
            """).ToListAsync(ct)).Single();
        // bắt đầu sau lúc mua = gói nối tiếp gói đang còn hạn, tức là gia hạn
        return new DangKyChoKetQua(r.Id, r.NgayBatDau, r.NgayKetThuc, r.SoTien, r.NgayBatDau > r.MuaLuc);
    }
}

public partial class DangKyChoStore
{
    private class GoiDaMuaRow
    {
        public int Id { get; set; }
        public int XeId { get; set; }
        public int ChuXeId { get; set; }
        public string TenGoi { get; set; } = "";
        public DateTime NgayBatDau { get; set; }
        public DateTime NgayKetThuc { get; set; }
        public decimal SoTien { get; set; }
        public string TrangThai { get; set; } = "";
        public bool LaGoiCuoi { get; set; }
        public string BienSo { get; set; } = "";
    }

    public async Task<GoiDaMua?> LayGoiDaMuaAsync(int dangKyChoId, CancellationToken ct)
    {
        var r = (await _db.Database.SqlQuery<GoiDaMuaRow>($"""
            SELECT d.id AS Id, d.xe_id AS XeId, x.tai_khoan_id AS ChuXeId, g.ten AS TenGoi,
                   d.ngay_bat_dau AS NgayBatDau, d.ngay_ket_thuc AS NgayKetThuc, d.so_tien AS SoTien, d.trang_thai AS TrangThai,
                   CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.dang_ky_cho s WHERE s.xe_id = d.xe_id AND s.id <> d.id
                                          AND s.trang_thai = 'CON_HAN' AND s.ngay_bat_dau > d.ngay_bat_dau)
                        THEN 0 ELSE 1 END AS BIT) AS LaGoiCuoi,
                   x.bien_so AS BienSo
            FROM dbo.dang_ky_cho d JOIN dbo.xe x ON x.id = d.xe_id JOIN dbo.goi_cho g ON g.id = d.goi_cho_id
            WHERE d.id = {dangKyChoId}
            """).ToListAsync(ct)).FirstOrDefault();
        return r is null ? null : new GoiDaMua(r.Id, r.XeId, r.ChuXeId, r.TenGoi, r.NgayBatDau, r.NgayKetThuc,
                                               r.SoTien, r.TrangThai, r.LaGoiCuoi, XeDangTrongBai: false, r.BienSo);
        // XeDangTrongBai: use case hỏi module CongRaVao rồi điền vào, store không đọc bảng luot_gui
    }

    public async Task HuyGoiAsync(int taiKhoanId, int dangKyChoId, decimal soTienHoan, string? lyDo, string? gopY, CancellationToken ct)
    {
        try
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC dbo.sp_huy_goi @tai_khoan_id = {taiKhoanId}, @dang_ky_cho_id = {dangKyChoId}, @so_tien_hoan = {soTienHoan}, @ly_do = {lyDo}, @gop_y = {gopY}", ct);
        }
        catch (SqlException ex) { throw SqlLoiMapper.Doi(ex); }
    }

    public async Task AdminHuyGoiAsync(int adminId, int dangKyChoId, string lyDo, decimal soTienHoan, CancellationToken ct)
    {
        try
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC dbo.sp_admin_huy_goi @admin_id = {adminId}, @dang_ky_cho_id = {dangKyChoId}, @ly_do = {lyDo}, @so_tien_hoan = {soTienHoan}", ct);
        }
        catch (SqlException ex) { throw SqlLoiMapper.Doi(ex); }
    }
}

public class SucChuaStore : ISucChuaStore
{
    private readonly GoiChoDbContext _db;
    public SucChuaStore(GoiChoDbContext db) => _db = db;

    public async Task DoiSucChuaAsync(int adminId, int loaiXeId, int sucChua, CancellationToken ct)
    {
        try
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC dbo.sp_doi_suc_chua @admin_id = {adminId}, @loai_xe_id = {loaiXeId}, @suc_chua = {sucChua}", ct);
        }
        catch (SqlException ex) { throw SqlLoiMapper.Doi(ex); }
    }
}

public class QuanLyXeStore : IQuanLyXeStore
{
    private readonly GoiChoDbContext _db;
    public QuanLyXeStore(GoiChoDbContext db) => _db = db;

    public async Task GoXeAsync(int adminId, int xeId, string lyDo, CancellationToken ct)
    {
        try
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.sp_go_xe @admin_id = {adminId}, @xe_id = {xeId}, @ly_do = {lyDo}", ct);
        }
        catch (SqlException ex) { throw SqlLoiMapper.Doi(ex); }
    }
}

public class PhiQuaHanStore : IPhiQuaHanStore
{
    private readonly GoiChoDbContext _db;
    public PhiQuaHanStore(GoiChoDbContext db) => _db = db;

    /// <summary>
    /// Gọi hàm fn_phi_qua_han (của GoiCho) cho từng lượt gửi. Id lượt gửi do CongRaVao trả lời.
    /// Ghi chú: bên trong hàm SQL vẫn đọc thời gian vào của lượt gửi (xem docs/kien-truc-module.md, mục ngoại lệ).
    /// </summary>
    public async Task<IReadOnlyList<PhiQuaHanDto>> LayNoAsync(IReadOnlyCollection<int> luotGuiIds, CancellationToken ct)
    {
        if (luotGuiIds.Count == 0) return Array.Empty<PhiQuaHanDto>();
        var ds = string.Join(',', luotGuiIds);
        return await _db.Database.SqlQuery<PhiQuaHanDto>($"""
            SELECT p.luot_gui_id AS LuotGuiId, p.het_han_luc AS HetHanLuc, p.so_ngay_con_no AS SoNgayConNo,
                   CAST(p.so_tien_con_no AS DECIMAL(12,0)) AS SoTienConNo
            FROM STRING_SPLIT({ds}, ',') s CROSS APPLY dbo.fn_phi_qua_han(CAST(s.value AS INT)) p
            """).ToListAsync(ct);
    }

    public async Task ThanhToanAsync(int taiKhoanId, int luotGuiId, CancellationToken ct)
    {
        try
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC dbo.sp_thanh_toan_phi_qua_han @tai_khoan_id = {taiKhoanId}, @luot_gui_id = {luotGuiId}", ct);
        }
        catch (SqlException ex) { throw SqlLoiMapper.Doi(ex); }
    }
}

/// <summary>Sức chứa lấy từ v_cho_con_ban: cùng nguồn với sp_mua_goi nên con số Admin thấy khớp với con số chặn bán.</summary>
public class SucChuaQuery : ISucChuaQuery
{
    private readonly GoiChoDbContext _db;
    public SucChuaQuery(GoiChoDbContext db) => _db = db;

    public async Task<IReadOnlyList<SucChuaLoaiXe>> LaySucChuaAsync(CancellationToken ct) =>
        (await _db.Database.SqlQuery<SucChuaRow>($"""
            SELECT loai_xe_id AS LoaiXeId, loai_xe AS LoaiXe, suc_chua AS SucChua,
                   so_xe_giu_suat AS SoXeGiuSuat, so_xe_qua_han_trong_bai AS SoXeQuaHanTrongBai,
                   CASE WHEN so_cho_con_ban < 0 THEN 0 ELSE so_cho_con_ban END AS SoChoConBan
            FROM dbo.v_cho_con_ban ORDER BY loai_xe_id
            """).ToListAsync(ct))
        .Select(r => new SucChuaLoaiXe(r.LoaiXeId, r.LoaiXe, r.SucChua, r.SoXeGiuSuat, r.SoXeQuaHanTrongBai, r.SoChoConBan))
        .ToList();
}

public class DoanhThuQuery(GoiChoDbContext db) : IDoanhThuQuery
{
    private class Row { public string Loai { get; set; } = ""; public decimal Tong { get; set; } public int So { get; set; } }

    public async Task<NhaXe.Modules.GoiCho.Contracts.DoanhThu> LayAsync(DateTime tu, DateTime den, CancellationToken ct)
    {
        var ds = await db.Database.SqlQuery<Row>($"""
            SELECT loai AS Loai, SUM(so_tien) AS Tong, COUNT(*) AS So
            FROM dbo.thanh_toan
            WHERE trang_thai = 'THANH_CONG' AND thoi_gian >= {tu} AND thoi_gian < {den}
            GROUP BY loai
            """).ToListAsync(ct);
        decimal T(string l) => ds.FirstOrDefault(r => r.Loai == l)?.Tong ?? 0;
        return new(T("MUA_GOI"), T("PHI_QUA_HAN"), T("HOAN_TIEN"), ds.Sum(r => r.So));
    }
}
