using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NhaXe.Modules.TimXe.Application;
using NhaXe.Modules.TimXe.Contracts;
using NhaXe.Modules.TimXe.Domain;

namespace NhaXe.Modules.TimXe.Infrastructure;

/// <summary>DbContext riêng của TimXe: chỉ ánh xạ bảng của module (giu_cho_khu, vi_tri_xe).</summary>
public class TimXeDbContext(DbContextOptions<TimXeDbContext> o) : DbContext(o)
{
    public DbSet<GiuChoKhu> Giu => Set<GiuChoKhu>();
    public DbSet<ViTriXe> ViTri => Set<ViTriXe>();
    public DbSet<DiSaiKhu> DiSai => Set<DiSaiKhu>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<GiuChoKhu>(e =>
        {
            e.ToTable("giu_cho_khu", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.LuotGuiId).HasColumnName("luot_gui_id");
            e.Property(x => x.KhuId).HasColumnName("khu_vuc_id");
            e.Property(x => x.BatDauLuc).HasColumnName("bat_dau_luc").HasColumnType("datetime2(0)");
            e.Property(x => x.HetHanLuc).HasColumnName("het_han_luc").HasColumnType("datetime2(0)");
            e.Property(x => x.TrangThai).HasColumnName("trang_thai").HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.KetThucLuc).HasColumnName("ket_thuc_luc").HasColumnType("datetime2(0)");
            e.Property(x => x.KhuGoiYId).HasColumnName("khu_goi_y_id");
            e.Property(x => x.KhuThucTeId).HasColumnName("khu_thuc_te_id");
            e.Property(x => x.CanhBaoKhuId).HasColumnName("canh_bao_khu_id");
            e.Property(x => x.CanhBaoLuc).HasColumnName("canh_bao_luc").HasColumnType("datetime2(0)");
        });
        b.Entity<DiSaiKhu>(e =>
        {
            e.ToTable("di_sai_khu", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.LuotGuiId).HasColumnName("luot_gui_id");
            e.Property(x => x.XeId).HasColumnName("xe_id");
            e.Property(x => x.ChuXeId).HasColumnName("chu_xe_id");
            e.Property(x => x.BienSo).HasColumnName("bien_so").HasMaxLength(15).IsUnicode(false);
            e.Property(x => x.KhuChiDinhId).HasColumnName("khu_chi_dinh_id");
            e.Property(x => x.KhuThucTeId).HasColumnName("khu_thuc_te_id");
            e.Property(x => x.KhuDay).HasColumnName("khu_day");
            e.Property(x => x.Luc).HasColumnName("luc").HasColumnType("datetime2(0)");
        });
        b.Entity<ViTriXe>(e =>
        {
            e.ToTable("vi_tri_xe", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.LuotGuiId).HasColumnName("luot_gui_id");
            e.Property(x => x.CameraId).HasColumnName("camera_id");
            e.Property(x => x.KhuId).HasColumnName("khu_vuc_id");
            e.Property(x => x.BienSoDocDuoc).HasColumnName("bien_so_doc_duoc").HasMaxLength(15).IsUnicode(false);
            e.Property(x => x.DoTinCay).HasColumnName("do_tin_cay").HasPrecision(5, 4);
            e.Property(x => x.Nguon).HasColumnName("nguon").HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.Huong).HasColumnName("huong").HasMaxLength(3).IsUnicode(false);
            e.Property(x => x.ThayLuc).HasColumnName("thay_luc").HasColumnType("datetime2(0)");
        });
    }
}

public class TimXeRepository(TimXeDbContext db) : ITimXeRepository
{
    public Task<GiuChoKhu?> TimGiuDangGiuAsync(int luotGuiId, CancellationToken ct) =>
        db.Giu.FirstOrDefaultAsync(g => g.LuotGuiId == luotGuiId && g.TrangThai == TrangThaiGiu.DangGiu, ct);

    public async Task<IReadOnlyDictionary<int, int>> DemGiuTheoKhuAsync(DateTime bayGio, CancellationToken ct) =>
        await db.Giu.AsNoTracking().Where(g => g.TrangThai == TrangThaiGiu.DangGiu && g.HetHanLuc > bayGio)
            .GroupBy(g => g.KhuId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);

    /// <summary>Một câu UPDATE ... OUTPUT: đổi trạng thái và lấy các suất vừa đổi cùng lúc, hai bản API không lấy trùng.</summary>
    public async Task<IReadOnlyList<GiuChoKhu>> KetThucQuaHanAsync(DateTime bayGio, CancellationToken ct) =>
        await db.Giu.FromSql($"""
            UPDATE dbo.giu_cho_khu
            SET trang_thai = 'HET_HAN', ket_thuc_luc = CASE WHEN {bayGio} < bat_dau_luc THEN bat_dau_luc ELSE {bayGio} END
            OUTPUT inserted.*
            WHERE trang_thai = 'DANG_GIU' AND het_han_luc <= {bayGio}
            """).AsNoTracking().ToListAsync(ct);

    public async Task<DateTime?> LayLanSaiCuoiAsync(int luotGuiId, int khuId, CancellationToken ct) =>
        await db.DiSai.AsNoTracking().Where(d => d.LuotGuiId == luotGuiId && d.KhuThucTeId == khuId)
            .OrderByDescending(d => d.Luc).Select(d => (DateTime?)d.Luc).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<DiSaiKhu>> LayDiSaiGanDayAsync(int soDong, CancellationToken ct) =>
        await db.DiSai.AsNoTracking().OrderByDescending(d => d.Luc).ThenByDescending(d => d.Id).Take(soDong).ToListAsync(ct);

