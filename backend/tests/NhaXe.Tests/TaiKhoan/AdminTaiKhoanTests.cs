using NhaXe.Modules.TaiKhoan.Application;
using NhaXe.SharedKernel;
using TK = NhaXe.Modules.TaiKhoan.Domain.TaiKhoan;

namespace NhaXe.Tests.TaiKhoan;

/// <summary>v10 module 1: mã đẩy theo máy đăng nhập gần nhất, Admin khoá / mở khoá tài khoản.</summary>
public class AdminTaiKhoanTests
{
    private static (FakeTaiKhoanRepository repo, DangNhapUseCase dn) Tao()
    {
        var repo = new FakeTaiKhoanRepository();
        repo.Ds.Add(new TK(1, "Admin", "0900000001", "hash:123456", "ADMIN"));
        repo.Ds.Add(new TK(2, "An", "0900000002", "hash:123456", "USER"));
        return (repo, new DangNhapUseCase(repo, new FakeHasher(), new FakeToken(), new FakeClock()));
    }

    [Fact]
    public async Task Dang_nhap_may_moi_ghi_de_ma_day_may_cu()
    {
        var (repo, dn) = Tao();
        await dn.ThucHienAsync(new DangNhapLenh("0900000002", "123456", "ma-may-1"));
        await dn.ThucHienAsync(new DangNhapLenh("0900000002", "123456", "ma-may-2"));
        var mod = new TaiKhoanModule(repo);
        Assert.Equal("ma-may-2", (await mod.LayMaDayAsync([2]))[2]);
    }

    [Fact]
    public async Task Dang_nhap_tren_web_khong_co_ma_thi_xoa_ma_cu()
    {
        var (repo, dn) = Tao();
        await dn.ThucHienAsync(new DangNhapLenh("0900000002", "123456", "ma-may-1"));
        await dn.ThucHienAsync(new DangNhapLenh("0900000002", "123456"));
        Assert.Empty(await new TaiKhoanModule(repo).LayMaDayAsync([2]));
    }

    [Fact]
    public async Task Khoa_tai_khoan_thi_mat_phien_va_khong_dang_nhap_duoc()
    {
        var (repo, dn) = Tao();
        await dn.ThucHienAsync(new DangNhapLenh("0900000002", "123456", "ma"));
        var phien = repo.Ds[1].PhienHienTai!;
        await new KhoaTaiKhoanUseCase(repo).ThucHienAsync(1, 2, khoa: true);
        Assert.False(await new TaiKhoanModule(repo).PhienConHieuLucAsync(2, phien));
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => dn.ThucHienAsync(new DangNhapLenh("0900000002", "123456")));
        Assert.Equal("TAI_KHOAN_KHOA", loi.Ma);

        await new KhoaTaiKhoanUseCase(repo).ThucHienAsync(1, 2, khoa: false);
        await dn.ThucHienAsync(new DangNhapLenh("0900000002", "123456"));
    }

    [Fact]
    public async Task Khong_khoa_duoc_admin_va_khong_tu_khoa()
    {
        var (repo, _) = Tao();
        repo.Ds.Add(new TK(3, "Admin 2", "0900000003", "hash:1", "ADMIN"));
        var uc = new KhoaTaiKhoanUseCase(repo);
        Assert.Equal("KHONG_TU_KHOA", (await Assert.ThrowsAsync<LoiNghiepVu>(() => uc.ThucHienAsync(1, 1, true))).Ma);
        Assert.Equal("KHONG_KHOA_ADMIN", (await Assert.ThrowsAsync<LoiNghiepVu>(() => uc.ThucHienAsync(1, 3, true))).Ma);
    }

    [Fact]
    public async Task Danh_sach_tim_theo_tu_khoa()
    {
        var (repo, _) = Tao();
        var ds = await new DanhSachTaiKhoanUseCase(repo, new FakeClock()).ThucHienAsync("An", null);
        Assert.Single(ds);
        Assert.Equal(2, ds[0].Id);
    }
}
