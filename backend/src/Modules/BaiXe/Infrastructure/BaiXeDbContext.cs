using Microsoft.EntityFrameworkCore;
using NhaXe.Modules.BaiXe.Domain;

namespace NhaXe.Modules.BaiXe.Infrastructure;

/// <summary>
/// DbContext riêng của module BaiXe (mỗi module một DbContext, không dùng chung với GoiCho).
/// Chỉ ánh xạ 4 bảng của module: khu_vuc, camera, o_do, trang_thai_o. Bảng của module khác KHÔNG có ở đây,
/// nên code BaiXe không thể vô tình JOIN sang xe / luot_gui.
/// Thực thể Domain không biết EF: tên cột được khai báo ở đây (Fluent API), không dùng [Column] trong Domain.
/// </summary>
public class BaiXeDbContext : DbContext
{
    public BaiXeDbContext(DbContextOptions<BaiXeDbContext> o) : base(o) { }

    public DbSet<KhuVuc> Khu => Set<KhuVuc>();
    public DbSet<Camera> Camera => Set<Camera>();
    public DbSet<ODo> O => Set<ODo>();
    public DbSet<TrangThaiO> TrangThaiO => Set<TrangThaiO>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<KhuVuc>(e =>
        {
            e.ToTable("khu_vuc", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Ten).HasColumnName("ten").HasMaxLength(50);
            e.Property(x => x.LoaiXeId).HasColumnName("loai_xe_id");
            e.Property(x => x.SucChua).HasColumnName("suc_chua");
            e.Property(x => x.MaSoDo).HasColumnName("ma_so_do").HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.DangDung).HasColumnName("dang_dung");
        });

        b.Entity<Camera>(e =>
        {
            e.ToTable("camera", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.KhuId).HasColumnName("khu_vuc_id");
            e.Property(x => x.Ten).HasColumnName("ten").HasMaxLength(50);
            e.Property(x => x.NguonAnh).HasColumnName("nguon_anh").HasMaxLength(500).IsUnicode(false);
            e.Property(x => x.DangBat).HasColumnName("dang_bat");
            e.Property(x => x.VaiTro).HasColumnName("vai_tro").HasMaxLength(12).IsUnicode(false);
        });

        b.Entity<ODo>(e =>
        {
            e.ToTable("o_do", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.KhuId).HasColumnName("khu_vuc_id");
            e.Property(x => x.CameraId).HasColumnName("camera_id");
            e.Property(x => x.MaO).HasColumnName("ma_o").HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.MaQr).HasColumnName("ma_qr").HasMaxLength(50).IsUnicode(false);
        });

        b.Entity<TrangThaiO>(e =>
        {
            e.ToTable("trang_thai_o", "dbo");
            e.HasKey(x => x.OId);
            e.Property(x => x.OId).HasColumnName("o_do_id").ValueGeneratedNever();
            e.Property(x => x.TrangThai).HasColumnName("trang_thai").HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.Nguon).HasColumnName("nguon").HasMaxLength(5).IsUnicode(false);
            e.Property(x => x.CapNhatLuc).HasColumnName("cap_nhat_luc").HasColumnType("datetime2(0)");
        });
    }
}
