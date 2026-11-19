using NhaXe.Modules.GoiCho.Application;
using NhaXe.Modules.GoiCho.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Tests;

public class KhachHuyGopYTests
{
    private readonly FakeClock _clock = new();
    private FakeDangKyChoStoreHuy Store() => new()
    {
        Goi = new GoiDaMua(5, 2, 2, "1 tuần", _clock.BayGio.AddDays(1), _clock.BayGio.AddDays(8), 30000, "CON_HAN", true, false)
    };

    [Fact]
    public async Task HuyCoLyDoVaGopY_ChuyenXuongStoreDaCatKhoangTrang()
    {
        var store = Store();
        await new HuyGoiUseCase(store, new HoanTheoNgayConLai(), _clock, TaoModule.TraCuu(), new FakeThongBaoModule(), new FakeTaiKhoanModule()).ThucHienAsync(2, 5, " Giá cao ", "  Nên có gói 3 tháng  ");
        Assert.Equal("Giá cao", store.KhachLyDo);
        Assert.Equal("Nên có gói 3 tháng", store.KhachGopY);
    }

    [Fact]
    public async Task KhongNhapGi_VanHuyDuoc()
    {
        var store = Store();
        await new HuyGoiUseCase(store, new HoanTheoNgayConLai(), _clock, TaoModule.TraCuu(), new FakeThongBaoModule(), new FakeTaiKhoanModule()).ThucHienAsync(2, 5, "", "   ");
        Assert.Equal(30000, store.DaHuyVoiTien);
        Assert.Null(store.KhachLyDo);
        Assert.Null(store.KhachGopY);
    }

    [Fact]
    public async Task LyDoNgoaiDanhSach_BiChan_KhongHuy()
    {
        var store = Store();
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() =>
            new HuyGoiUseCase(store, new HoanTheoNgayConLai(), _clock, TaoModule.TraCuu(), new FakeThongBaoModule(), new FakeTaiKhoanModule()).ThucHienAsync(2, 5, "Tự bịa", null));
        Assert.Equal("LY_DO_SAI", loi.Ma);
        Assert.Null(store.DaHuyVoiTien);
    }

    [Fact]
    public void GopYQua500KyTu_BiChan() =>
        Assert.Equal("GOP_Y_QUA_DAI", Assert.Throws<LoiNghiepVu>(() => LyDoKhachHuy.ChuanHoa(null, new string('a', 501))).Ma);

    [Fact]
    public void GopYDung500KyTu_DuocPhep() => LyDoKhachHuy.ChuanHoa(null, new string('a', 500));
}
