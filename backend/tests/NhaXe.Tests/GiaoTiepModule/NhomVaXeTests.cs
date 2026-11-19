using NhaXe.Modules.GoiCho.Domain;
using NhaXe.Modules.NhomGiaDinh.Application;
using NhaXe.Modules.NhomGiaDinh.Domain;
using NhaXe.Tests.NhomGiaDinh;

namespace NhaXe.Tests.GiaoTiepModule;

public class NhomVaXeTests
{
    [Fact]
    public async Task ChiSauKhiDuyetMoiDungXeChiaSe_RoiNhomMatQuyen()
    {
        var repo = new FakeNhomRepository(); var clock = new FakeClock();
        var n = Nhom.Tao(2, "Gia đình An", "ABC1234567", clock.BayGio); repo.Them(n);
        var contract = new NhomGiaDinhModule(repo);
        var xe = Xe.DungLai(10, 2, 1, "30A12345", TrangThaiDuyet.DaDuyet, chiaSeNhom: true);
        n.XinThamGia(3, clock.BayGio);
        Assert.False(xe.DuocDungBoi(3, await contract.LayNguoiCungNhomAsync(3)));
        await new XuLyYeuCauUseCase(repo, clock).ThucHienAsync(2, n.YeuCau.Single().Id, true);
        Assert.True(xe.DuocDungBoi(3, await contract.LayNguoiCungNhomAsync(3)));
        await new RoiNhomUseCase(repo).ThucHienAsync(3, 3);
        Assert.False(xe.DuocDungBoi(3, await contract.LayNguoiCungNhomAsync(3)));
        Assert.True(xe.DuocDungBoi(2, [])); // chủ xe giữ quyền
    }
    [Fact]
    public async Task CungNhom_XeKhongBatChiaSe_KhongDuocDung()
    {
        var repo = new FakeNhomRepository(); var clock = new FakeClock();
        var n = Nhom.Tao(2, "Gia đình An", "ABC1234567", clock.BayGio); repo.Them(n);
        n.XinThamGia(3, clock.BayGio); n.XuLyYeuCau(2, 0, true, clock.BayGio);
        var xe = Xe.DungLai(10, 2, 1, "30A12345", TrangThaiDuyet.DaDuyet);
        Assert.False(xe.DuocDungBoi(3, await new NhomGiaDinhModule(repo).LayNguoiCungNhomAsync(3)));
    }
}
