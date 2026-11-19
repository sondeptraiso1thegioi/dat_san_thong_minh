using NhaXe.Modules.NhomGiaDinh.Application;
using NhaXe.Modules.NhomGiaDinh.Contracts;
using NhaXe.Modules.NhomGiaDinh.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Tests.NhomGiaDinh;

public class FakeNhomRepository : INhomRepository, IThanhVienNhomQuery
{
    public List<Nhom> Ds { get; } = [];
    public int SoLanLuu { get; private set; }
    public Task<Nhom?> LayCuaThanhVienAsync(int id, CancellationToken ct) => Task.FromResult(Ds.SingleOrDefault(x => x.ThanhVien.Any(v => v.TaiKhoanId == id)));
    public Task<Nhom?> TimTheoMaAsync(string ma, CancellationToken ct) => Task.FromResult(Ds.SingleOrDefault(x => x.MaMoi == ma));
    public Task<bool> DaCoNhomAsync(int id, CancellationToken ct) => Task.FromResult(Ds.Any(x => x.ThanhVien.Any(v => v.TaiKhoanId == id)));
    public Task<IReadOnlyList<YeuCauDto>> LayYeuCauCuaAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<YeuCauDto>>(
        Ds.SelectMany(x => x.YeuCau.Where(y => y.TaiKhoanId == id).Select(y => new YeuCauDto(y.Id, x.Id, id, y.TrangThai, y.TaoLuc))).ToArray());
    public void Them(Nhom n) => Ds.Add(n);
    public void Xoa(Nhom n) => Ds.Remove(n);
    public Task LuuAsync(CancellationToken ct) { SoLanLuu++; return Task.CompletedTask; }
    public Task<T> TrongGiaoDichAsync<T>(Func<Task<T>> f, CancellationToken ct) => f();
    public Task<IReadOnlyList<int>> LayNguoiCungNhomAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<int>>(
        Ds.Where(x => x.ThanhVien.Any(v => v.TaiKhoanId == id)).SelectMany(x => x.ThanhVien.Select(v => v.TaiKhoanId)).ToArray());
}
public class FakeMaMoi : IMaMoiGenerator { public string Ma { get; set; } = "ABC1234567"; public string Tao() => Ma; }

