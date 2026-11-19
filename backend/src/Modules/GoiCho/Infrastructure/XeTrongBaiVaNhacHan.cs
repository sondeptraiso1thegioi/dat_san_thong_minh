using Microsoft.EntityFrameworkCore;
using NhaXe.Modules.GoiCho.Application;

namespace NhaXe.Modules.GoiCho.Infrastructure;

/// <summary>Bản sao "xe đang trong bãi" của GoiCho (bảng xe_trong_bai). Chỉ GoiCho đọc / ghi bảng này.</summary>
public class XeTrongBaiStore : IXeTrongBaiStore
{
    private readonly GoiChoDbContext _db;
    public XeTrongBaiStore(GoiChoDbContext db) => _db = db;

    private class Row { public int XeId { get; set; } public int LuotGuiId { get; set; } }

    public async Task<IReadOnlyDictionary<int, int>> LayAsync(IReadOnlyCollection<int> xeIds, CancellationToken ct)
    {
        if (xeIds.Count == 0) return new Dictionary<int, int>();
        var ds = string.Join(',', xeIds.Distinct()); // chỉ gồm số nguyên; truyền như tham số rồi tách bằng STRING_SPLIT
        var rows = await _db.Database.SqlQuery<Row>($"""
            SELECT t.xe_id AS XeId, t.luot_gui_id AS LuotGuiId
            FROM dbo.xe_trong_bai t
            JOIN STRING_SPLIT({ds}, ',') s ON CAST(s.value AS INT) = t.xe_id
            """).ToListAsync(ct);
        return rows.ToDictionary(r => r.XeId, r => r.LuotGuiId);
    }

    /// <summary>Idempotent: đã có đúng lượt gửi này thì thôi; xe còn dòng cũ (lỡ mất sự kiện ra) thì thay bằng lượt mới.</summary>
    public Task GhiVaoAsync(int xeId, int luotGuiId, DateTime vaoLuc, CancellationToken ct) =>
        _db.Database.ExecuteSqlInterpolatedAsync($"""
            MERGE dbo.xe_trong_bai WITH (HOLDLOCK) AS d
            USING (SELECT {xeId} AS xe_id, {luotGuiId} AS luot_gui_id, {vaoLuc} AS vao_luc) AS s ON d.xe_id = s.xe_id
            WHEN MATCHED AND d.luot_gui_id <> s.luot_gui_id THEN UPDATE SET luot_gui_id = s.luot_gui_id, vao_luc = s.vao_luc
            WHEN NOT MATCHED THEN INSERT (xe_id, luot_gui_id, vao_luc) VALUES (s.xe_id, s.luot_gui_id, s.vao_luc);
            """, ct);

    public Task GhiRaAsync(int xeId, int luotGuiId, CancellationToken ct) =>
        _db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM dbo.xe_trong_bai WHERE xe_id = {xeId} AND luot_gui_id = {luotGuiId}", ct);
}

/// <summary>
/// Đánh dấu và lấy các gói cần nhắc gia hạn bằng MỘT câu UPDATE ... OUTPUT: hai instance API chạy cùng lúc
/// thì mỗi gói chỉ một instance lấy được (không nhắc trùng).
/// </summary>
public class NhacHanStore : INhacHanStore
{
    private readonly GoiChoDbContext _db;
    public NhacHanStore(GoiChoDbContext db) => _db = db;

