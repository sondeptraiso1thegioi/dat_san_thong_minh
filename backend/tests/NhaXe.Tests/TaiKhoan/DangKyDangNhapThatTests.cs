using NhaXe.Modules.TaiKhoan.Application;
using NhaXe.Modules.TaiKhoan.Domain;
using NhaXe.SharedKernel;
using TK = NhaXe.Modules.TaiKhoan.Domain.TaiKhoan;

namespace NhaXe.Tests.TaiKhoan;

/// <summary>Đăng ký rồi đăng nhập ngay (không còn tài khoản demo), chuẩn hoá SĐT, admin đầu tiên từ cấu hình.</summary>
public class DangKyDangNhapThatTests
{
    private readonly FakeTaiKhoanRepository _repo = new();
    private readonly FakeHasher _hasher = new();
    private readonly FakeClock _clock = new();
    private DangKyUseCase DangKy() => new(_repo, _hasher);
    private DangNhapUseCase DangNhap() => new(_repo, _hasher, new FakeToken(), _clock);

    [Theory]
    [InlineData("0912 345 678", "0912345678")]
    [InlineData("0912.345.678", "0912345678")]
    [InlineData("0912-345-678", "0912345678")]
    [InlineData("+84 912 345 678", "0912345678")]
    [InlineData("84912345678", "0912345678")]
    [InlineData(" 0912345678 ", "0912345678")]
    public void BoKyTuPhanCach_DuaVeDangLuuTrongDb(string nhap, string mongDoi) =>
        Assert.Equal(mongDoi, ThongTinDangKy.BoKyTuPhanCach(nhap));

    [Fact]
    public void Sdt84MuoiChuSo_KhongBiCatNham() => Assert.Equal("0841234567", ThongTinDangKy.BoKyTuPhanCach("0841234567"));

    [Fact]
    public async Task DangKyRoiDangNhapNgay_BangSdtCoDauCach()
    {
        await DangKy().ThucHienAsync(new("Nguyễn An", "0912 345 678", "an@example.com", "MatKhau123"));
        Assert.Equal("0912345678", Assert.Single(_repo.Ds).Sdt);

        var kq = await DangNhap().ThucHienAsync(new("+84 912.345.678", "MatKhau123"));
        Assert.Equal("Nguyễn An", kq.HoTen); Assert.Equal(VaiTro.User, kq.VaiTro);
        Assert.NotNull(_repo.Ds[0].PhienHienTai);
    }

    [Fact]
    public async Task DangNhapSdtSaiDinhDang_CungThongBaoVoiSaiMatKhau()
    {
        var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => DangNhap().ThucHienAsync(new("abc", "MatKhau123")));
        Assert.Equal("SAI_DANG_NHAP", ex.Ma);
    }

    private static TaoAdminDauTienLenh Admin => new("Quản trị viên", "0911 000 111", "admin@nhaxe.vn", "AdminManh123");

    [Fact]
    public async Task ChuaCoAdmin_TaoAdminTuCauHinh_DangNhapDuoc()
    {
        Assert.True(await new TaoAdminDauTienUseCase(_repo, _hasher).ThucHienAsync(Admin));
        var tk = Assert.Single(_repo.Ds);
        Assert.Equal(VaiTro.Admin, tk.VaiTro); Assert.Equal("0911000111", tk.Sdt); Assert.Equal("hash:AdminManh123", tk.MatKhauHash);
        Assert.Equal(VaiTro.Admin, (await DangNhap().ThucHienAsync(new("0911000111", "AdminManh123"))).VaiTro);
    }

    [Fact]
    public async Task DaCoAdmin_KhongTaoThem_KhongDoiMatKhau()
    {
        _repo.Ds.Add(new TK(1, "Admin cũ", "0900000009", "hash:MatKhauCu1", VaiTro.Admin));
        Assert.False(await new TaoAdminDauTienUseCase(_repo, _hasher).ThucHienAsync(Admin));
        Assert.Single(_repo.Ds); Assert.Equal("hash:MatKhauCu1", _repo.Ds[0].MatKhauHash);
    }

    [Fact]
    public async Task SdtAdminDaLaNguoiDung_BaoTrung_KhongNangQuyen()
    {
        await DangKy().ThucHienAsync(new("Người dùng", "0911000111", "nd@example.com", "MatKhau123"));
        var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => new TaoAdminDauTienUseCase(_repo, _hasher).ThucHienAsync(Admin));
        Assert.Equal("TAI_KHOAN_TRUNG", ex.Ma);
        Assert.Equal(VaiTro.User, Assert.Single(_repo.Ds).VaiTro);
    }

    [Fact]
    public async Task MatKhauAdminYeu_KhongTao()
    {
        var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => new TaoAdminDauTienUseCase(_repo, _hasher).ThucHienAsync(Admin with { MatKhau = "123456" }));
        Assert.Equal("MAT_KHAU_YEU", ex.Ma); Assert.Empty(_repo.Ds);
    }
}
