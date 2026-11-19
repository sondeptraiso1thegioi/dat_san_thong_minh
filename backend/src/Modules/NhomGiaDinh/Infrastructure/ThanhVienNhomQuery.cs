using Microsoft.EntityFrameworkCore;
using NhaXe.Modules.NhomGiaDinh.Application;

namespace NhaXe.Modules.NhomGiaDinh.Infrastructure;

public class ThanhVienNhomQuery : IThanhVienNhomQuery
{
    private readonly NhomGiaDinhDbContext _db;
    public ThanhVienNhomQuery(NhomGiaDinhDbContext db) => _db = db;

    public async Task<IReadOnlyList<int>> LayNguoiCungNhomAsync(int taiKhoanId, CancellationToken ct) =>
        await _db.Database.SqlQuery<int>($"""
            SELECT khac.tai_khoan_id AS Value
            FROM dbo.thanh_vien_nhom toi
            JOIN dbo.thanh_vien_nhom khac ON khac.nhom_id = toi.nhom_id AND khac.tai_khoan_id <> toi.tai_khoan_id
            WHERE toi.tai_khoan_id = {taiKhoanId}
            """).ToListAsync(ct);
}