    public async Task<IReadOnlyList<GoiCanNhacRow>> DanhDauGoiCanNhacAsync(int soGio, CancellationToken ct)
    {
        // gói còn hạn, sẽ hết trong soGio giờ, chưa nhắc, và KHÔNG có gói nối tiếp (đã gia hạn thì không cần nhắc).
        // UPDATE ... OUTPUT trả ra đúng các gói vừa đánh dấu (một câu lệnh, nguyên tử).
        var ds = await _db.Database.SqlQueryRaw<GoiCanNhacRow>("""
            DECLARE @da TABLE (id INT PRIMARY KEY);
            UPDATE d SET da_nhac_72h = 1
            OUTPUT inserted.id INTO @da
            FROM dbo.dang_ky_cho d
            WHERE d.trang_thai = 'CON_HAN' AND d.da_nhac_72h = 0
              AND d.ngay_ket_thuc > SYSDATETIME()
              AND d.ngay_ket_thuc <= DATEADD(HOUR, @so_gio, SYSDATETIME())
              AND NOT EXISTS (SELECT 1 FROM dbo.dang_ky_cho n
                              WHERE n.xe_id = d.xe_id AND n.trang_thai <> 'HUY' AND n.ngay_ket_thuc > d.ngay_ket_thuc);
            SELECT d.id AS DangKyChoId, x.id AS XeId, x.bien_so AS BienSo, x.tai_khoan_id AS ChuXeId,
                   x.chia_se_nhom AS ChiaSeNhom, g.ten AS TenGoi, d.ngay_ket_thuc AS HetHanLuc
            FROM @da a
            JOIN dbo.dang_ky_cho d ON d.id = a.id
            JOIN dbo.xe x ON x.id = d.xe_id
            JOIN dbo.goi_cho g ON g.id = d.goi_cho_id;
            """, new Microsoft.Data.SqlClient.SqlParameter("@so_gio", soGio)).ToListAsync(ct);
        return ds;
    }

    public async Task<IReadOnlyList<GoiCanNhacRow>> DanhDauGoiVuaHetHanAsync(CancellationToken ct) =>
        await _db.Database.SqlQueryRaw<GoiCanNhacRow>("""
            DECLARE @da TABLE (id INT PRIMARY KEY);
            UPDATE d SET da_nhac_het_han = 1
            OUTPUT inserted.id INTO @da
            FROM dbo.dang_ky_cho d
            WHERE d.trang_thai <> 'HUY' AND d.da_nhac_het_han = 0
              AND d.ngay_ket_thuc <= SYSDATETIME() AND d.ngay_ket_thuc > DATEADD(DAY, -7, SYSDATETIME())
              AND NOT EXISTS (SELECT 1 FROM dbo.dang_ky_cho n
                              WHERE n.xe_id = d.xe_id AND n.trang_thai <> 'HUY' AND n.ngay_ket_thuc > d.ngay_ket_thuc);
            SELECT d.id AS DangKyChoId, x.id AS XeId, x.bien_so AS BienSo, x.tai_khoan_id AS ChuXeId,
                   x.chia_se_nhom AS ChiaSeNhom, g.ten AS TenGoi, d.ngay_ket_thuc AS HetHanLuc
            FROM @da a JOIN dbo.dang_ky_cho d ON d.id = a.id
            JOIN dbo.xe x ON x.id = d.xe_id JOIN dbo.goi_cho g ON g.id = d.goi_cho_id;
            """).ToListAsync(ct);

    public async Task<IReadOnlyList<GoiCanNhacRow>> DanhDauNhacQuaHanHomNayAsync(CancellationToken ct) =>
        await _db.Database.SqlQueryRaw<GoiCanNhacRow>("""
            DECLARE @da TABLE (id INT PRIMARY KEY);
            UPDATE d SET ngay_nhac_qua_han = CAST(SYSDATETIME() AS DATE)
            OUTPUT inserted.id INTO @da
            FROM dbo.dang_ky_cho d
            JOIN dbo.xe_trong_bai t ON t.xe_id = d.xe_id
            WHERE d.trang_thai <> 'HUY' AND d.ngay_ket_thuc <= SYSDATETIME()
              AND (d.ngay_nhac_qua_han IS NULL OR d.ngay_nhac_qua_han < CAST(SYSDATETIME() AS DATE))
              AND NOT EXISTS (SELECT 1 FROM dbo.dang_ky_cho n
                              WHERE n.xe_id = d.xe_id AND n.trang_thai <> 'HUY' AND n.ngay_ket_thuc > d.ngay_ket_thuc);
            SELECT d.id AS DangKyChoId, x.id AS XeId, x.bien_so AS BienSo, x.tai_khoan_id AS ChuXeId,
                   x.chia_se_nhom AS ChiaSeNhom, g.ten AS TenGoi, d.ngay_ket_thuc AS HetHanLuc
            FROM @da a JOIN dbo.dang_ky_cho d ON d.id = a.id
            JOIN dbo.xe x ON x.id = d.xe_id JOIN dbo.goi_cho g ON g.id = d.goi_cho_id;
            """).ToListAsync(ct);
}
