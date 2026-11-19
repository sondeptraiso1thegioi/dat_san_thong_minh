using NhaXe.Modules.GoiCho.Application;
using NhaXe.Modules.GoiCho.Domain;
using NhaXe.SharedKernel;
using GoiChoEntity = NhaXe.Modules.GoiCho.Domain.GoiCho;

namespace NhaXe.Tests;

public class MuaGoiTests
{
    private readonly FakeXeRepository _xe = new();
    private readonly FakeGoiChoRepository _goi = new();
    private readonly FakeThanhToan _tt = new();
    private readonly FakeDangKyChoStore _store = new();
    private MuaGoiUseCase UseCase => new(_xe, _goi, _tt, _store);

    public MuaGoiTests()
    {
        _xe.Ds.Add(Xe.DungLai(1, chuXeId: 2, loaiXeId: 1, "30A12345", TrangThaiDuyet.DaDuyet));
        _xe.Ds.Add(Xe.DungLai(5, chuXeId: 4, loaiXeId: 2, "59C112233", TrangThaiDuyet.ChoDuyet));
        _goi.Ds.Add(new GoiChoEntity(1, 1, "1 tuần", 7, 300000));      // gói ô tô
        _goi.Ds.Add(new GoiChoEntity(4, 2, "1 tuần", 7, 30000));       // gói xe máy
        _goi.Ds.Add(new GoiChoEntity(9, 1, "Gói cũ", 7, 1, dangBan: false));
    }

    [Fact]
    public async Task ChuXe_MuaGoiDungLoai_ThanhCong()
    {
        var kq = await UseCase.ThucHienAsync(2, new MuaGoiLenh(1, 1));
        Assert.Equal(99, kq.DangKyChoId);
        Assert.Equal(1, _tt.SoLanThu);
        Assert.Equal(0, _tt.SoLanHoan);
    }

    [Fact]
    public async Task KhongPhaiChuXe_BiChan_KhongThuTien()
    {
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => UseCase.ThucHienAsync(3, new MuaGoiLenh(1, 1)));
        Assert.Equal("KHONG_PHAI_CHU_XE", loi.Ma);
        Assert.Equal(0, _tt.SoLanThu);
        Assert.Equal(0, _store.SoLanGoi);
    }

    [Fact]
    public async Task XeChuaDuyet_BiChan()
    {
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => UseCase.ThucHienAsync(4, new MuaGoiLenh(5, 4)));
        Assert.Equal("XE_CHUA_DUYET", loi.Ma);
    }

    [Fact]
    public async Task GoiSaiLoaiXe_BiChan()
    {
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => UseCase.ThucHienAsync(2, new MuaGoiLenh(1, 4)));
        Assert.Equal("SAI_LOAI_XE", loi.Ma);
    }

    [Fact]
    public async Task GoiNgungBan_BiChan()
    {
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => UseCase.ThucHienAsync(2, new MuaGoiLenh(1, 9)));
        Assert.Equal("GOI_NGUNG_BAN", loi.Ma);
    }

    [Fact]
    public async Task DbBaoHetCho_HoanTien()
    {
        _store.NemLoi = new LoiNghiepVu(LoaiLoi.XungDot, "HET_CHO", "Hết chỗ cho loại xe này.");
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => UseCase.ThucHienAsync(2, new MuaGoiLenh(1, 1)));
        Assert.Equal("HET_CHO", loi.Ma);
        Assert.Equal(1, _tt.SoLanHoan);
    }
}
