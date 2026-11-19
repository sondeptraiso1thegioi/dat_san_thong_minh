using Microsoft.EntityFrameworkCore;
using NhaXe.Modules.GoiCho.Domain;
using GoiChoEntity = NhaXe.Modules.GoiCho.Domain.GoiCho;

namespace NhaXe.Modules.GoiCho.Infrastructure;

/// <summary>DbContext riêng của module Gói chỗ, map vào bảng có sẵn trong nha-xe.sql (database-first).</summary>
public class GoiChoDbContext : DbContext
{
    public GoiChoDbContext(DbContextOptions<GoiChoDbContext> options) : base(options) { }

    public DbSet<Xe> Xes => Set<Xe>();
    public DbSet<LoaiXe> LoaiXes => Set<LoaiXe>();
    public DbSet<GoiChoEntity> GoiChos => Set<GoiChoEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Xe>(e =>
        {
            e.ToTable("xe");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ChuXeId).HasColumnName("tai_khoan_id");
            e.Property(x => x.LoaiXeId).HasColumnName("loai_xe_id");
            e.Property(x => x.BienSo).HasColumnName("bien_so");
            e.Property(x => x.AnhGiayDangKy).HasColumnName("anh_giay_dang_ky");
            e.Property(x => x.TrangThaiDuyet).HasColumnName("trang_thai_duyet");
            e.Property(x => x.ChiaSeNhom).HasColumnName("chia_se_nhom");
            e.Property(x => x.TaoLuc).HasColumnName("tao_luc");
            e.Property(x => x.LyDoGo).HasColumnName("ly_do_go");
            e.Property(x => x.LyDoTuChoi).HasColumnName("ly_do_tu_choi");
            e.Property(x => x.DuyetLuc).HasColumnName("duyet_luc").HasColumnType("datetime2(0)");
            e.Property(x => x.DuyetBoiId).HasColumnName("duyet_boi_id");
        });
        b.Entity<LoaiXe>(e =>
        {
            e.ToTable("loai_xe");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Ten).HasColumnName("ten");
            e.Property(x => x.SucChua).HasColumnName("suc_chua");
            e.Property(x => x.GiaQuaHanNgay).HasColumnName("gia_qua_han_ngay").HasPrecision(12, 0);
        });
        b.Entity<GoiChoEntity>(e =>
        {
            e.ToTable("goi_cho");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.LoaiXeId).HasColumnName("loai_xe_id");
            e.Property(x => x.Ten).HasColumnName("ten");
            e.Property(x => x.SoNgay).HasColumnName("so_ngay");
            e.Property(x => x.Gia).HasColumnName("gia").HasPrecision(12, 0);
            e.Property(x => x.DangBan).HasColumnName("dang_ban");
        });
    }
}
