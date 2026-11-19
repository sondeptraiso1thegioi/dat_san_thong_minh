using Microsoft.EntityFrameworkCore;
using NhaXe.Modules.NhomGiaDinh.Domain;

namespace NhaXe.Modules.NhomGiaDinh.Infrastructure;

/// <summary>DbContext riêng của module NhomGiaDinh, chỉ đụng bảng nhom, thanh_vien_nhom, yeu_cau_tham_gia.</summary>
public class NhomGiaDinhDbContext : DbContext
{
    public NhomGiaDinhDbContext(DbContextOptions<NhomGiaDinhDbContext> o) : base(o) { }
    public DbSet<Nhom> Nhoms => Set<Nhom>();
    public DbSet<ThanhVienNhom> ThanhViens => Set<ThanhVienNhom>();
    public DbSet<YeuCauThamGia> YeuCaus => Set<YeuCauThamGia>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Nhom>(e =>
        {
            e.ToTable("nhom"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ChuNhomId).HasColumnName("chu_nhom_id");
            e.Property(x => x.Ten).HasColumnName("ten");
            e.Property(x => x.MaMoi).HasColumnName("ma_moi");
            e.Property(x => x.MaHetHanLuc).HasColumnName("ma_het_han_luc");
            e.Property(x => x.TaoLuc).HasColumnName("tao_luc");
            e.HasMany(x => x.ThanhVien).WithOne().HasForeignKey(x => x.NhomId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.YeuCau).WithOne().HasForeignKey(x => x.NhomId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.ThanhVien).HasField("_thanhVien").UsePropertyAccessMode(PropertyAccessMode.Field);
            e.Navigation(x => x.YeuCau).HasField("_yeuCau").UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        b.Entity<ThanhVienNhom>(e =>
        {
            e.ToTable("thanh_vien_nhom"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.NhomId).HasColumnName("nhom_id");
            e.Property(x => x.TaiKhoanId).HasColumnName("tai_khoan_id");
            e.Property(x => x.VaiTro).HasColumnName("vai_tro");
            e.Property(x => x.ThamGiaLuc).HasColumnName("tham_gia_luc");
        });
        b.Entity<YeuCauThamGia>(e =>
        {
            e.ToTable("yeu_cau_tham_gia"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.NhomId).HasColumnName("nhom_id");
            e.Property(x => x.TaiKhoanId).HasColumnName("tai_khoan_id");
            e.Property(x => x.TrangThai).HasColumnName("trang_thai");
            e.Property(x => x.TaoLuc).HasColumnName("tao_luc");
            e.Property(x => x.XuLyLuc).HasColumnName("xu_ly_luc");
        });
    }
}
