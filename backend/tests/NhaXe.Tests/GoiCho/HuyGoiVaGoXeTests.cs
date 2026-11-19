using NhaXe.Modules.GoiCho.Application;
using NhaXe.Modules.GoiCho.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Tests;

public class HoanTienTests
{
    private static readonly DateTime BayGio = new(2026, 11, 5, 8, 0, 0);
    private readonly HoanTheoNgayConLai _cs = new();

    private static GoiDaMua Goi(DateTime batDau, int soNgay, decimal tien) =>
        new(1, 1, 2, "1 tháng", batDau, batDau.AddDays(soNgay), tien, "CON_HAN", true, false);

    [Fact]
    public void ChuaBatDau_Hoan100PhanTram()
        => Assert.Equal(100000, _cs.Tinh(Goi(BayGio.AddDays(3), 30, 100000), BayGio).SoTien);

    [Fact]
    public void DaDung10Ngay_Hoan20Ngay()
    {
        var h = _cs.Tinh(Goi(BayGio.AddDays(-10), 30, 300000), BayGio);
        Assert.Equal(20, h.SoNgayConLai);
        Assert.Equal(200000, h.SoTien);
    }

    [Fact]
    public void LeGio_LamTronXuong()
    {
        // dùng 10 ngày 5 tiếng: còn 19 ngày 19 tiếng -> tính 19 ngày
        var h = _cs.Tinh(Goi(BayGio.AddDays(-10).AddHours(-5), 30, 300000), BayGio);
        Assert.Equal(19, h.SoNgayConLai);
        Assert.Equal(190000, h.SoTien);
    }
}

public class QuyTacHuyGoiTests
{
    private static readonly DateTime BayGio = new(2026, 11, 5, 8, 0, 0);

    private static GoiDaMua Goi(bool laGoiCuoi = true, bool trongBai = false, bool chuaBatDau = false, string tt = "CON_HAN") =>
        new(1, 1, 2, "1 tuần", chuaBatDau ? BayGio.AddDays(1) : BayGio.AddDays(-1), BayGio.AddDays(6), 30000, tt, laGoiCuoi, trongBai);

    [Fact] public void KhongPhaiChuXe() => Assert.Equal("KHONG_PHAI_CHU_XE", Assert.Throws<LoiNghiepVu>(() => Goi().KiemTraHuyDuoc(3, BayGio)).Ma);
    [Fact] public void KhongPhaiGoiCuoi() => Assert.Equal("KHONG_PHAI_GOI_CUOI", Assert.Throws<LoiNghiepVu>(() => Goi(laGoiCuoi: false).KiemTraHuyDuoc(2, BayGio)).Ma);
    [Fact] public void GoiDaHuy() => Assert.Equal("GOI_HET_HIEU_LUC", Assert.Throws<LoiNghiepVu>(() => Goi(tt: "HUY").KiemTraHuyDuoc(2, BayGio)).Ma);
    [Fact] public void DangDung_XeTrongBai_BiChan() => Assert.Equal("XE_TRONG_BAI", Assert.Throws<LoiNghiepVu>(() => Goi(trongBai: true).KiemTraHuyDuoc(2, BayGio)).Ma);
    [Fact] public void ChuaBatDau_XeTrongBai_VanHuyDuoc() => Goi(trongBai: true, chuaBatDau: true).KiemTraHuyDuoc(2, BayGio);
    [Fact] public void XemTruoc_XeTrongBai_VanXemDuoc() => Goi(trongBai: true).KiemTraXemHuy(2, BayGio);
}

