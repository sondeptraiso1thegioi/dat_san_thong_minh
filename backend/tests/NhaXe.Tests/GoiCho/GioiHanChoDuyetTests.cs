using NhaXe.Modules.GoiCho.Application;
using NhaXe.Modules.GoiCho.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Tests;

/// <summary>Giới hạn số xe CHỜ DUYỆT cùng lúc của mỗi tài khoản (mặc định 5).</summary>
public class GioiHanChoDuyetTests
{
    private static ThemXeUseCase TaoUc(FakeXeRepository repo, FakeCauHinh? ch = null) =>
        new(repo, new FakeGoiChoRepository(), new FakeClock(), ch ?? new FakeCauHinh());

    private static void ThemSan(FakeXeRepository repo, int chu, string trangThai, int soXe, int batDau)
    {
        for (var i = 0; i < soXe; i++)
            repo.Ds.Add(Xe.DungLai(batDau + i, chu, 2, $"29B1{batDau + i:00000}", trangThai));
    }

    [Fact]
    public async Task Du5XeChoDuyet_XeThu6BiChan()
    {
        var repo = new FakeXeRepository();
        ThemSan(repo, 5, TrangThaiDuyet.ChoDuyet, 5, 1);
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => TaoUc(repo).ThucHienAsync(5, new ThemXeLenh(1, "30F-444.44", false)));
        Assert.Equal("QUA_SO_XE_CHO_DUYET", loi.Ma);
        Assert.Equal(LoaiLoi.XungDot, loi.Loai);
        Assert.Equal(5, repo.Ds.Count);
    }

    [Fact]
    public async Task XeDaDuyetTuChoiBiGo_KhongTinh()
    {
        var repo = new FakeXeRepository();
        ThemSan(repo, 5, TrangThaiDuyet.ChoDuyet, 4, 1);
        ThemSan(repo, 5, TrangThaiDuyet.DaDuyet, 3, 10);
        ThemSan(repo, 5, TrangThaiDuyet.TuChoi, 3, 20);
        ThemSan(repo, 5, TrangThaiDuyet.BiGo, 1, 30);
        await TaoUc(repo).ThucHienAsync(5, new ThemXeLenh(1, "30F-444.44", false)); // xe chờ duyệt thứ 5: vẫn được
        Assert.Equal(5, repo.Ds.Count(x => x.ChuXeId == 5 && x.TrangThaiDuyet == TrangThaiDuyet.ChoDuyet));
    }

    [Fact]
    public async Task XeChoDuyetCuaNguoiKhac_KhongTinh()
    {
        var repo = new FakeXeRepository();
        ThemSan(repo, 4, TrangThaiDuyet.ChoDuyet, 5, 1);
        await TaoUc(repo).ThucHienAsync(5, new ThemXeLenh(1, "30F-444.44", false));
        Assert.Contains(repo.Ds, x => x.ChuXeId == 5);
    }

    [Fact]
    public async Task DoiCauHinh_GioiHanDoiTheo()
    {
        var repo = new FakeXeRepository();
        ThemSan(repo, 5, TrangThaiDuyet.ChoDuyet, 2, 1);
        var ch = new FakeCauHinh(); ch.GiaTri[ThemXeUseCase.KhoaSoXeChoDuyetToiDa] = 2;
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => TaoUc(repo, ch).ThucHienAsync(5, new ThemXeLenh(1, "30F-444.44", false)));
        Assert.Contains("tối đa 2", loi.Message);
    }
}
