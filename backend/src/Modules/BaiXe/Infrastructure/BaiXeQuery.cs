using Microsoft.EntityFrameworkCore;
using NhaXe.Modules.BaiXe.Application;

namespace NhaXe.Modules.BaiXe.Infrastructure;

/// <summary>
/// Đọc dữ liệu bãi: chỉ bảng khu_vuc, o_do của module BaiXe.
/// Không đọc luot_gui (của CongRaVao), không JOIN xe / loai_xe / dang_ky_cho (của GoiCho): những thứ đó hỏi qua Contracts.
/// </summary>
public class BaiXeQuery : IBaiXeQuery
{
    private readonly BaiXeDbContext _db;
    public BaiXeQuery(BaiXeDbContext db) => _db = db;

    public async Task<IReadOnlyList<KhuRow>> LayKhuAsync(CancellationToken ct) =>
        await _db.Database.SqlQuery<KhuRow>($"""
            SELECT k.id AS Id, k.ten AS Ten, k.loai_xe_id AS LoaiXeId FROM dbo.khu_vuc k
            WHERE k.dang_dung = 1   -- Sơ đồ bãi chỉ hiện khu đang dùng (khu tạm tắt như Khu A, B ô tô bị ẩn, dữ liệu vẫn giữ)
            """).ToListAsync(ct);

    public async Task<IReadOnlyList<ODoRow>> LayODoAsync(CancellationToken ct) =>
        await _db.Database.SqlQuery<ODoRow>($"""
            SELECT o.id AS Id, o.khu_vuc_id AS KhuId, o.ma_o AS MaO FROM dbo.o_do o
            """).ToListAsync(ct);
}
