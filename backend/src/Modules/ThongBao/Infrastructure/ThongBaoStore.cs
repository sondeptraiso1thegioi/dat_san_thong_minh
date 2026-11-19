using Microsoft.EntityFrameworkCore;
using NhaXe.Modules.ThongBao.Application;

namespace NhaXe.Modules.ThongBao.Infrastructure;

public class ThongBaoStore : IThongBaoStore
{
    private readonly ThongBaoDbContext _db;
    public ThongBaoStore(ThongBaoDbContext db) => _db = db;

    public async Task<IReadOnlyList<ThongBaoDto>> LayAsync(int taiKhoanId, IReadOnlyCollection<string>? cacLoai, CancellationToken ct)
    {
        // danh sách loại truyền như MỘT tham số rồi tách bằng STRING_SPLIT; null = mọi loại
        var loai = cacLoai is null ? null : string.Join(',', cacLoai);
        return await _db.Database.SqlQuery<ThongBaoDto>($"""
            SELECT TOP 100 id AS Id, loai AS Loai, noi_dung AS NoiDung, gui_luc AS GuiLuc, da_doc AS DaDoc,
                   hanh_dong AS HanhDong, tham_chieu_id AS ThamChieuId
            FROM dbo.thong_bao
            WHERE tai_khoan_id = {taiKhoanId}
              AND ({loai} IS NULL OR loai IN (SELECT value FROM STRING_SPLIT({loai}, ',')))
            ORDER BY gui_luc DESC, id DESC
            """).ToListAsync(ct);
    }

    public Task<int> DemChuaDocAsync(int taiKhoanId, CancellationToken ct) =>
        _db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.thong_bao WHERE tai_khoan_id = {taiKhoanId} AND da_doc = 0")
           .SingleAsync(ct);

    public async Task<bool> DanhDauDaDocAsync(int taiKhoanId, int thongBaoId, CancellationToken ct) =>
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dbo.thong_bao SET da_doc = 1 WHERE id = {thongBaoId} AND tai_khoan_id = {taiKhoanId}", ct) > 0;

    public Task<int> DocHetAsync(int taiKhoanId, CancellationToken ct) =>
        _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dbo.thong_bao SET da_doc = 1 WHERE tai_khoan_id = {taiKhoanId} AND da_doc = 0", ct);

    public async Task<IReadOnlyList<TinDaLuu>> ThemAsync(IReadOnlyList<int> taiKhoanIds, string loai, string noiDung, string? hanhDong,
                                                         int? thamChieuId, int chongLapGiay, CancellationToken ct)
    {
        var kq = new List<TinDaLuu>();
        foreach (var id in taiKhoanIds)
        {
            // MỘT câu lệnh: kiểm tra "đã gửi đúng tin này trong N giây chưa" rồi thêm. UPDLOCK + HOLDLOCK khoá khoảng đọc
            // nên 2 bản API cùng gửi một lúc thì bản sau thấy bản trước, không thêm trùng.
            var moi = await _db.Database.SqlQuery<int>($"""
                INSERT dbo.thong_bao (tai_khoan_id, loai, noi_dung, hanh_dong, tham_chieu_id)
                OUTPUT INSERTED.id AS Value
                SELECT {id}, {loai}, {noiDung}, {hanhDong}, {thamChieuId}
                WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.thong_bao WITH (UPDLOCK, HOLDLOCK)
                    WHERE tai_khoan_id = {id} AND loai = {loai} AND noi_dung = {noiDung}
                      AND ISNULL(tham_chieu_id, -1) = ISNULL({thamChieuId}, -1)
                      AND gui_luc >= DATEADD(SECOND, -{chongLapGiay}, SYSDATETIME()))
                """).ToListAsync(ct);
            if (moi.Count > 0) kq.Add(new TinDaLuu(id, moi[0]));
        }
        return kq;
    }
}
