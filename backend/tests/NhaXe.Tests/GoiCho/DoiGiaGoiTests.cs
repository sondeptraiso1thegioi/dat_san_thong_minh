using NhaXe.Modules.GoiCho.Application;
using NhaXe.Modules.GoiCho.Domain;
using NhaXe.SharedKernel;
using GoiChoEntity = NhaXe.Modules.GoiCho.Domain.GoiCho;

namespace NhaXe.Tests;

public class DoiGiaGoiTests
{
    private readonly FakeGoiChoRepository _goi = new();
    public DoiGiaGoiTests() => _goi.Ds.Add(new GoiChoEntity(1, 1, "1 tuần", 7, 300000));

    [Fact]
    public async Task DoiGiaHopLe_LuuVaTraGiaMoi()
    {
        var kq = await new DoiGiaGoiUseCase(_goi).ThucHienAsync(1, 350000);
        Assert.Equal(350000, kq.Gia);
        Assert.Equal(350000, _goi.Ds[0].Gia);
        Assert.Equal(1, _goi.SoLanLuu);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    [InlineData(123456)]          // không tròn nghìn
    [InlineData(2_000_000_000)]   // quá trần
    public async Task GiaSai_BiChan_KhongLuu(decimal gia)
    {
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => new DoiGiaGoiUseCase(_goi).ThucHienAsync(1, gia));
        Assert.Equal("GIA_SAI", loi.Ma);
        Assert.Equal(300000, _goi.Ds[0].Gia);
        Assert.Equal(0, _goi.SoLanLuu);
    }

    [Fact]
    public async Task GoiKhongTonTai_BaoKhongTimThay()
    {
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => new DoiGiaGoiUseCase(_goi).ThucHienAsync(99, 100000));
        Assert.Equal("KHONG_TIM_THAY_GOI", loi.Ma);
    }
}

/// <summary>Khách đang mở trang với giá cũ thì không bị trừ tiền theo giá mới mà chưa thấy.</summary>
public class MuaGoiSauKhiDoiGiaTests
{
    private readonly FakeXeRepository _xe = new();
    private readonly FakeGoiChoRepository _goi = new();
    private readonly FakeThanhToan _tt = new();
    private readonly FakeDangKyChoStore _store = new();
    private MuaGoiUseCase UseCase => new(_xe, _goi, _tt, _store);

    public MuaGoiSauKhiDoiGiaTests()
    {
        _xe.Ds.Add(Xe.DungLai(1, chuXeId: 2, loaiXeId: 1, "30A12345", TrangThaiDuyet.DaDuyet));
        _goi.Ds.Add(new GoiChoEntity(1, 1, "1 tuần", 7, 300000));
        _goi.Ds[0].DoiGia(350000); // Admin vừa đổi giá
    }

    [Fact]
    public async Task GiaKhachThayLaGiaCu_Bao409_KhongThuTien()
    {
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => UseCase.ThucHienAsync(2, new MuaGoiLenh(1, 1, 300000)));
        Assert.Equal("GIA_DA_DOI", loi.Ma);
        Assert.Equal(LoaiLoi.XungDot, loi.Loai);
        Assert.Equal(0, _tt.SoLanThu);
        Assert.Equal(0, _store.SoLanGoi);
    }

    [Fact]
    public async Task GiaKhachThayDungGiaMoi_MuaDuoc()
    {
        await UseCase.ThucHienAsync(2, new MuaGoiLenh(1, 1, 350000));
        Assert.Equal(1, _tt.SoLanThu);
    }

    [Fact]
    public async Task KhongGuiGia_VanMuaDuoc_DeTuongThichSwagger()
    {
        await UseCase.ThucHienAsync(2, new MuaGoiLenh(1, 1));
        Assert.Equal(1, _tt.SoLanThu);
    }
}

public class ThongDiepGiaTests
{
    [Fact]
    public void ThongDiep_GhiTienKieuVietNam()
    {
        var g = new GoiChoEntity(4, 2, "1 tuần", 7, 30000);
        g.DoiGia(1_235_000);
        var loi = Assert.Throws<LoiNghiepVu>(() => g.KiemTraGiaKhachThay(30000));
        Assert.Contains("30.000 đ thành 1.235.000 đ", loi.Message);
    }
}
