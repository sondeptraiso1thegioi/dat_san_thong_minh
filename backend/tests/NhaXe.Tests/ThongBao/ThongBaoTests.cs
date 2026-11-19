using NhaXe.Modules.ThongBao.Application;
using NhaXe.Modules.ThongBao.Contracts;
using NhaXe.SharedKernel;

namespace NhaXe.Tests.ThongBao;

public class FakeThongBaoStore : IThongBaoStore
{
    public List<(int TaiKhoan, ThongBaoDto Tb)> Ds { get; } = new();
    public Task<IReadOnlyList<ThongBaoDto>> LayAsync(int tk, IReadOnlyCollection<string>? loai, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ThongBaoDto>>(Ds.Where(x => x.TaiKhoan == tk && (loai is null || loai.Contains(x.Tb.Loai))).Select(x => x.Tb).ToList());
    public Task<int> DemChuaDocAsync(int tk, CancellationToken ct) => Task.FromResult(Ds.Count(x => x.TaiKhoan == tk && !x.Tb.DaDoc));
    public Task<int> DocHetAsync(int tk, CancellationToken ct)
    {
        var ds = Ds.Where(x => x.TaiKhoan == tk && !x.Tb.DaDoc).ToList();
        foreach (var x in ds) x.Tb.DaDoc = true;
        return Task.FromResult(ds.Count);
    }
    public Task<bool> DanhDauDaDocAsync(int tk, int id, CancellationToken ct)
    {
        var x = Ds.FirstOrDefault(d => d.TaiKhoan == tk && d.Tb.Id == id);
        if (x.Tb is null) return Task.FromResult(false);
        x.Tb.DaDoc = true; return Task.FromResult(true);
    }
    /// <summary>Giờ "hiện tại" của store giả (chống lặp so với GuiLuc).</summary>
    public DateTime BayGio { get; set; } = new(2026, 11, 19, 3, 42, 0);

    public Task<IReadOnlyList<TinDaLuu>> ThemAsync(IReadOnlyList<int> taiKhoanIds, string loai, string noiDung, string? hanhDong,
                                                   int? thamChieuId, int chongLapGiay, CancellationToken ct)
    {
        var kq = new List<TinDaLuu>();
        foreach (var tk in taiKhoanIds)
        {
            var trung = Ds.Any(x => x.TaiKhoan == tk && x.Tb.Loai == loai && x.Tb.NoiDung == noiDung && x.Tb.ThamChieuId == thamChieuId
                                    && x.Tb.GuiLuc >= BayGio.AddSeconds(-chongLapGiay));
            if (trung) continue;
            var id = Ds.Count + 1;
            Ds.Add((tk, new ThongBaoDto { Id = id, Loai = loai, NoiDung = noiDung, GuiLuc = BayGio, HanhDong = hanhDong, ThamChieuId = thamChieuId }));
            kq.Add(new TinDaLuu(tk, id));
        }
        return Task.FromResult<IReadOnlyList<TinDaLuu>>(kq);
    }
}

public class ThongBaoAdminTests
{
    [Fact]
    public async Task ChiLayKhachHuy_DemSoChuaDoc()
    {
        var s = new FakeThongBaoStore();
        s.Ds.Add((1, new ThongBaoDto { Id = 1, Loai = "KHACH_HUY", NoiDung = "An huỷ" }));
        s.Ds.Add((1, new ThongBaoDto { Id = 2, Loai = "KHACH_HUY", NoiDung = "Cúc huỷ", DaDoc = true }));
        s.Ds.Add((1, new ThongBaoDto { Id = 3, Loai = "VAO", NoiDung = "xe vào" }));
        var kq = await new LayThongBaoAdminUseCase(s).ThucHienAsync(1);
        Assert.Equal(2, kq.ThongBao.Count);
        Assert.Equal(1, kq.SoChuaDoc);
    }

    [Fact]
    public async Task DanhDauThongBaoCuaNguoiKhac_BaoKhongTimThay()
    {
        var s = new FakeThongBaoStore();
        s.Ds.Add((1, new ThongBaoDto { Id = 1, Loai = "KHACH_HUY" }));
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => new DanhDauDaDocUseCase(s).ThucHienAsync(99, 1));
        Assert.Equal("KHONG_TIM_THAY_THONG_BAO", loi.Ma);
    }
}

public class HopThuTests
{
    [Fact]
    public async Task HopThuKhach_LayMoiLoai_DocHetChiCuaMinh()
    {
        var s = new FakeThongBaoStore();
        s.Ds.Add((2, new ThongBaoDto { Id = 1, Loai = "VAO" }));
        s.Ds.Add((2, new ThongBaoDto { Id = 2, Loai = "SAP_HET_HAN" }));
        s.Ds.Add((3, new ThongBaoDto { Id = 3, Loai = "VAO" }));
        var hop = await new LayHopThuUseCase(s).ThucHienAsync(2);
        Assert.Equal((2, 2), (hop.ThongBao.Count, hop.SoChuaDoc));
        Assert.Equal(2, await new DocHetUseCase(s).ThucHienAsync(2));
        Assert.Equal(0, await new DemChuaDocUseCase(s).ThucHienAsync(2));
        Assert.Equal(1, await new DemChuaDocUseCase(s).ThucHienAsync(3));
    }
}

