using NhaXe.Modules.GoiCho.Application;
using NhaXe.Modules.GoiCho.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Tests;

public class AdminHuyGoiTests
{
    private readonly FakeClock _clock = new();
    private readonly BangChinhSachHoan _bang = new(new IChinhSachHoanTien[] { new HoanTheoNgayConLai(), new KhongHoanTien() });

    // gói 30 ngày đang dùng, còn 20 ngày, xe đang trong bãi
    private FakeDangKyChoStoreHuy Store(bool laGoiCuoi = true) => new()
    {
        Goi = new GoiDaMua(5, 2, 2, "1 tháng", _clock.BayGio.AddDays(-10), _clock.BayGio.AddDays(20), 300000, "CON_HAN", laGoiCuoi, true)
    };

    [Fact]
    public async Task AdminHuy_XeTrongBai_VanHuyDuoc_GhiLyDo()
    {
        var store = Store();
        var kq = await new AdminHuyGoiUseCase(store, _bang, _clock, new FakeThongBaoModule())
            .ThucHienAsync(1, 5, new AdminHuyGoiLenh("  Quá hạn thanh toán  ", "KHONG_HOAN"));
        Assert.Equal("Quá hạn thanh toán", store.AdminLyDo);
        Assert.Equal(0, kq.SoTienHoan);
    }

    [Fact]
    public async Task AdminHuy_HoanTheoNgay_TinhDungTien()
    {
        var store = Store();
        await new AdminHuyGoiUseCase(store, _bang, _clock, new FakeThongBaoModule()).ThucHienAsync(1, 5, new AdminHuyGoiLenh("Bảo trì khu A", "THEO_NGAY_CON_LAI"));
        Assert.Equal(200000, store.DaHuyVoiTien);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public async Task AdminHuy_ThieuLyDo_BiChan(string? lyDo)
    {
        var store = Store();
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() =>
            new AdminHuyGoiUseCase(store, _bang, _clock, new FakeThongBaoModule()).ThucHienAsync(1, 5, new AdminHuyGoiLenh(lyDo, null)));
        Assert.Equal("THIEU_LY_DO", loi.Ma);
        Assert.Null(store.DaHuyVoiTien);
    }

    [Fact]
    public async Task AdminHuy_KhongPhaiGoiCuoi_BiChan()
    {
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() =>
            new AdminHuyGoiUseCase(Store(laGoiCuoi: false), _bang, _clock, new FakeThongBaoModule()).ThucHienAsync(1, 5, new AdminHuyGoiLenh("Vi phạm nội quy", null)));
        Assert.Equal("KHONG_PHAI_GOI_CUOI", loi.Ma);
    }

    [Fact]
    public void ChinhSachKhongCo_BaoLoi() =>
        Assert.Equal("CHINH_SACH_SAI", Assert.Throws<LoiNghiepVu>(() => _bang.Lay("HOAN_200")).Ma);
}

public class SucChuaTests
{
    [Fact]
    public void HaSucChuaDuoiSoDangChiem_BiChan() =>
        Assert.Equal("SUC_CHUA_QUA_THAP", Assert.Throws<LoiNghiepVu>(() => LoaiXe.KiemTraSucChuaMoi(1, 2)).Ma);

    [Fact]
    public void HaSucChuaBangSoDangChiem_DuocPhep() => LoaiXe.KiemTraSucChuaMoi(2, 2);

    [Fact]
    public void SucChuaAm_BiChan() =>
        Assert.Equal("SUC_CHUA_SAI", Assert.Throws<LoiNghiepVu>(() => LoaiXe.KiemTraSucChuaMoi(-1, 0)).Ma);
}