public class NhomGiaDinhTests
{
    private readonly FakeNhomRepository _repo = new();
    private readonly FakeClock _clock = new();
    private readonly FakeMaMoi _ma = new();
    private async Task<Nhom> TaoAsync()
    {
        await new TaoNhomUseCase(_repo, _ma, _clock).ThucHienAsync(2, new("Gia đình An"));
        return _repo.Ds[0];
    }
    [Fact]
    public async Task TaoNhom_ChuNhomLaThanhVien_MaHetHanSauBayNgay()
    {
        var n = await TaoAsync(); Assert.Equal(2, n.ChuNhomId); Assert.Equal("CHU", Assert.Single(n.ThanhVien).VaiTro);
        Assert.Equal(_clock.BayGio.AddDays(7), n.MaHetHanLuc);
    }
    [Fact]
    public async Task DaCoNhom_KhongTaoNhomThuHai()
    {
        await TaoAsync(); var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => new TaoNhomUseCase(_repo, _ma, _clock).ThucHienAsync(2, new("Nhóm khác")));
        Assert.Equal("DA_CO_NHOM", ex.Ma); Assert.Single(_repo.Ds);
    }
    [Theory]
    [InlineData("")]
    [InlineData("A")]
    public async Task TenSai_KhongLuu(string ten)
    {
        var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => new TaoNhomUseCase(_repo, _ma, _clock).ThucHienAsync(2, new(ten)));
        Assert.Equal("TEN_NHOM_SAI", ex.Ma); Assert.Equal(0, _repo.SoLanLuu);
    }
    [Fact]
    public async Task XinThamGia_ChoDuyet_ChuaDuocCungNhom()
    {
        var n = await TaoAsync(); await new XinThamGiaUseCase(_repo, _clock).ThucHienAsync(3, new(" abc1234567 "));
        Assert.Equal("CHO_DUYET", Assert.Single(n.YeuCau).TrangThai); Assert.Single(n.ThanhVien);
        Assert.Empty(await new NhomGiaDinhModule(_repo).LayNguoiCungNhomAsync(3));
    }
    [Fact]
    public async Task GuiTrung_KhongTaoThemYeuCau()
    {
        await TaoAsync(); var uc = new XinThamGiaUseCase(_repo, _clock); await uc.ThucHienAsync(3, new(_ma.Ma));
        var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => uc.ThucHienAsync(3, new(_ma.Ma)));
        Assert.Equal("YEU_CAU_TRUNG", ex.Ma); Assert.Single(_repo.Ds[0].YeuCau);
    }
    [Theory]
    [InlineData("123", "MA_MOI_SAI")]
    [InlineData("ABC123", "KHONG_CO_MA_MOI")]
    [InlineData("ZZZ1234567", "KHONG_CO_MA_MOI")]
    public async Task MaSai_KhongGui(string ma, string loi)
    {
        await TaoAsync(); var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => new XinThamGiaUseCase(_repo, _clock).ThucHienAsync(3, new(ma)));
        Assert.Equal(loi, ex.Ma); Assert.Empty(_repo.Ds[0].YeuCau);
    }
    [Fact]
    public async Task MaHetHan_KhongGuiYeuCau()
    {
        await TaoAsync(); _clock.BayGio = _clock.BayGio.AddDays(7);
        var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => new XinThamGiaUseCase(_repo, _clock).ThucHienAsync(3, new(_ma.Ma)));
        Assert.Equal("MA_MOI_HET_HAN", ex.Ma);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChuNhomXuLy_ChiDongYMoiThemThanhVien(bool dongY)
    {
        var n = await TaoAsync(); n.XinThamGia(3, _clock.BayGio);
        await new XuLyYeuCauUseCase(_repo, _clock).ThucHienAsync(2, n.YeuCau.Single().Id, dongY);
        Assert.Equal(dongY ? 2 : 1, n.ThanhVien.Count); Assert.Equal(dongY ? "DONG_Y" : "TU_CHOI", n.YeuCau.Single().TrangThai);
        var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => new XuLyYeuCauUseCase(_repo, _clock).ThucHienAsync(2, n.YeuCau.Single().Id, false));
        Assert.Equal("YEU_CAU_DA_XU_LY", ex.Ma);
    }
    [Fact]
    public async Task DaVaoNhomKhac_KhongDuyetThem()
    {
        var n = await TaoAsync(); n.XinThamGia(3, _clock.BayGio);
        _repo.Ds.Add(Nhom.Tao(3, "Nhóm khác", "ZZZ1234567", _clock.BayGio));
        var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => new XuLyYeuCauUseCase(_repo, _clock).ThucHienAsync(2, 0, true));
        Assert.Equal("DA_CO_NHOM", ex.Ma); Assert.Single(n.ThanhVien); Assert.Equal("CHO_DUYET", n.YeuCau.Single().TrangThai);
    }
    [Fact]
    public async Task ThanhVien_KhongDuyet_KhongDoiMa_KhongGiaiTan()
    {
        var n = await TaoAsync(); n.XinThamGia(3, _clock.BayGio); n.XuLyYeuCau(2, 0, true, _clock.BayGio);
        Assert.Equal("KHONG_PHAI_CHU_NHOM", (await Assert.ThrowsAsync<LoiNghiepVu>(() => new XuLyYeuCauUseCase(_repo, _clock).ThucHienAsync(3, 0, false))).Ma);
        Assert.Equal("KHONG_PHAI_CHU_NHOM", (await Assert.ThrowsAsync<LoiNghiepVu>(() => new DoiMaMoiUseCase(_repo, _ma, _clock).ThucHienAsync(3))).Ma);
        Assert.Equal("KHONG_PHAI_CHU_NHOM", (await Assert.ThrowsAsync<LoiNghiepVu>(() => new GiaiTanNhomUseCase(_repo).ThucHienAsync(3))).Ma);
        var dto = await new XemNhomUseCase(_repo).ThucHienAsync(3); Assert.Null(dto.Nhom!.MaMoi); Assert.Empty(dto.Nhom.YeuCau);
    }
    [Fact]
    public async Task ChuNhomDoiMa_MaCuKhongDungDuoc()
    {
        await TaoAsync(); _ma.Ma = "NEW1234567"; await new DoiMaMoiUseCase(_repo, _ma, _clock).ThucHienAsync(2);
        Assert.Equal("KHONG_CO_MA_MOI", (await Assert.ThrowsAsync<LoiNghiepVu>(() => new XinThamGiaUseCase(_repo, _clock).ThucHienAsync(3, new("ABC1234567")))).Ma);
    }
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task XoaHoacRoiNhom_MatQuyenCungNhom(int nguoiThucHien)
    {
        var n = await TaoAsync(); n.XinThamGia(3, _clock.BayGio); n.XuLyYeuCau(2, 0, true, _clock.BayGio);
        Assert.Contains(2, await new NhomGiaDinhModule(_repo).LayNguoiCungNhomAsync(3));
        await new RoiNhomUseCase(_repo).ThucHienAsync(nguoiThucHien, 3);
        Assert.Empty(await new NhomGiaDinhModule(_repo).LayNguoiCungNhomAsync(3));
    }
    [Fact]
    public async Task ChuNhomKhongRoiDuoc_NhungGiaiTanDuoc()
    {
        await TaoAsync(); Assert.Equal("CHU_NHOM_KHONG_THE_ROI", (await Assert.ThrowsAsync<LoiNghiepVu>(() => new RoiNhomUseCase(_repo).ThucHienAsync(2, 2))).Ma);
        await new GiaiTanNhomUseCase(_repo).ThucHienAsync(2); Assert.Empty(_repo.Ds);
    }
}