/// <summary>
/// Hằng số loại thông báo ở Contracts (module khác dùng để gửi) phải nằm trong danh sách hợp lệ của Domain ThongBao,
/// nếu không thông báo bị từ chối âm thầm (lỗi thật đã gặp khi thêm XE_CHO_DUYET / KET_QUA_DUYET).
/// </summary>
public class LoaiThongBaoKhopTests
{
    [Fact]
    public void MoiLoaiOContracts_DeuHopLeODomain()
    {
        var contracts = typeof(NhaXe.Modules.ThongBao.Contracts.LoaiThongBao)
            .GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToList();
        Assert.NotEmpty(contracts);
        Assert.All(contracts, l => Assert.Contains(l, NhaXe.Modules.ThongBao.Domain.LoaiThongBao.HopLe));
    }

    [Fact]
    public void AdminThayThongBaoXeChoDuyet() =>
        Assert.Contains("XE_CHO_DUYET", NhaXe.Modules.ThongBao.Domain.LoaiThongBao.HopThuAdmin);
}

/// <summary>Đẩy SignalR giả: ghi lại gói đã đẩy.</summary>
public class FakeDay : IDayThoiGianThuc
{
    public List<TinDay> DaDay { get; } = new();
    public Task DayAsync(IReadOnlyList<TinDay> ds, CancellationToken ct) { DaDay.AddRange(ds); return Task.CompletedTask; }
}

/// <summary>Thông báo bấm được: lưu hanh_dong + tham_chieu_id, đẩy kèm id; chống gửi lặp 30 giây.</summary>
public class ThongBaoHanhDongTests
{
    private readonly FakeThongBaoStore _s = new();
    private readonly FakeDay _day = new();
    private readonly NhaXe.Tests.FakeTaiKhoanModule _tk = new();
    private ThongBaoModule Module() => new(_s, _tk, _day);

    [Fact]
    public async Task LuuHanhDongVaThamChieu_DayKemIdVaHanhDong()
    {
        await Module().GuiNhieuAsync(new[] { 2, 3 }, "RA", "Xe 36C239395 đang ở cổng ra.",
            new DichThongBao(HanhDongThongBao.XacNhanRa, 123));
        Assert.All(_s.Ds, x => Assert.Equal((HanhDongThongBao.XacNhanRa, (int?)123), (x.Tb.HanhDong, x.Tb.ThamChieuId)));
        Assert.Equal(new[] { (2, 1), (3, 2) }, _day.DaDay.Select(t => (t.TaiKhoanId, t.Id)));
        Assert.All(_day.DaDay, t => Assert.Equal((HanhDongThongBao.XacNhanRa, (int?)123), (t.HanhDong, t.ThamChieuId)));
    }

    [Fact]
    public async Task KhongCoDich_ThongBaoChiDeDoc_CotNull()
    {
        await Module().GuiAsync(2, "VAO", "Xe A đã vào bãi.");
        Assert.Null(_s.Ds.Single().Tb.HanhDong);
        Assert.Null(_s.Ds.Single().Tb.ThamChieuId);
    }

    [Fact]
    public async Task HanhDongLa_BiTuChoi()
    {
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => Module().GuiAsync(2, "VAO", "x", new DichThongBao("MO_TRANG_LA", 1)));
        Assert.Equal("HANH_DONG_SAI", loi.Ma);
        Assert.Empty(_s.Ds);
    }

    [Fact]
    public async Task CungTinTrong30Giay_KhongGuiLai_Qua30GiayThiGui()
    {
        var dich = new DichThongBao(HanhDongThongBao.MuaGoi, 45);
        const string nd = "Xe 36C239395 vừa tới cổng vào nhưng chưa có gói.";
        await Module().GuiAsync(2, "HET_HAN", nd, dich);
        _s.BayGio = _s.BayGio.AddSeconds(20);
        await Module().GuiAsync(2, "HET_HAN", nd, dich); // camera quét lại / Admin chụp lại: bỏ qua
        Assert.Single(_s.Ds);
        Assert.Single(_day.DaDay);

        _s.BayGio = _s.BayGio.AddSeconds(15); // 35 giây sau tin đầu
        await Module().GuiAsync(2, "HET_HAN", nd, dich);
        Assert.Equal(2, _s.Ds.Count);
    }

    [Fact]
    public async Task KhacXe_HoacKhacNoiDung_VanGuiNgay()
    {
        await Module().GuiAsync(2, "HET_HAN", "Xe A chưa có gói.", new DichThongBao(HanhDongThongBao.MuaGoi, 45));
        await Module().GuiAsync(2, "HET_HAN", "Xe A chưa có gói.", new DichThongBao(HanhDongThongBao.MuaGoi, 46));
        await Module().GuiAsync(2, "KHU", "Khu S đã đầy, mời bạn sang Khu R.", new DichThongBao(HanhDongThongBao.DanDuong, 7));
        await Module().GuiAsync(2, "KHU", "Xe A: mời vào Khu S.", new DichThongBao(HanhDongThongBao.DanDuong, 7));
        Assert.Equal(4, _s.Ds.Count);
    }

    [Fact]
    public void HanhDongContracts_DuDanhSach()
    {
        var hang = typeof(HanhDongThongBao).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToList();
        Assert.Equal(hang.OrderBy(x => x), HanhDongThongBao.TatCa.OrderBy(x => x));
        Assert.All(hang, h => Assert.True(h.Length <= 30, $"{h} dài quá cột hanh_dong VARCHAR(30)"));
    }
}
