using Microsoft.EntityFrameworkCore;
using NhaXe.Modules.CongRaVao.Domain;

namespace NhaXe.Modules.CongRaVao.Infrastructure;

/// <summary>
/// DbContext riêng của module CongRaVao: chỉ 3 bảng của module (luot_gui, yeu_cau_xac_nhan, su_kien_cong).
/// Bảng luot_gui còn các cột vị trí (Find my car) chưa dùng ở đây: không ánh xạ thì EF không đụng tới, SQL dùng giá trị mặc định.
/// </summary>
public class CongRaVaoDbContext : DbContext
{
    public CongRaVaoDbContext(DbContextOptions<CongRaVaoDbContext> o) : base(o) { }

    public DbSet<LuotGui> LuotGui => Set<LuotGui>();
    public DbSet<YeuCauXacNhan> YeuCau => Set<YeuCauXacNhan>();
    public DbSet<SuKienCong> SuKienCong => Set<SuKienCong>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<LuotGui>(e =>
        {
            e.ToTable("luot_gui", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.XeId).HasColumnName("xe_id");
            e.Property(x => x.ThoiGianVao).HasColumnName("thoi_gian_vao").HasColumnType("datetime2(0)");
            e.Property(x => x.ThoiGianRa).HasColumnName("thoi_gian_ra").HasColumnType("datetime2(0)");
            e.Property(x => x.TrangThai).HasColumnName("trang_thai").HasMaxLength(15).IsUnicode(false);
            e.Property(x => x.DoTinCayVao).HasColumnName("do_tin_cay_vao").HasPrecision(5, 4);
            e.Property(x => x.DoTinCayRa).HasColumnName("do_tin_cay_ra").HasPrecision(5, 4);
            e.Property(x => x.ViTriKhuId).HasColumnName("vi_tri_khu_id");
            e.Property(x => x.ViTriLuc).HasColumnName("vi_tri_luc").HasColumnType("datetime2(0)");
            e.Property(x => x.ViTriNguon).HasColumnName("vi_tri_nguon").HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.AnhVao).HasColumnName("anh_vao").HasMaxLength(120).IsUnicode(false);
            e.Property(x => x.AnhRa).HasColumnName("anh_ra").HasMaxLength(120).IsUnicode(false);
            e.Ignore(x => x.DangMo);
        });

        b.Entity<YeuCauXacNhan>(e =>
        {
            e.ToTable("yeu_cau_xac_nhan", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.LuotGuiId).HasColumnName("luot_gui_id");
            e.Property(x => x.TaoLuc).HasColumnName("tao_luc").HasColumnType("datetime2(0)");
            e.Property(x => x.HetHanLuc).HasColumnName("het_han_luc").HasColumnType("datetime2(0)");
            e.Property(x => x.KetQua).HasColumnName("ket_qua").HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.ChuXeDongY).HasColumnName("chu_xe_dong_y");
            e.Property(x => x.ChuXeTraLoiId).HasColumnName("chu_xe_tra_loi_id");
            e.Property(x => x.ChuXeTraLoiLuc).HasColumnName("chu_xe_tra_loi_luc").HasColumnType("datetime2(0)");
            e.Property(x => x.QuanLyDongY).HasColumnName("quan_ly_dong_y");
            e.Property(x => x.QuanLyId).HasColumnName("quan_ly_id");
            e.Property(x => x.QuanLyTraLoiLuc).HasColumnName("quan_ly_tra_loi_luc").HasColumnType("datetime2(0)");
            e.Property(x => x.LyDo).HasColumnName("ly_do").HasMaxLength(255);
            e.Property(x => x.KetThucLuc).HasColumnName("ket_thuc_luc").HasColumnType("datetime2(0)");
            // Khoá lạc quan: UPDATE ... WHERE id = @id AND phien_ban = @cu. Không khớp (người khác vừa sửa) → DbUpdateConcurrencyException
            e.Property(x => x.PhienBan).HasColumnName("phien_ban").IsRowVersion();
            e.Ignore(x => x.DangCho);
            e.Ignore(x => x.NguoiDungXeBaoKhongPhai);
        });

        b.Entity<SuKienCong>(e =>
        {
            e.ToTable("su_kien_cong", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Loai).HasColumnName("loai").HasMaxLength(5).IsUnicode(false);
            e.Property(x => x.BienSo).HasColumnName("bien_so_doc_duoc").HasMaxLength(15).IsUnicode(false);
            e.Property(x => x.DoTinCay).HasColumnName("do_tin_cay").HasPrecision(5, 4);
            e.Property(x => x.LuotGuiId).HasColumnName("luot_gui_id");
            e.Property(x => x.KetQua).HasColumnName("ket_qua").HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.LyDo).HasColumnName("ly_do").HasMaxLength(255);
            e.Property(x => x.AdminId).HasColumnName("admin_xu_ly_id");
            e.Property(x => x.ThoiGian).HasColumnName("thoi_gian").HasColumnType("datetime2(0)");
            // Lúc xe vào, lượt gửi và nhật ký được thêm cùng lúc: EF lưu lượt gửi trước rồi tự điền luot_gui_id
            e.HasOne(x => x.Luot).WithMany().HasForeignKey(x => x.LuotGuiId);
        });
    }
}
