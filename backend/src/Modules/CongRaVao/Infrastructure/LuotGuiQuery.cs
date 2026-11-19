using Microsoft.EntityFrameworkCore;
using NhaXe.Modules.CongRaVao.Application;
using NhaXe.Modules.CongRaVao.Contracts;

namespace NhaXe.Modules.CongRaVao.Infrastructure;

/// <summary>Đọc lượt gửi đang mở. Chỉ đọc bảng luot_gui, không JOIN sang bảng của module khác.</summary>
public class LuotGuiQuery : ILuotGuiQuery
{
    private class Row
    {
        public int LuotGuiId { get; set; }
        public int XeId { get; set; }
        public DateTime ThoiGianVao { get; set; }
        public int? ViTriKhuId { get; set; }
        public int? ViTriOId { get; set; }
        public DateTime? ViTriLuc { get; set; }
    }

    private readonly CongRaVaoDbContext _db;
    public LuotGuiQuery(CongRaVaoDbContext db) => _db = db;

    public async Task<IReadOnlyList<LuotGuiDangMo>> LayDangMoAsync(IReadOnlyCollection<int>? xeIds, CancellationToken ct)
    {
        // danh sách id chỉ gồm số nguyên; truyền như MỘT tham số rồi tách bằng STRING_SPLIT (không ghép chuỗi SQL)
        var ds = xeIds is null ? null : string.Join(',', xeIds);
        var rows = await _db.Database.SqlQuery<Row>($"""
            SELECT lg.id AS LuotGuiId, lg.xe_id AS XeId, lg.thoi_gian_vao AS ThoiGianVao,
                   lg.vi_tri_khu_id AS ViTriKhuId, lg.vi_tri_o_id AS ViTriOId, lg.vi_tri_luc AS ViTriLuc
            FROM dbo.luot_gui lg
            WHERE lg.thoi_gian_ra IS NULL
              AND ({ds} IS NULL OR lg.xe_id IN (SELECT CAST(value AS INT) FROM STRING_SPLIT({ds}, ',')))
            """).ToListAsync(ct);
        return rows.Select(r => new LuotGuiDangMo(r.LuotGuiId, r.XeId, r.ThoiGianVao, r.ViTriKhuId, r.ViTriOId, r.ViTriLuc)).ToList();
    }
}
