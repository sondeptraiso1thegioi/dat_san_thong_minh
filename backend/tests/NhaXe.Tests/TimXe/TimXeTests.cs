using NhaXe.Modules.AiBienSo.Contracts;
using NhaXe.Modules.BaiXe.Contracts;
using NhaXe.Modules.CongRaVao.Contracts;
using NhaXe.Modules.GoiCho.Contracts;
using NhaXe.Modules.TimXe.Application;
using NhaXe.Modules.TimXe.Contracts;
using NhaXe.Modules.TimXe.Domain;
using NhaXe.SharedKernel;
using NhaXe.Tests.BaiXe;
using LoaiTB = NhaXe.Modules.ThongBao.Contracts.LoaiThongBao;

namespace NhaXe.Tests.TimXe;

public class FakeBaiXeModule : IBaiXeModule
{
    public List<KhuThongTin> Khu { get; } = new();
    public List<CameraThongTin> Camera { get; } = new();
    public Task CapNhatTrangThaiOAsync(int oId, string trangThai, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<KhuThongTin>> LayKhuAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<KhuThongTin>>(Khu);
    public Task<CameraThongTin?> LayCameraAsync(int cameraId, CancellationToken ct = default) => Task.FromResult(Camera.FirstOrDefault(c => c.Id == cameraId));
    public Task<DuongDiDto?> TimDuongAsync(int khuId, CancellationToken ct = default) =>
        Task.FromResult<DuongDiDto?>(new DuongDiDto(khuId, 10, [new(1, "Lối vào", 0, 0), new(9, "Đầu khu", 10, 0)]));
}

public class FakeTimXeRepository : ITimXeRepository
{
    public List<GiuChoKhu> Giu { get; } = new();
    public List<ViTriXe> ViTri { get; } = new();
    private readonly List<object> _cho = new();
    private int _id;
    public Task<GiuChoKhu?> TimGiuDangGiuAsync(int luotGuiId, CancellationToken ct) =>
        Task.FromResult(Giu.FirstOrDefault(g => g.LuotGuiId == luotGuiId && g.TrangThai == TrangThaiGiu.DangGiu));
    public Task<IReadOnlyDictionary<int, int>> DemGiuTheoKhuAsync(DateTime bayGio, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<int, int>>(Giu.Where(g => g.ConGiu(bayGio)).GroupBy(g => g.KhuId).ToDictionary(g => g.Key, g => g.Count()));
    public List<DiSaiKhu> DiSai { get; } = new();
    /// <summary>Giống câu UPDATE ... OUTPUT thật: đổi trạng thái và trả các suất vừa đổi (gọi lại lần hai không trả lại).</summary>
    public Task<IReadOnlyList<GiuChoKhu>> KetThucQuaHanAsync(DateTime bayGio, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<GiuChoKhu>>(Giu.Where(g => g.HetHan(bayGio)).ToList());
    public Task<DateTime?> LayLanSaiCuoiAsync(int luotGuiId, int khuId, CancellationToken ct) =>
        Task.FromResult(DiSai.Where(d => d.LuotGuiId == luotGuiId && d.KhuThucTeId == khuId).Select(d => (DateTime?)d.Luc).Max());
    public Task<IReadOnlyList<DiSaiKhu>> LayDiSaiGanDayAsync(int soDong, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DiSaiKhu>>(DiSai.OrderByDescending(d => d.Luc).Take(soDong).ToList());
    public Task<IReadOnlyList<DemDiSai>> DemDiSaiTheoChuXeAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DemDiSai>>(DiSai.GroupBy(d => d.ChuXeId)
            .Select(g => new DemDiSai(g.Key, g.Count(), g.Count(x => x.KhuDay), g.Max(x => x.Luc))).OrderByDescending(x => x.SoLan).ToList());
    public Task<ViTriXe?> LayGhiCuoiAsync(int luotGuiId, CancellationToken ct) =>
        Task.FromResult(ViTri.Where(v => v.LuotGuiId == luotGuiId).OrderBy(v => v.ThayLuc).LastOrDefault());
    public Task<DateTime?> LayLanCuoiTaiCameraAsync(int luotGuiId, int cameraId, CancellationToken ct) =>
        Task.FromResult(ViTri.Where(v => v.LuotGuiId == luotGuiId && v.CameraId == cameraId).Select(v => (DateTime?)v.ThayLuc).Max());
    public Task<IReadOnlyDictionary<int, GiuChoKhu>> LayGiuGanNhatAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<int, GiuChoKhu>>(Giu.Where(g => ids.Contains(g.LuotGuiId)).GroupBy(g => g.LuotGuiId).ToDictionary(g => g.Key, g => g.Last()));
    public Task<ThongKeDinhVi> LayThongKeAsync(DateTime tu, DateTime den, CancellationToken ct) => Task.FromResult(new ThongKeDinhVi(0, 0, 0, 0, 0));
    public void Them(GiuChoKhu g) => _cho.Add(g);
    public void Them(ViTriXe v) => _cho.Add(v);
    public void Them(DiSaiKhu d) => _cho.Add(d);
    public Task LuuAsync(CancellationToken ct)
    {
        foreach (var o in _cho)
        {
            o.GetType().GetProperty("Id")!.SetValue(o, o is ViTriXe or DiSaiKhu ? (object)(long)++_id : ++_id);
            if (o is GiuChoKhu g) Giu.Add(g); else if (o is DiSaiKhu d) DiSai.Add(d); else ViTri.Add((ViTriXe)o);
        }
        _cho.Clear();
        return Task.CompletedTask;
    }
}

public class FakeCauHinhTimXe : ICauHinhTimXe
{
    public Task<int> LayAsync(string khoa, int macDinh, CancellationToken ct) => Task.FromResult(macDinh);
}

/// <summary>
/// Luồng xe máy chia khu (đề cương v10). Khu 3 "Khu C" (xe máy, 3 chỗ, cách cổng 35 m), khu 4 "Khu D" (xe máy, 1 chỗ, 8 m),
/// khu 1 "Khu A" (ô tô). Camera 4: lối vào Khu C; camera 5: lối vào Khu D; camera 1: lối vào Khu A.
/// Xe 2 "29B112345", xe 3 "29B198765" là xe máy; xe 1 "30A12345" ô tô. Người dùng xe 2: tài khoản 2 và 3.
/// </summary>
public class TimXeTests
{
    private readonly FakeClock _clock = new() { BayGio = new DateTime(2026, 11, 17, 8, 0, 0) };
    private readonly FakeBaiXeModule _bai = new();
    private readonly FakeCongRaVaoModule _cong = new();
    private readonly FakeGoiChoModule _goiCho = new();
    private readonly FakeTimXeRepository _repo = new();
    private readonly FakeThongBaoModule _tb = new();
    private readonly FakeAiBienSoModule _ai = new();
    private readonly FakeCauHinhTimXe _ch = new();

    public TimXeTests()
    {
        _bai.Khu.AddRange([new(1, "Khu A", 1, 4, 15), new(3, "Khu C", 2, 3, 35), new(4, "Khu D", 2, 1, 8)]);
        _bai.Camera.AddRange([new(4, 3, "CAM-C-LOI-VAO", "LOI_VAO_KHU", true), new(5, 4, "CAM-D-LOI-VAO", "LOI_VAO_KHU", true),
                              new(1, 1, "CAM-A-LOI-VAO", "LOI_VAO_KHU", true), new(9, 3, "CAM-C1", "GIAM_SAT", true)]);
        _goiCho.Xe[1] = new(1, "30A12345", 1, "O_TO", 2, _clock.BayGio.AddDays(9), 0, 0);
        _goiCho.Xe[2] = new(2, "29B112345", 2, "XE_MAY", 2, _clock.BayGio.AddDays(9), 0, 0);
        _goiCho.Xe[3] = new(3, "29B198765", 2, "XE_MAY", 4, _clock.BayGio.AddDays(9), 0, 0);
        _goiCho.NguoiDung[2] = new() { 2, 3 };
        _goiCho.NguoiDung[3] = new() { 4 };
        _goiCho.SucChua.AddRange([new(1, "O_TO", 4, 0, 0, 4), new(2, "XE_MAY", 4, 0, 0, 4)]);
    }

    private TinhChoKhu Tinh() => new(_bai, _cong, _repo);
    private NgheCongTimXe Nghe() => new(_goiCho, Tinh(), _repo, _ch, _tb, _clock);
    private XuLyCameraKhuUseCase Camera() => new(_bai, _cong, _goiCho, _ai, _repo, _ch, _tb, _clock);
    private DaDoXeUseCase DaDo() => new(_goiCho, _cong, _bai, _repo, _tb, _clock);
    private LayMucChoUseCase Muc() => new(Tinh(), _ch, _goiCho, _clock);

    private async Task VaoCong(int xeId, int luotId)
    {
        _cong.CoXeTrongBai(xeId, luotId);
        _cong.TheoXe[xeId] = _cong.TheoXe[xeId] with { ThoiGianVao = _clock.BayGio };
        await Nghe().XuLyAsync(new XeDaVao(luotId, xeId, _clock.BayGio), default);
    }

    [Fact]
    public async Task XeMayVaoCong_ChonKhuGanNhatConCho_GiuSuat5Phut_BaoNguoiDung()
    {
        await VaoCong(2, 10);
        var g = Assert.Single(_repo.Giu);
        Assert.Equal((4, _clock.BayGio.AddMinutes(5)), (g.KhuId, g.HetHanLuc)); // Khu D gần cổng nhất
        Assert.Equal(new int?[] { 2, 3 }, _tb.DaGui.Where(t => t.Loai == LoaiTB.Khu).Select(t => t.TaiKhoanId));
        Assert.Contains("mời vào Khu D", _tb.DaGui[0].NoiDung);

        // Khu D (1 chỗ) đang bị giữ: xe sau được mời sang Khu C
        await VaoCong(3, 11);
        Assert.Equal(3, _repo.Giu.Single(x => x.LuotGuiId == 11).KhuId);

        // sự kiện tới lần hai: không giữ thêm
        await Nghe().XuLyAsync(new XeDaVao(10, 2, _clock.BayGio), default);
        Assert.Equal(2, _repo.Giu.Count);
    }

    [Fact]
    public async Task KhuDangTat_KhongDuocChon_KhongHienChoNguoiDung_KhongBaoDaDoDuoc()
    {
        // Khu D gần cổng nhất nhưng tạm tắt (V009 tắt khu mẫu): xe được mời sang Khu C
        _bai.Khu[2] = _bai.Khu[2] with { DangDung = false };
        await VaoCong(2, 10);
        Assert.Equal(3, Assert.Single(_repo.Giu).KhuId);

        Assert.DoesNotContain(await Muc().ThucHienAsync(true), k => k.Id == 4);           // trang người dùng: ẩn
        Assert.False((await Muc().ThucHienAsync(false)).Single(k => k.Id == 4).DangDung);  // sơ đồ / Admin: còn, kèm cờ
        Assert.Equal("KHU_DANG_TAT", (await Assert.ThrowsAsync<LoiNghiepVu>(() => DaDo().ThucHienAsync(2, 2, 4))).Ma);
    }

    [Fact]
    public async Task MaSoDo_TraVeTrongMucCho()
    {
        _bai.Khu.Add(new(10, "Khu R", 2, 60, 40, "R"));
        Assert.Equal("R", (await Muc().ThucHienAsync(true)).Single(k => k.Id == 10).MaSoDo);
    }

    [Fact]
    public async Task OTo_ChuaLamPhanChiKhu()
    {
        await VaoCong(1, 10);
        Assert.Empty(_repo.Giu);
        Assert.Empty(_tb.DaGui);
    }

    [Fact]
    public async Task MoiKhuDay_BaoAdmin_KhongGiu()
    {
        _bai.Khu.Clear();
        _bai.Khu.Add(new(3, "Khu C", 2, 0, 35));
        await VaoCong(2, 10);
        Assert.Empty(_repo.Giu);
        Assert.Contains(_tb.DaGui, t => t.TaiKhoanId is null && t.Loai == LoaiTB.DoiSoat);
    }

    [Fact]
    public async Task CameraLoiVaoKhu_VaoRoiRa_ChongDocLap30Giay()
    {
        await VaoCong(2, 10);
        _clock.BayGio = _clock.BayGio.AddMinutes(2);

        var vao = await Camera().ThucHienAsync(5, "29B1-123.45", null, null);
        Assert.Equal((KetQuaCamera.DaGhi, HuongDi.Vao), (vao.KetQua, vao.Huong));
        Assert.Equal(4, _cong.TheoXe[2].ViTriKhuId);
        Assert.Equal(TrangThaiGiu.DaVao, _repo.Giu.Single().TrangThai);
        Assert.Equal(("CAMERA_KHU", 4), (_cong.DaGhiViTri.Last().Nguon, _cong.DaGhiViTri.Last().KhuId!.Value));

        _clock.BayGio = _clock.BayGio.AddSeconds(20);
        Assert.Equal(KetQuaCamera.BoQua, (await Camera().ThucHienAsync(5, "29B112345", null, null)).KetQua);

        _clock.BayGio = _clock.BayGio.AddSeconds(15);
        var ra = await Camera().ThucHienAsync(5, "29B112345", null, null);
        Assert.Equal(HuongDi.Ra, ra.Huong);
        Assert.Null(_cong.TheoXe[2].ViTriKhuId);
        Assert.Equal(3, _repo.ViTri.Count + 1); // 2 lần ghi (lần đọc lặp không ghi)
    }

    [Fact]
    public async Task DangOKhuKhac_ChuyenHanSangKhuMoi()
    {
        await VaoCong(2, 10);
        await Camera().ThucHienAsync(5, "29B112345", null, null);
        _clock.BayGio = _clock.BayGio.AddMinutes(3);
        var kq = await Camera().ThucHienAsync(4, "29B112345", null, null);
        Assert.Equal(HuongDi.Vao, kq.Huong);
        Assert.Equal(3, _cong.TheoXe[2].ViTriKhuId);
    }

    [Fact]
    public async Task AiDocLech1KyTu_KhopXeTrongBai_BienLa_BaoAdmin()
    {
        await VaoCong(2, 10);
        _ai.KetQua = new KetQuaDocBienSo("29B112346", .9, false, [new("29B112346", .9)]);
        var kq = await Camera().ThucHienAsync(4, null, [255, 216, 255], "image/jpeg");
        Assert.Equal(("29B112345", KetQuaCamera.DaGhi), (kq.BienSo, kq.KetQua));

        var la = await Camera().ThucHienAsync(4, "43B155555", null, null);
        Assert.Equal(KetQuaCamera.KhongKhop, la.KetQua);
        Assert.Contains(_tb.DaGui, t => t.TaiKhoanId is null && t.Loai == LoaiTB.DoiSoat && t.NoiDung.Contains("43B155555"));
    }

    [Fact]
    public async Task TuQuet_KhungKhongCoBien_KhongGhiKhongBao()
    {
        await VaoCong(2, 10);
        var truoc = _tb.DaGui.Count;
        _ai.KetQua = new KetQuaDocBienSo(null, 0, true, []);
        var kq = await Camera().ThucHienAsync(4, null, [255, 216, 255], "image/jpeg", default, tuDong: true);
        Assert.Equal(KetQuaCamera.KhongThayBien, kq.KetQua);
        Assert.Empty(_repo.ViTri);
        Assert.Equal(truoc, _tb.DaGui.Count);
    }

    [Fact]
    public async Task CameraHuongVao_CongMot_CameraHuongRa_TruMot()
    {
        _bai.Camera.AddRange([new(20, 3, "CAM-C-VAO", "KHU_VAO", true), new(21, 3, "CAM-C-RA", "KHU_RA", true)]);
        await VaoCong(2, 10);
        var tinh = Tinh();
        var cam = new XuLyCameraKhuUseCase(_bai, _cong, _goiCho, _ai, _repo, _ch, _tb, _clock, tinh);

        // camera hướng ra thấy xe chưa ghi trong khu: không trừ
        var raTruoc = await cam.ThucHienAsync(21, "29B112345", null, null);
        Assert.Equal((KetQuaCamera.BoQua, HuongDi.Ra), (raTruoc.KetQua, raTruoc.Huong));

        var vao = await cam.ThucHienAsync(20, "29B112345", null, null);
        Assert.Equal((KetQuaCamera.DaGhi, HuongDi.Vao, 1, "Khu C"), (vao.KetQua, vao.Huong, vao.SoXeTrongKhu, vao.Khu));
        Assert.Equal(3, _cong.TheoXe[2].ViTriKhuId);

        // camera hướng vào thấy lại (sau 30 giây): xe đã ở trong khu, không cộng lần hai
        _clock.BayGio = _clock.BayGio.AddMinutes(1);
        Assert.Equal(KetQuaCamera.BoQua, (await cam.ThucHienAsync(20, "29B112345", null, null)).KetQua);

        var ra = await cam.ThucHienAsync(21, "29B112345", null, null);
        Assert.Equal((KetQuaCamera.DaGhi, HuongDi.Ra, 0), (ra.KetQua, ra.Huong, ra.SoXeTrongKhu));
        Assert.Null(_cong.TheoXe[2].ViTriKhuId);
    }

    [Theory]
    [InlineData("KHU_VAO", 3, 3, "VAO")]
    [InlineData("KHU_RA", null, 3, "RA")]
    [InlineData("LOI_VAO_KHU", null, 3, "VAO")]
    [InlineData("LOI_VAO_KHU", 3, 3, "RA")]
    public void HuongTheoVaiTroCamera(string vaiTro, int? dangGhi, int khuCamera, string huong) =>
        Assert.Equal(huong, QuyTacKhu.HuongTheoCamera(vaiTro, dangGhi, khuCamera));

    [Fact]
    public async Task XeMayDiVaoKhuOTo_VanGhi_BaoAdmin()
    {
        await VaoCong(2, 10);
        var kq = await Camera().ThucHienAsync(1, "29B112345", null, null);
        Assert.Equal(KetQuaCamera.DaGhi, kq.KetQua);
        Assert.Contains(_tb.DaGui, t => t.TaiKhoanId is null && t.NoiDung.Contains("không dành cho loại xe này"));
    }

    [Fact]
    public async Task CameraGiamSat_KhongNhanAnhLoiVao()
    {
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => Camera().ThucHienAsync(9, "29B112345", null, null));
        Assert.Equal("KHONG_PHAI_CAMERA_KHU", loi.Ma);
    }

    [Fact]
    public async Task DaDoXe_ChuaCoKhu_GhiTheoNguoiDung_CameraDaGhiKhacThiTinCamera()
    {
        await VaoCong(2, 10);
        Assert.Equal("Đã ghi xe ở Khu C.", await DaDo().ThucHienAsync(3, 2, 3));
        Assert.Equal(("NGUOI_DUNG", 3), (_cong.DaGhiViTri.Last().Nguon, _cong.DaGhiViTri.Last().KhuId!.Value));

        _clock.BayGio = _clock.BayGio.AddMinutes(1);
        await Camera().ThucHienAsync(5, "29B112345", null, null); // camera thấy xe ở Khu D
        var kq = await DaDo().ThucHienAsync(2, 2, 3);
        Assert.Contains("giữ theo camera", kq);
        Assert.Equal(4, _cong.TheoXe[2].ViTriKhuId);
        Assert.Contains(_tb.DaGui, t => t.TaiKhoanId is null && t.NoiDung.Contains("camera đã ghi ở Khu D"));
    }

    [Fact]
    public async Task DaDoXe_NguoiKhongDungXe_KhongThayXe()
    {
        await VaoCong(2, 10);
        Assert.Equal("KHONG_TIM_THAY_XE", (await Assert.ThrowsAsync<LoiNghiepVu>(() => DaDo().ThucHienAsync(4, 2, 3))).Ma);
    }

    [Fact]
    public async Task SuatGiuQuaHan_TraLai_ChoConTangLai()
    {
        await VaoCong(2, 10);
        Assert.Equal(0, (await Muc().ThucHienAsync()).Single(k => k.Id == 4).ConLai);
        _clock.BayGio = _clock.BayGio.AddMinutes(5);
        Assert.Equal(1, await new HetHanGiuChoUseCase(_repo, _clock).ThucHienAsync());
        Assert.Equal(1, (await Muc().ThucHienAsync()).Single(k => k.Id == 4).ConLai);
    }

    [Fact]
    public async Task XeRaCong_HuySuatDangGiu()
    {
        await VaoCong(2, 10);
        await Nghe().XuLyAsync(new XeDaRa(10, 2, _clock.BayGio.AddMinutes(1)), default);
        Assert.Equal(TrangThaiGiu.Huy, _repo.Giu.Single().TrangThai);
    }

    [Fact]
    public async Task AdminGanVaGoKhu_SoXeKhuCapNhat()
    {
        await VaoCong(2, 10);
        var uc = new LaySoXeKhuUseCase(Muc(), _clock);
        var gan = new AdminGanKhuUseCase(_cong, _bai, _repo, _clock);
        await gan.ThucHienAsync(new GanKhuLenh(10, 3));
        Assert.Equal(3, _cong.TheoXe[2].ViTriKhuId);
        var khu = await uc.ThucHienAsync();
        Assert.Equal(1, khu.Khu.Single(k => k.Id == 3).SoXe);
        Assert.Contains(khu.Khu, k => k.Id == 1); // sơ đồ có cả khu ô tô
        await gan.ThucHienAsync(new GanKhuLenh(10, null));
        Assert.Null(_cong.TheoXe[2].ViTriKhuId);
        Assert.Equal(0, (await uc.ThucHienAsync()).Khu.Single(k => k.Id == 3).SoXe);
    }

    [Fact]
    public async Task TimXeCuaToi_ChiThayXeMinhDuocDung_KemKhuVaDuongDi()
    {
        await VaoCong(2, 10);
        var uc = new TimXeCuaToiUseCase(_goiCho, _cong, _bai, _repo, Muc(), _clock);
        _goiCho.TaiCong["29B112345"] = new XeTaiCong(2, "29B112345", "XE_MAY", 2, true, true, null, 0);
        var truoc = (await uc.ThucHienAsync(3)).Xe.Single();
        Assert.Equal(("Khu D", (int?)null), (truoc.MoiVao!.Khu, truoc.KhuId));
        Assert.NotNull(truoc.Duong);

        await Camera().ThucHienAsync(4, "29B112345", null, null);
        var sau = (await uc.ThucHienAsync(3)).Xe.Single();
        Assert.Equal(("Khu C", "CAMERA_KHU"), (sau.Khu, sau.NguonKhu));
        Assert.Null(sau.MoiVao);
        Assert.Empty((await uc.ThucHienAsync(4)).Xe); // tài khoản 4 không được dùng xe 2
    }

    [Theory]
    [InlineData(10, 3, "CON_NHIEU")]
    [InlineData(10, 2, "SAP_DAY")]
    [InlineData(10, 0, "DAY")]
    [InlineData(0, 0, "DAY")]
    public void MucCho_Theo20PhanTram(int sucChua, int conLai, string muc) => Assert.Equal(muc, QuyTacKhu.Muc(sucChua, conLai, 20));
}
