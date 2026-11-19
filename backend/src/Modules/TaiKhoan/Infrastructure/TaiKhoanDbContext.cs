using Microsoft.EntityFrameworkCore;
using TK = NhaXe.Modules.TaiKhoan.Domain.TaiKhoan;

namespace NhaXe.Modules.TaiKhoan.Infrastructure;

/// <summary>
/// DbContext RIÊNG của module Tài khoản: chỉ map bảng mà module này sở hữu (Buổi 12: mỗi module giữ dữ liệu của mình).
/// Database-first: bảng đã tạo sẵn bằng database/nha-xe.sql, EF Core chỉ ánh xạ, không tạo bảng.
/// </summary>
public class TaiKhoanDbContext : DbContext
{
    public TaiKhoanDbContext(DbContextOptions<TaiKhoanDbContext> options) : base(options) { }

    public DbSet<TK> TaiKhoans => Set<TK>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<TK>(e =>
        {
            e.ToTable("tai_khoan");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.HoTen).HasColumnName("ho_ten");
            e.Property(x => x.Sdt).HasColumnName("sdt");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.MatKhauHash).HasColumnName("mat_khau_hash");
            e.Property(x => x.VaiTro).HasColumnName("vai_tro");
            e.Property(x => x.TrangThai).HasColumnName("trang_thai");
            e.Property(x => x.PhienHienTai).HasColumnName("phien_hien_tai");
            e.Property(x => x.DangNhapLuc).HasColumnName("dang_nhap_luc");
            e.Property(x => x.MaDay).HasColumnName("fcm_token");
            e.Property(x => x.TaoLuc).HasColumnName("tao_luc").ValueGeneratedOnAdd();
            e.Property(x => x.MaKhoiPhucHash).HasColumnName("ma_khoi_phuc_hash");
            e.Property(x => x.MaKhoiPhucHetHanLuc).HasColumnName("ma_khoi_phuc_het_han_luc");
            e.Property(x => x.GuiMaKhoiPhucLuc).HasColumnName("gui_ma_khoi_phuc_luc");
            e.Property(x => x.SoLanSai).HasColumnName("so_lan_sai");
            e.Property(x => x.KhoaDangNhapDen).HasColumnName("khoa_dang_nhap_den");
            e.Property(x => x.PhienBan).HasColumnName("phien_ban").IsRowVersion();
        });
    }
}
