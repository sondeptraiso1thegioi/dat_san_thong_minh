using NhaXe.Modules.GoiCho.Application;
using NhaXe.Modules.GoiCho.Domain;
using NhaXe.Modules.TaiKhoan.Application;
using NhaXe.SharedKernel;
using TK = NhaXe.Modules.TaiKhoan.Domain.TaiKhoan;

namespace NhaXe.Tests;

public class BienSoTests
{
    [Theory]
    [InlineData("30A-123.45", "30A12345")]
    [InlineData("29b1 123.45", "29B112345")]
    [InlineData(" 51g67890 ", "51G67890")]
    public void ChuanHoa(string nhap, string ketQua) => Assert.Equal(ketQua, BienSo.Tao(nhap).GiaTri);

    [Theory]
    [InlineData("")]
    [InlineData("ABC")]
    [InlineData("3OA12345")]
    public void SaiDinhDang_BaoLoi(string nhap) => Assert.Throws<LoiNghiepVu>(() => BienSo.Tao(nhap));
}

public class ThemXeTests
{
    [Fact]
    public async Task ThemXe_TrangThaiChoDuyet_VaChuanHoaBien()
    {
        var repo = new FakeXeRepository();
        var uc = new ThemXeUseCase(repo, new FakeGoiChoRepository(), new FakeClock(), new FakeCauHinh());
        await uc.ThucHienAsync(4, new ThemXeLenh(2, "59-c1 999.99", false));
        var xe = Assert.Single(repo.Ds);
        Assert.Equal("59C199999", xe.BienSo);
        Assert.Equal(TrangThaiDuyet.ChoDuyet, xe.TrangThaiDuyet);
        Assert.Equal(4, xe.ChuXeId);
    }

    [Fact]
    public async Task BienSoTrung_BaoLoi()
    {
        var repo = new FakeXeRepository();
        repo.Ds.Add(Xe.DungLai(1, 2, 1, "30A12345", TrangThaiDuyet.DaDuyet));
        var uc = new ThemXeUseCase(repo, new FakeGoiChoRepository(), new FakeClock(), new FakeCauHinh());
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => uc.ThucHienAsync(4, new ThemXeLenh(1, "30A-123.45", false)));
        Assert.Equal("BIEN_SO_TRUNG", loi.Ma);
    }

    [Fact]
    public void DuyetXeDaXuLy_BaoLoi()
    {
        var xe = Xe.DungLai(1, 2, 1, "30A12345", TrangThaiDuyet.DaDuyet);
        Assert.Throws<LoiNghiepVu>(() => xe.TuChoi("Ảnh mờ, không đọc được"));
    }
}

public class DangNhapTests
{
    private readonly FakeTaiKhoanRepository _repo = new();
    private DangNhapUseCase UseCase => new(_repo, new FakeHasher(), new FakeToken(), new FakeClock());

    public DangNhapTests()
    {
        _repo.Ds.Add(new TK(2, "Nguyễn Văn An", "0900000002", "hash:123456", "USER"));
        _repo.Ds.Add(new TK(9, "Bị khoá", "0900000009", "hash:123456", "USER", "KHOA"));
    }

    [Fact]
    public async Task DungMatKhau_CoToken_VaPhienMoi()
    {
        var kq = await UseCase.ThucHienAsync(new DangNhapLenh("0900000002", "123456"));
        Assert.StartsWith("token-2-", kq.Token);
        Assert.NotNull(_repo.Ds[0].PhienHienTai);
    }

    [Fact]
    public async Task DangNhapLan2_PhienCuMatHieuLuc()
    {
        await UseCase.ThucHienAsync(new DangNhapLenh("0900000002", "123456"));
        var phienCu = _repo.Ds[0].PhienHienTai!;
        await UseCase.ThucHienAsync(new DangNhapLenh("0900000002", "123456"));
        Assert.False(await new TaiKhoanModule(_repo).PhienConHieuLucAsync(2, phienCu));
    }

    [Fact]
    public async Task SaiMatKhau_BaoLoi()
    {
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => UseCase.ThucHienAsync(new DangNhapLenh("0900000002", "sai")));
        Assert.Equal("SAI_DANG_NHAP", loi.Ma);
    }

    [Fact]
    public async Task TaiKhoanKhoa_BaoLoi()
    {
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => UseCase.ThucHienAsync(new DangNhapLenh("0900000009", "123456")));
        Assert.Equal("TAI_KHOAN_KHOA", loi.Ma);
    }
}
