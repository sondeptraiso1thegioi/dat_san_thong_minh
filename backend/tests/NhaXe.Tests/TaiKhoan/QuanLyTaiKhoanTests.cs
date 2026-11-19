using NhaXe.Modules.TaiKhoan.Application;
using NhaXe.Modules.TaiKhoan.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Tests.TaiKhoan;

public class FakeGuiMa : IGuiMaKhoiPhuc
{
    public List<(string Email, string Ma)> DaGui { get; } = [];
    public Task GuiAsync(string email, string ma, CancellationToken ct) { DaGui.Add((email, ma)); return Task.CompletedTask; }
}

public class GuiMaHong : IGuiMaKhoiPhuc
{
    public bool Hong { get; set; } = true;
    public int SoLanGui { get; private set; }
    public Task GuiAsync(string email, string ma, CancellationToken ct)
    {
        if (Hong) throw new System.Net.Mail.SmtpException("Máy chủ email chưa bật");
        SoLanGui++; return Task.CompletedTask;
    }
}

public class QuanLyTaiKhoanTests
{
    private readonly FakeTaiKhoanRepository _repo = new();
    private readonly FakeHasher _hasher = new();
    private readonly FakeClock _clock = new();
    private readonly FakeGuiMa _gui = new();
    private static DangKyLenh Lenh => new("  Nguyễn An  ", "0901234567", " AN@example.com ", "MatKhau123");

