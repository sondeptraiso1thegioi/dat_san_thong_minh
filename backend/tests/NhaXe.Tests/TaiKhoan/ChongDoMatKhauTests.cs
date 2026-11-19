using NhaXe.Modules.TaiKhoan.Application;
using NhaXe.SharedKernel;
using TK = NhaXe.Modules.TaiKhoan.Domain.TaiKhoan;

namespace NhaXe.Tests.TaiKhoan;

/// <summary>Lớp 3 chống dò mật khẩu: khoá đăng nhập theo tài khoản sau 5 lần sai liên tiếp, thời gian khoá tăng dần.</summary>
public class ChongDoMatKhauTests
{
    private readonly FakeTaiKhoanRepository _repo = new();
    private readonly FakeClock _clock = new() { BayGio = new DateTime(2026, 11, 12, 8, 0, 0) };
    private DangNhapUseCase Uc() => new(_repo, new FakeHasher(), new FakeToken(), _clock);

    public ChongDoMatKhauTests() => _repo.Ds.Add(new TK(7, "An", "0901234567", "hash:Dung123", "USER"));

    private async Task<string> Thu(string mk)
    {
        try { await Uc().ThucHienAsync(new("0901234567", mk)); return "OK"; }
        catch (LoiNghiepVu e) { return e.Ma; }
    }

    [Fact]
    public async Task Sai5Lan_TamKhoa15Phut_KeCaMatKhauDung()
    {
        for (var i = 0; i < 4; i++) Assert.Equal("SAI_DANG_NHAP", await Thu("sai"));
        Assert.Equal("SAI_DANG_NHAP", await Thu("sai"));          // lần 5: vẫn báo sai, nhưng từ giờ bị khoá
        Assert.Equal("SAI_DANG_NHAP", await Thu("Dung123")); // đang khoá: mật khẩu đúng cũng không vào được, thông báo y hệt sai mật khẩu
        _clock.BayGio = _clock.BayGio.AddMinutes(15);
        Assert.Equal("OK", await Thu("Dung123"));
        Assert.Equal(0, _repo.Ds[0].SoLanSai);
    }

    [Fact]
    public async Task DotKhoaThuHai_GapDoiThoiGian()
    {
        for (var i = 0; i < 5; i++) await Thu("sai");
        _clock.BayGio = _clock.BayGio.AddMinutes(15);
        for (var i = 0; i < 5; i++) await Thu("sai");               // lần 6–10 → đợt 2
        Assert.Equal(_clock.BayGio.AddMinutes(30), _repo.Ds[0].KhoaDangNhapDen);
    }

    [Fact]
    public async Task ThuTrongLucKhoa_KhongDemThem()
    {
        for (var i = 0; i < 5; i++) await Thu("sai");
        for (var i = 0; i < 10; i++) Assert.Equal("SAI_DANG_NHAP", await Thu("sai"));
        Assert.Equal(5, _repo.Ds[0].SoLanSai);
    }

    [Fact]
    public async Task KhoaToiDa60Phut_KhongPhai24Gio()
    {
        for (var dot = 0; dot < 6; dot++)
        {
            for (var i = 0; i < 5; i++) await Thu("sai");
            _clock.BayGio = _repo.Ds[0].KhoaDangNhapDen!.Value;
        }
        for (var i = 0; i < 5; i++) await Thu("sai");
        Assert.Equal(_clock.BayGio.AddMinutes(60), _repo.Ds[0].KhoaDangNhapDen);
    }

    [Fact]
    public async Task DangNhapDung_DatLaiBoDem()
    {
        for (var i = 0; i < 4; i++) await Thu("sai");
        Assert.Equal("OK", await Thu("Dung123"));
        Assert.Equal(0, _repo.Ds[0].SoLanSai);
        Assert.Equal("SAI_DANG_NHAP", await Thu("sai"));             // đếm lại từ đầu, chưa khoá
    }

    [Fact]
    public async Task SdtKhongTonTai_CungThongBaoNhuSaiMatKhau()
    {
        var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => Uc().ThucHienAsync(new("0999999999", "x")));
        Assert.Equal("SAI_DANG_NHAP", ex.Ma);
    }

    [Fact]
    public void KhoaToiDa60Phut_DuSaiBaoNhieuLan()
    {
        var tk = new TK(1, "A", "0900000000", "h", "USER");
        var luc = new DateTime(2026, 1, 1);
        for (var i = 0; i < 60; i++) tk.GhiSaiMatKhau(luc);
        Assert.Equal(luc.AddMinutes(60), tk.KhoaDangNhapDen);
    }
}
