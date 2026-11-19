using Microsoft.EntityFrameworkCore;
using NhaXe.Modules.NhomGiaDinh.Infrastructure;
using NhaXe.Modules.NhomGiaDinh.Domain;
using NhaXe.Modules.TaiKhoan.Infrastructure;
using TK = NhaXe.Modules.TaiKhoan.Domain.TaiKhoan;

namespace NhaXe.Tests.NhomGiaDinh;

// Kiểm tra model và dịch truy vấn thực của provider SQL Server, không mở kết nối.
public class AnhXaDbTests
{
    [Fact]
    public void Nhom_ChiAnhXaBaBangCuaMinh_CollectionChiDocDichDuocSangSql()
    {
        using var db = new NhomGiaDinhDbContext(new DbContextOptionsBuilder<NhomGiaDinhDbContext>().UseSqlServer("Server=unused;Database=unused").Options);
        Assert.Equal(new[] { "nhom", "thanh_vien_nhom", "yeu_cau_tham_gia" }, db.Model.GetEntityTypes().Select(x => x.GetTableName()).Order().ToArray());
        var sql = db.Nhoms.Include(x => x.ThanhVien).Include(x => x.YeuCau).Where(x => x.ThanhVien.Any(v => v.TaiKhoanId == 3)).ToQueryString();
        Assert.Contains("thanh_vien_nhom", sql); Assert.Contains("yeu_cau_tham_gia", sql);
        var nhom = Nhom.Tao(2, "Gia đình", "ABC1234567", new(2026, 11, 11));
        db.Nhoms.Add(nhom); db.ChangeTracker.DetectChanges();
        Assert.Equal(EntityState.Added, db.Entry(nhom).State);
        Assert.Equal(EntityState.Added, db.Entry(nhom.ThanhVien.Single()).State);
    }
    [Fact]
    public void TaiKhoan_ChiAnhXaBangCuaMinh_RowversionBaoVeCapNhatDongThoi()
    {
        using var db = new TaiKhoanDbContext(new DbContextOptionsBuilder<TaiKhoanDbContext>().UseSqlServer("Server=unused;Database=unused").Options);
        Assert.Equal("tai_khoan", Assert.Single(db.Model.GetEntityTypes()).GetTableName());
        var version = db.Model.FindEntityType(typeof(TK))!.FindProperty(nameof(TK.PhienBan))!;
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
    }
}