    public async Task<IReadOnlyList<DemDiSai>> DemDiSaiTheoChuXeAsync(CancellationToken ct) =>
        (await db.DiSai.AsNoTracking().GroupBy(d => d.ChuXeId)
            .Select(g => new { g.Key, SoLan = g.Count(), KhuDay = g.Count(x => x.KhuDay), LanCuoi = g.Max(x => x.Luc) })
            .OrderByDescending(x => x.SoLan).ThenByDescending(x => x.LanCuoi).Take(50).ToListAsync(ct))
        .Select(x => new DemDiSai(x.Key, x.SoLan, x.KhuDay, x.LanCuoi)).ToList();

    public Task<ViTriXe?> LayGhiCuoiAsync(int luotGuiId, CancellationToken ct) =>
        db.ViTri.AsNoTracking().Where(v => v.LuotGuiId == luotGuiId).OrderByDescending(v => v.ThayLuc).ThenByDescending(v => v.Id).FirstOrDefaultAsync(ct);

    public async Task<DateTime?> LayLanCuoiTaiCameraAsync(int luotGuiId, int cameraId, CancellationToken ct) =>
        await db.ViTri.AsNoTracking().Where(v => v.LuotGuiId == luotGuiId && v.CameraId == cameraId)
            .OrderByDescending(v => v.ThayLuc).Select(v => (DateTime?)v.ThayLuc).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<int, GiuChoKhu>> LayGiuGanNhatAsync(IReadOnlyCollection<int> luotGuiIds, CancellationToken ct)
    {
        if (luotGuiIds.Count == 0) return new Dictionary<int, GiuChoKhu>();
        var ds = await db.Giu.AsNoTracking().Where(g => luotGuiIds.Contains(g.LuotGuiId)).ToListAsync(ct);
        return ds.GroupBy(g => g.LuotGuiId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.BatDauLuc).ThenByDescending(x => x.Id).First());
    }

    public async Task<ThongKeDinhVi> LayThongKeAsync(DateTime tu, DateTime den, CancellationToken ct)
    {
        // Lượt xe máy được chỉ khu (có suất giữ) trong khoảng; lần ghi khu đầu tiên của mỗi lượt cho biết nguồn định vị.
        var luot = await db.Giu.AsNoTracking().Where(g => g.BatDauLuc >= tu && g.BatDauLuc < den).Select(g => g.LuotGuiId).Distinct().ToListAsync(ct);
        if (luot.Count == 0) return new ThongKeDinhVi(0, 0, 0, 0, 0);
        var dau = (await db.ViTri.AsNoTracking().Where(v => luot.Contains(v.LuotGuiId) && v.Huong == HuongDi.Vao).ToListAsync(ct))
            .GroupBy(v => v.LuotGuiId).Select(g => g.OrderBy(v => v.ThayLuc).ThenBy(v => v.Id).First()).ToList();
        return new ThongKeDinhVi(luot.Count, dau.Count, dau.Count(v => v.Nguon == NguonGhi.CameraKhu),
            dau.Count(v => v.Nguon == NguonGhi.NguoiDung), dau.Count(v => v.Nguon == NguonGhi.Admin));
    }

    public void Them(GiuChoKhu g) => db.Giu.Add(g);
    public void Them(ViTriXe v) => db.ViTri.Add(v);
    public void Them(DiSaiKhu d) => db.DiSai.Add(d);
    public Task LuuAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public class CauHinhTimXe(TimXeDbContext db) : ICauHinhTimXe
{
    public async Task<int> LayAsync(string khoa, int macDinh, CancellationToken ct)
    {
        var s = await db.Database.SqlQuery<string>($"SELECT gia_tri AS Value FROM dbo.cau_hinh WHERE khoa = {khoa}").FirstOrDefaultAsync(ct);
        return int.TryParse(s, out var v) && v > 0 ? v : macDinh;
    }
}

/// <summary>Tác vụ nền: mỗi chu kỳ trả các suất giữ quá hạn. Chạy 2 instance: suất đã đổi trạng thái thì lần sau không lấy lại.</summary>
public class HetHanGiuChoNen(IServiceScopeFactory scopes, ILogger<HetHanGiuChoNen> log, TimeSpan chuKy) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        using var hen = new PeriodicTimer(chuKy);
        while (await hen.WaitForNextTickAsync(stop))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var n = await scope.ServiceProvider.GetRequiredService<HetHanGiuChoUseCase>().ThucHienAsync(stop);
                if (n > 0) log.LogInformation("Trả {N} suất giữ khu quá hạn", n);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogWarning(ex, "Trả suất giữ lỗi, thử lại chu kỳ sau"); }
        }
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddTimXeModule(this IServiceCollection s, string connectionString, TimeSpan chuKyNen)
    {
        s.AddDbContext<TimXeDbContext>(o => o.UseSqlServer(connectionString));
        s.AddScoped<ITimXeRepository, TimXeRepository>();
        s.AddScoped<ICauHinhTimXe, CauHinhTimXe>();
        s.AddTimXeApplication();
        s.AddHostedService(sp => new HetHanGiuChoNen(sp.GetRequiredService<IServiceScopeFactory>(),
                                                     sp.GetRequiredService<ILogger<HetHanGiuChoNen>>(), chuKyNen));
        return s;
    }
}