    [Fact]
    public async Task DangKyHopLe_ChuanHoa_VaiTroUser_KhongMoPhien()
    {
        var kq = await new DangKyUseCase(_repo, _hasher).ThucHienAsync(Lenh);
        var tk = Assert.Single(_repo.Ds);
        Assert.Equal("Nguyễn An", kq.HoTen); Assert.Equal("USER", kq.VaiTro);
        Assert.Equal("an@example.com", tk.Email); Assert.Equal("hash:MatKhau123", tk.MatKhauHash); Assert.Null(tk.PhienHienTai);
    }
    [Theory]
    [InlineData("", "0901234567", "an@example.com", "MatKhau123", "HO_TEN_SAI")]
    [InlineData("An", "123", "an@example.com", "MatKhau123", "SDT_SAI")]
    [InlineData("An", "0901234567", "sai-email", "MatKhau123", "EMAIL_SAI")]
    [InlineData("An", "0901234567", "an@example.com", "123456", "MAT_KHAU_YEU")]
    public async Task DangKySai_KhongLuu(string ten, string sdt, string email, string mk, string ma)
    {
        var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => new DangKyUseCase(_repo, _hasher).ThucHienAsync(new(ten, sdt, email, mk)));
        Assert.Equal(ma, ex.Ma); Assert.Empty(_repo.Ds);
    }
    [Theory]
    [InlineData("0901234567", "khac@example.com")]
    [InlineData("0909999999", "AN@example.com")]
    public async Task TrungSdtHoacEmail_BaoXungDot(string sdt, string email)
    {
        var uc = new DangKyUseCase(_repo, _hasher); await uc.ThucHienAsync(Lenh);
        var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => uc.ThucHienAsync(Lenh with { Sdt = sdt, Email = email }));
        Assert.Equal("TAI_KHOAN_TRUNG", ex.Ma); Assert.Single(_repo.Ds);
    }
    [Fact]
    public async Task QuenMatKhau_GuiEmail_KhongLuuMaGoc_ChoPhepDungMotLan()
    {
        await new DangKyUseCase(_repo, _hasher).ThucHienAsync(Lenh);
        var tk = _repo.Ds[0]; tk.MoPhienMoi("phien-cu", _clock.BayGio);
        await new QuenMatKhauUseCase(_repo, _gui, _clock).ThucHienAsync(new(Lenh.Sdt));
        var mail = Assert.Single(_gui.DaGui); Assert.Equal("an@example.com", mail.Email); Assert.NotEqual(mail.Ma, tk.MaKhoiPhucHash);
        var reset = new DatLaiMatKhauUseCase(_repo, _hasher, _clock);
        await reset.ThucHienAsync(new(Lenh.Sdt, mail.Ma, "MatKhauMoi123"));
        Assert.Equal("hash:MatKhauMoi123", tk.MatKhauHash); Assert.Null(tk.PhienHienTai); Assert.Null(tk.MaKhoiPhucHash);
        var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => reset.ThucHienAsync(new(Lenh.Sdt, mail.Ma, "MatKhauKhac123")));
        Assert.Equal("MA_KHOI_PHUC_SAI", ex.Ma);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MaSaiHoacHetHan_KhongDoiMatKhau(bool hetHan)
    {
        await new DangKyUseCase(_repo, _hasher).ThucHienAsync(Lenh);
        await new QuenMatKhauUseCase(_repo, _gui, _clock).ThucHienAsync(new(Lenh.Sdt));
        var ma = _gui.DaGui[0].Ma;
        if (hetHan) _clock.BayGio = _clock.BayGio.AddMinutes(15); else ma = "sai";
        var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => new DatLaiMatKhauUseCase(_repo, _hasher, _clock).ThucHienAsync(new(Lenh.Sdt, ma, "MatKhauMoi123")));
        Assert.Equal("MA_KHOI_PHUC_SAI", ex.Ma); Assert.Equal("hash:MatKhau123", _repo.Ds[0].MatKhauHash);
    }
    [Fact]
    public async Task GuiLaiMa_CachNhauItNhatMotPhut_MaCuMatHieuLuc()
    {
        await new DangKyUseCase(_repo, _hasher).ThucHienAsync(Lenh);
        var uc = new QuenMatKhauUseCase(_repo, _gui, _clock);
        await uc.ThucHienAsync(new(Lenh.Sdt)); await uc.ThucHienAsync(new(Lenh.Sdt)); Assert.Single(_gui.DaGui);
        _clock.BayGio = _clock.BayGio.AddMinutes(1); await uc.ThucHienAsync(new(Lenh.Sdt)); Assert.Equal(2, _gui.DaGui.Count);
        var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => new DatLaiMatKhauUseCase(_repo, _hasher, _clock).ThucHienAsync(new(Lenh.Sdt, _gui.DaGui[0].Ma, "MatKhauMoi123")));
        Assert.Equal("MA_KHOI_PHUC_SAI", ex.Ma);
    }
    [Fact]
    public async Task TaiKhoanKhongTonTai_KhongGuiMail()
    {
        await new QuenMatKhauUseCase(_repo, _gui, _clock).ThucHienAsync(new("0909999999")); Assert.Empty(_gui.DaGui);
    }
    [Theory]
    [InlineData("MatKhau123", true)]
    [InlineData("sai", false)]
    public async Task DoiMatKhau_KiemTraMatKhauCu(string cu, bool hopLe)
    {
        await new DangKyUseCase(_repo, _hasher).ThucHienAsync(Lenh);
        var tk = _repo.Ds[0]; tk.MoPhienMoi("phien", _clock.BayGio);
        var task = new DoiMatKhauUseCase(_repo, _hasher).ThucHienAsync(tk.Id, new(cu, "MatKhauMoi123"));
        if (hopLe) { await task; Assert.Null(tk.PhienHienTai); Assert.Equal("hash:MatKhauMoi123", tk.MatKhauHash); }
        else { Assert.Equal("SAI_MAT_KHAU", (await Assert.ThrowsAsync<LoiNghiepVu>(() => task)).Ma); Assert.Equal("phien", tk.PhienHienTai); }
    }
    [Theory]
    [InlineData("moi", true)]
    [InlineData("cu", false)]
    public async Task DangXuat_ChiDongPhienCuaMinh(string phien, bool dong)
    {
        var tk = new NhaXe.Modules.TaiKhoan.Domain.TaiKhoan(2, "An", "0901234567", "hash:MatKhau123", "USER");
        tk.MoPhienMoi("moi", _clock.BayGio); _repo.Ds.Add(tk);
        await new DangXuatUseCase(_repo).ThucHienAsync(2, phien);
        Assert.Equal(dong ? null : "moi", tk.PhienHienTai);
    }

    /// <summary>Hộp thư chưa bật: trả 503 rõ ràng (không 500), huỷ mã, bấm lại ngay khi hộp thư bật là nhận được mail.</summary>
    [Fact]
    public async Task QuenMatKhau_EmailLoi_Bao503_VaBamLaiNgayDuoc()
    {
        await new DangKyUseCase(_repo, _hasher).ThucHienAsync(Lenh);
        var gui = new GuiMaHong();
        var uc = new QuenMatKhauUseCase(_repo, gui, _clock);
        var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => uc.ThucHienAsync(new(Lenh.Sdt)));
        Assert.Equal((LoaiLoi.DichVuTamNgung, "CHUA_GUI_DUOC_EMAIL"), (ex.Loai, ex.Ma));
        Assert.Null(_repo.Ds[0].MaKhoiPhucHash);              // mã chưa gửi đã bị huỷ

        gui.Hong = false;                                   // bật hộp thư, KHÔNG chờ 1 phút
        await uc.ThucHienAsync(new(Lenh.Sdt));
        Assert.Equal(1, gui.SoLanGui);
    }
}
