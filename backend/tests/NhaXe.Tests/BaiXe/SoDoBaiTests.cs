using NhaXe.Modules.BaiXe.Application;
using NhaXe.Modules.BaiXe.Domain;
using NhaXe.Modules.CongRaVao.Contracts;
using NhaXe.Modules.GoiCho.Contracts;

namespace NhaXe.Tests.BaiXe;

public class FakeBaiXeQuery : IBaiXeQuery
{
    public List<KhuRow> Khu { get; } = new();
    public List<ODoRow> O { get; } = new();
    public Task<IReadOnlyList<KhuRow>> LayKhuAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<KhuRow>>(Khu);
    public Task<IReadOnlyList<ODoRow>> LayODoAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ODoRow>>(O);
}

/// <summary>Module GoiCho giả: BaiXe chỉ biết GoiCho qua IGoiChoModule nên thay bằng bản giả là test được.</summary>
public class FakeGoiChoModule : IGoiChoModule
{
    public List<SucChuaLoaiXe> SucChua { get; } = new();
    public Dictionary<int, TinhTrangXeGoi> Xe { get; } = new();
    public Task<IReadOnlyList<SucChuaLoaiXe>> LaySucChuaAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SucChuaLoaiXe>>(SucChua);
    public Task<IReadOnlyDictionary<int, TinhTrangXeGoi>> LayTinhTrangXeAsync(IEnumerable<int> xeIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<int, TinhTrangXeGoi>>(xeIds.Distinct().Where(Xe.ContainsKey).ToDictionary(i => i, i => Xe[i]));

    // ----- phần cổng hỏi (test CongRaVao dùng) -----
    public Dictionary<string, XeTaiCong> TaiCong { get; } = new();
    public Dictionary<int, List<int>> NguoiDung { get; } = new();
    public Task<XeTaiCong?> TimXeTaiCongAsync(string bienSo, CancellationToken ct = default)
    {
        var bs = bienSo.ToUpperInvariant().Replace("-", "").Replace(".", "").Replace(" ", "");
        if (bs.Length < 7) throw new NhaXe.SharedKernel.LoiNghiepVu(NhaXe.SharedKernel.LoaiLoi.DuLieuKhongHopLe, "BIEN_SO_SAI", "Biển số sai");
        return Task.FromResult(TaiCong.GetValueOrDefault(bs));
    }
    public Task<IReadOnlyList<int>> LayNguoiDungXeAsync(int xeId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<int>>(NguoiDung.GetValueOrDefault(xeId) ?? new List<int>());
    public Task<IReadOnlyList<XeTomTat>> LayXeDuocDungAsync(int taiKhoanId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<XeTomTat>>(TaiCong.Values.Where(x => NguoiDung.GetValueOrDefault(x.XeId)?.Contains(taiKhoanId) == true)
            .Select(x => new XeTomTat(x.XeId, x.BienSo, 1, x.ChuXeId)).ToList());
    public Task<IReadOnlyList<XeTomTat>> LayXeDaDuyetAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<XeTomTat>>(TaiCong.Values.Where(x => x.DaDuyet).Select(x => new XeTomTat(x.XeId, x.BienSo, 1, x.ChuXeId)).ToList());
    public Task<KetQuaKhopGanDung> TimXeGanDungAsync(string bienSo, IReadOnlyCollection<int>? chiTrongXeIds = null, CancellationToken ct = default)
    {
        var ds = TaiCong.Values.Where(x => x.DaDuyet && (chiTrongXeIds is null || chiTrongXeIds.Contains(x.XeId))).ToList();
        var (khop, _, uv) = NhaXe.SharedKernel.KhopBienSo.Tim(bienSo.ToUpperInvariant(), ds, x => x.BienSo);
        return Task.FromResult(new KetQuaKhopGanDung(khop, uv.Select(x => x.BienSo).ToList()));
    }
    /// <summary>Nợ hiện tại của xe; test đổi giá trị này để giả lập "đã trả phí".</summary>
    public Dictionary<int, decimal> No { get; } = new();
    public Task<decimal> LaySoTienNoAsync(int xeId, CancellationToken ct = default) =>
        Task.FromResult(No.TryGetValue(xeId, out var n) ? n : TaiCong.Values.FirstOrDefault(x => x.XeId == xeId)?.SoTienNo ?? 0);
}

/// <summary>Cấu hình bãi giả (trạng thái ô).</summary>
public class FakeCauHinhBaiQuery : ICauHinhBaiQuery
{
    public List<KhuVuc> Khu { get; } = new();
    public List<ODo> O { get; } = new();
    public List<Camera> Camera { get; } = new();
    public List<TrangThaiO> TrangThai { get; } = new();
    public Task<IReadOnlyList<KhuVuc>> LayKhuAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<KhuVuc>>(Khu);
    public Task<IReadOnlyList<ODo>> LayOAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ODo>>(O);
    public Task<IReadOnlyList<Camera>> LayCameraAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Camera>>(Camera);
    public Task<IReadOnlyList<TrangThaiO>> LayTrangThaiOAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<TrangThaiO>>(TrangThai);
    public List<NutRow> Nut { get; } = new();
    public List<DuongNganNhat.Canh> Canh { get; } = new();
    public Task<IReadOnlyList<NutRow>> LayNutAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<NutRow>>(Nut);
    public Task<IReadOnlyList<DuongNganNhat.Canh>> LayCanhAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<DuongNganNhat.Canh>>(Canh);
}

public class SoDoBaiTests
{
    private readonly FakeClock _clock = new();

    private (FakeBaiXeQuery, FakeCongRaVaoModule, FakeGoiChoModule) DuLieu()
    {
        var q = new FakeBaiXeQuery();
        q.Khu.AddRange(new[] { new KhuRow { Id = 1, Ten = "Khu A", LoaiXeId = 1 },
                               new KhuRow { Id = 3, Ten = "Khu C", LoaiXeId = 2 } });
        q.O.AddRange(new[] { new ODoRow { Id = 1, KhuId = 1, MaO = "A01" }, new ODoRow { Id = 2, KhuId = 1, MaO = "A02" } });

        // xe nào đang trong bãi, đỗ ở đâu: module CongRaVao trả lời
        var c = new FakeCongRaVaoModule().CoXeTrongBai(10, 1, khuId: 1, oId: 2).CoXeTrongBai(11, 2, khuId: 3).CoXeTrongBai(12, 3);

        var g = new FakeGoiChoModule();
        g.SucChua.Add(new SucChuaLoaiXe(1, "O_TO", 10, 3, 1, 6));
        g.SucChua.Add(new SucChuaLoaiXe(2, "XE_MAY", 5, 5, 0, 0));
        g.Xe[10] = new TinhTrangXeGoi(10, "30F-444.44", 1, "O_TO", 2, _clock.BayGio.AddDays(10), 0, 0);
        g.Xe[11] = new TinhTrangXeGoi(11, "59-D1-456.78", 2, "XE_MAY", 3, _clock.BayGio.AddHours(5), 0, 0);
        g.Xe[12] = new TinhTrangXeGoi(12, "51K-999.99", 1, "O_TO", 4, _clock.BayGio.AddDays(-2), 2, 100000);
        return (q, c, g);
    }

    [Fact]
    public async Task XeDuocDatDungO_XeChuaRoO_VaXeChuaDinhVi_TachRieng()
    {
        var (q, c, g) = DuLieu();
        var kq = await new LaySoDoBaiUseCase(q, c, g, new FakeTaiKhoanModule(), _clock, new FakeCauHinhBaiQuery()).ThucHienAsync();

        var khuA = kq.Khu.Single(k => k.Ten == "Khu A");
        Assert.Null(khuA.O.Single(o => o.MaO == "A01").Xe);
        Assert.Equal(10, khuA.O.Single(o => o.MaO == "A02").Xe!.XeId);
        Assert.Equal(1, khuA.SoOTrong);
        Assert.Equal(11, kq.Khu.Single(k => k.Ten == "Khu C").XeChuaRoO.Single().XeId);
        Assert.Equal(12, kq.XeChuaDinhVi.Single().XeId);
        Assert.Equal("TK2", khuA.O[1].Xe!.TenChuXe);
        Assert.Equal("A02", khuA.O[1].Xe!.MaO);   // tên ô, tên khu: BaiXe tự tra từ bảng của mình
        Assert.Equal("Khu C", kq.XeTrongBai.Single(x => x.XeId == 11).Khu);
    }

    [Fact]
    public async Task BaiTrong_KhongCoXe_VanVeDuSoDo()
    {
        var (q, _, g) = DuLieu();
        var kq = await new LaySoDoBaiUseCase(q, new FakeCongRaVaoModule(), g, new FakeTaiKhoanModule(), _clock, new FakeCauHinhBaiQuery()).ThucHienAsync();
        Assert.Empty(kq.XeTrongBai);
        Assert.Equal(2, kq.Khu.Single(k => k.Ten == "Khu A").SoOTrong);
        Assert.Equal(0, kq.SucChua.Single(s => s.LoaiXeId == 1).SoXeTrongBai);
    }

    [Fact]
    public async Task ThongTinXeVaGoi_LayTuModuleGoiCho()
    {
        var (q, c, g) = DuLieu();
        var kq = await new LaySoDoBaiUseCase(q, c, g, new FakeTaiKhoanModule(), _clock, new FakeCauHinhBaiQuery()).ThucHienAsync();

        var xe12 = kq.XeChuaDinhVi.Single();
        Assert.Equal("51K-999.99", xe12.BienSo);
        Assert.Equal(100000, xe12.SoTienNo);
        Assert.Equal("QUA_HAN", xe12.TinhTrangGoi);
        Assert.Equal("SAP_HET", kq.XeTrongBai.Single(x => x.XeId == 11).TinhTrangGoi);

        var oTo = kq.SucChua.Single(s => s.LoaiXeId == 1);
        Assert.Equal(4, oTo.DangChiem);
        Assert.Equal(2, oTo.SoXeTrongBai);   // đếm từ lượt gửi (CongRaVao) + loại xe (GoiCho)
        Assert.Equal(2, oTo.SoODo);          // đếm từ ô của các khu ô tô (BaiXe)
        Assert.Equal("O_TO", kq.Khu.Single(k => k.Ten == "Khu A").LoaiXe); // tên loại xe lấy từ GoiCho
        Assert.True(kq.SucChua.Single(s => s.LoaiXeId == 2).HetCho);
    }

    [Theory]
    [InlineData(100, "CON_HAN")]
    [InlineData(72, "SAP_HET")]
    [InlineData(1, "SAP_HET")]
    [InlineData(0, "QUA_HAN")]
    [InlineData(-5, "QUA_HAN")]
    public void TinhTrangGoi_TheoMoc72Gio(int soGio, string mongDoi)
    {
        var bayGio = new DateTime(2026, 11, 5, 8, 0, 0);
        Assert.Equal(mongDoi, TinhTrangGoiTrenSoDo.Tinh(bayGio.AddHours(soGio), bayGio));
    }

    [Fact]
    public void KhongCoGoi() => Assert.Equal("KHONG_CO_GOI", TinhTrangGoiTrenSoDo.Tinh(null, DateTime.Now));
}