public class FakeDangKyChoStoreHuy : IDangKyChoStore
{
    public GoiDaMua? Goi;
    public decimal? DaHuyVoiTien;
    public Task<DangKyChoKetQua> MuaGoiAsync(int t, int x, int g, CancellationToken ct) => throw new NotImplementedException();
    public Task<GoiDaMua?> LayGoiDaMuaAsync(int id, CancellationToken ct) => Task.FromResult(Goi);
    public string? KhachLyDo, KhachGopY;
    public Task HuyGoiAsync(int t, int id, decimal tien, string? lyDo, string? gopY, CancellationToken ct)
    { DaHuyVoiTien = tien; KhachLyDo = lyDo; KhachGopY = gopY; return Task.CompletedTask; }
    public string? AdminLyDo;
    public Task AdminHuyGoiAsync(int a, int id, string lyDo, decimal tien, CancellationToken ct)
    { DaHuyVoiTien = tien; AdminLyDo = lyDo; return Task.CompletedTask; }
}


public class HuyGoiUseCaseTests
{
    private readonly FakeClock _clock = new();
    private FakeDangKyChoStoreHuy StoreVoiGoi() => new()
    {
        Goi = new GoiDaMua(5, 2, 2, "1 tuần", _clock.BayGio.AddDays(1), _clock.BayGio.AddDays(8), 30000, "CON_HAN", true, false)
    };

    [Fact]
    public async Task HuyGoiChuaBatDau_GoiStoreVoiTienHoanDayDu()
    {
        var store = StoreVoiGoi();
        var kq = await new HuyGoiUseCase(store, new HoanTheoNgayConLai(), _clock, TaoModule.TraCuu(), new FakeThongBaoModule(), new FakeTaiKhoanModule()).ThucHienAsync(2, 5);
        Assert.Equal(30000, store.DaHuyVoiTien);
        Assert.True(kq.ChuaBatDau);
    }

    [Fact]
    public async Task DoiChinhSach_UseCaseGiuNguyen()
    {
        var store = StoreVoiGoi();
        await new HuyGoiUseCase(store, new KhongHoanTien(), _clock, TaoModule.TraCuu(), new FakeThongBaoModule(), new FakeTaiKhoanModule()).ThucHienAsync(2, 5);
        Assert.Equal(0, store.DaHuyVoiTien);
    }

    [Fact]
    public async Task NguoiKhacHuy_KhongGoiStore()
    {
        var store = StoreVoiGoi();
        await Assert.ThrowsAsync<LoiNghiepVu>(() => new HuyGoiUseCase(store, new HoanTheoNgayConLai(), _clock, TaoModule.TraCuu(), new FakeThongBaoModule(), new FakeTaiKhoanModule()).ThucHienAsync(3, 5));
        Assert.Null(store.DaHuyVoiTien);
    }
}

public class GoXeTests
{
    [Fact]
    public void XeChuaDuyet_KhongGoDuoc()
        => Assert.Equal("KHONG_GO_DUOC", Assert.Throws<LoiNghiepVu>(() => Xe.DungLai(1, 2, 1, "30A12345", TrangThaiDuyet.ChoDuyet).KiemTraGoDuoc("Biển số giả")).Ma);

    [Fact]
    public void LyDoQuaNgan_BaoLoi()
        => Assert.Equal("THIEU_LY_DO", Assert.Throws<LoiNghiepVu>(() => Xe.DungLai(1, 2, 1, "30A12345", TrangThaiDuyet.DaDuyet).KiemTraGoDuoc("abc")).Ma);

    [Fact]
    public async Task XeBiGo_KhongMuaGoiDuoc()
    {
        var repo = new FakeXeRepository();
        repo.Ds.Add(Xe.DungLai(1, 2, 1, "30A12345", TrangThaiDuyet.BiGo));
        var goi = new FakeGoiChoRepository();
        goi.Ds.Add(new NhaXe.Modules.GoiCho.Domain.GoiCho(1, 1, "1 tuần", 7, 300000));
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() =>
            new MuaGoiUseCase(repo, goi, new FakeThanhToan(), new FakeDangKyChoStore()).ThucHienAsync(2, new MuaGoiLenh(1, 1)));
        Assert.Equal("XE_BI_GO", loi.Ma);
    }
}
