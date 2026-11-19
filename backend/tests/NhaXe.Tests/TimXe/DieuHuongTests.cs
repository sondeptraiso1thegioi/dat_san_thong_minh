using NhaXe.Modules.BaiXe.Application;
using NhaXe.Modules.BaiXe.Contracts;
using NhaXe.Modules.BaiXe.Domain;
using NhaXe.Modules.CongRaVao.Contracts;
using NhaXe.Modules.GoiCho.Contracts;
using NhaXe.Modules.TimXe.Application;
using NhaXe.Modules.TimXe.Domain;
using NhaXe.SharedKernel;
using NhaXe.Tests.BaiXe;
using LoaiTB = NhaXe.Modules.ThongBao.Contracts.LoaiThongBao;

namespace NhaXe.Tests.TimXe;

/// <summary>
/// Điều hướng trong bãi (mục 2.6): chọn khu, đổi khu đích, đi sai khu, kết thúc phiên qua camera khu / hết hạn / ra cổng,
/// Dijkstra trên đồ thị mốc thật (do-thi-ban-do.json).
/// Bãi giả: Khu R (id 10, 10 chỗ, 50 m), Hầm (12, 20 chỗ, 90 m), Khu S (11, 5 chỗ, 120 m). Camera VÀO = 100 + id, RA = 200 + id.
/// Xe 2 "29B112345" xe máy của tài khoản 2; xe 1 "30A12345" ô tô của tài khoản 2.
/// </summary>
public class DieuHuongTests
{
    private const int R = 10, S = 11, HAM = 12;
    private readonly FakeClock _clock = new() { BayGio = new DateTime(2026, 11, 18, 8, 0, 0) };
    private readonly FakeBaiXeModule _bai = new();
    private readonly FakeCongRaVaoModule _cong = new();
    private readonly FakeGoiChoModule _goiCho = new();
    private readonly FakeTimXeRepository _repo = new();
    private readonly FakeThongBaoModule _tb = new();
    private readonly FakeCauHinhTimXe _ch = new();

    public DieuHuongTests()
    {
        _bai.Khu.AddRange([new(R, "Khu R", 2, 10, 50, "R"), new(S, "Khu S", 2, 5, 120, "S"), new(HAM, "Hầm xe máy B1/B2", 2, 20, 90, "HAM"),
                           new(1, "Khu A", 1, 4, null)]);
        foreach (var k in new[] { R, S, HAM })
            _bai.Camera.AddRange([new(100 + k, k, $"CAM-{k}-VAO", "KHU_VAO", true), new(200 + k, k, $"CAM-{k}-RA", "KHU_RA", true)]);
        var han = _clock.BayGio.AddDays(9);
        _goiCho.Xe[1] = new(1, "30A12345", 1, "O_TO", 2, han, 0, 0);
        _goiCho.Xe[2] = new(2, "29B112345", 2, "XE_MAY", 2, han, 0, 0);
        _goiCho.NguoiDung[1] = new() { 2 };
        _goiCho.NguoiDung[2] = new() { 2 };
        _goiCho.TaiCong["30A12345"] = new XeTaiCong(1, "30A12345", "O_TO", 2, true, true, han, 0);
        _goiCho.TaiCong["29B112345"] = new XeTaiCong(2, "29B112345", "XE_MAY", 2, true, true, han, 0);
        _goiCho.SucChua.AddRange([new(1, "O_TO", 4, 0, 0, 4), new(2, "XE_MAY", 35, 0, 0, 35)]);
    }

    private TinhChoKhu Tinh() => new(_bai, _cong, _repo);
    private NgheCongTimXe Nghe() => new(_goiCho, Tinh(), _repo, _ch, _tb, _clock);
    private XuLyCameraKhuUseCase Camera() => new(_bai, _cong, _goiCho, new FakeAiBienSoModule(), _repo, _ch, _tb, _clock);
    private LayMucChoUseCase Muc() => new(Tinh(), _ch, _goiCho, _clock);
    private LayDieuHuongCuaToiUseCase CuaToi() => new(_goiCho, _cong, _bai, _repo, Muc(), _clock);
    private DoiKhuDichUseCase DoiKhu() => new(_goiCho, _cong, _repo, Muc(), CuaToi(), _clock);
    private Task<PhienDieuHuongDto?> Phien() => CuaToi().ThucHienAsync(2).ContinueWith(t => t.Result.Phien);

    private async Task VaoCong(int xeId, int luotId)
    {
        _cong.CoXeTrongBai(xeId, luotId);
        _cong.TheoXe[xeId] = _cong.TheoXe[xeId] with { ThoiGianVao = _clock.BayGio };
        await Nghe().XuLyAsync(new XeDaVao(luotId, xeId, _clock.BayGio), default);
    }

    /// <summary>Thêm n xe "khác" đang đỗ ở khu (chỉ để đếm số xe, không khớp biển).</summary>
    private void XeKhacDangDo(int khuId, int n)
    {
        for (var i = 0; i < n; i++) _cong.CoXeTrongBai(5000 + khuId * 100 + i, 9000 + khuId * 100 + i, khuId);
    }

    // ---------------- 2.1 Chọn khu ----------------

    [Fact]
    public void ChonKhu_GanNhatTrongCacKhuConNhieu()
    {
        var ds = new QuyTacKhu.UngVienKhu[] { new(HAM, 20, 15, 90), new(R, 10, 9, 50), new(S, 5, 5, 120) };
        Assert.Equal(R, QuyTacKhu.ChonKhu(ds));
    }

    [Fact]
    public void ChonKhu_KhuGanNhatChiSapDay_ChonKhuConNhieuXaHon()
    {
        // R gần nhất nhưng chỉ còn 2/10 (= 20%: Sắp đầy) → bỏ qua, lấy Hầm (Còn nhiều)
        var ds = new QuyTacKhu.UngVienKhu[] { new(R, 10, 2, 50), new(HAM, 20, 15, 90) };
        Assert.Equal(HAM, QuyTacKhu.ChonKhu(ds));
    }

    [Fact]
    public void ChonKhu_KhongKhuNaoConNhieu_ChonKhuConNhieuChoNhat()
    {
        var ds = new QuyTacKhu.UngVienKhu[] { new(R, 100, 10, 50), new(S, 100, 20, 120), new(HAM, 100, 15, 90) };
        Assert.Equal(S, QuyTacKhu.ChonKhu(ds)); // cả ba ≤ 20%: S còn nhiều chỗ nhất dù xa nhất
    }

    [Fact]
    public async Task ChonKhu_TatCaDay_KhongChiDuong_BaoNguoiDungHetCho()
    {
        XeKhacDangDo(R, 10); XeKhacDangDo(S, 5); XeKhacDangDo(HAM, 20);
        await VaoCong(2, 10);
        Assert.Empty(_repo.Giu);
        Assert.Contains(_tb.DaGui, t => t.TaiKhoanId == 2 && t.NoiDung.Contains("bãi xe máy đã hết chỗ"));
        var du = await CuaToi().ThucHienAsync(2);
        Assert.Null(du.Phien);
        Assert.Equal("29B112345", du.HetCho!.BienSo);
    }

    [Fact]
    public async Task ChonKhu_CoTruSuatDangGiu_XeVaoCungLucKhongDonVaoKhuConMotCho()
    {
        _bai.Khu[0] = _bai.Khu[0] with { SucChua = 2 };    // R: 2 chỗ
        XeKhacDangDo(R, 1);                                 // còn 1 chỗ (50%: Còn nhiều, gần nhất)
        _goiCho.Xe[3] = new(3, "29B198765", 2, "XE_MAY", 4, _clock.BayGio.AddDays(9), 0, 0);
        await VaoCong(2, 10);
        await VaoCong(3, 11);
        Assert.Equal(R, _repo.Giu.Single(g => g.LuotGuiId == 10).KhuId);
        Assert.Equal(HAM, _repo.Giu.Single(g => g.LuotGuiId == 11).KhuId); // R còn 1 chỗ nhưng đã giữ cho xe trước
    }

    [Fact]
    public async Task OTo_VaoCong_KhongTaoPhienDieuHuong()
    {
        await VaoCong(1, 10);
        Assert.Empty(_repo.Giu);
        Assert.Null(await Phien());
    }

    // ---------------- 2.2 Dijkstra trên đồ thị mốc thật ----------------

    [Fact]
    public void DoThiThat_DuongToiHam_DiTrucChinhQuaX1VaH1_RoiReVaoLoiHam()
    {
        var g = DoThiBanDo.MacDinh;
        var (nut, canh) = g.ChoBaiXe(new Dictionary<string, int> { ["R"] = R, ["S"] = S, ["HAM"] = HAM });
        var cong = nut[0];
        Assert.Equal("Cổng vào X", cong.Ten);
        var (kc, truoc) = DuongNganNhat.Tinh(cong.Id, canh);
        string[] Ma(int khu) => DuongNganNhat.Duong(cong.Id, nut.Single(n => n.KhuId == khu).Id, truoc)!
            .Select(id => g.CacNut.Single(n => n.Id == id).Ma).ToArray();
        Assert.Equal(["CONG_X", "NGA_BA_X1", "GOC_TRUC", "TRUC_GIUA", "RE_HAM", "LOI_HAM", "KHU_HAM"], Ma(HAM));
        Assert.Equal(["CONG_X", "NGA_BA_X1", "GOC_TREN", "DUONG_R", "DAU_R", "KHU_R"], Ma(R));
        Assert.Equal("KHU_S", Ma(S)[^1]);
        double Met(int khu) => kc[nut.Single(n => n.KhuId == khu).Id];
        Assert.True(Met(R) < Met(HAM) && Met(HAM) < Met(S), $"R {Met(R)} m, Hầm {Met(HAM)} m, S {Met(S)} m");
        Assert.All(nut, n => Assert.InRange(n.X, 0, 1));
    }

    [Fact]
    public void DoThi_JsonSai_BaoLoiRo()
    {
        Assert.Throws<InvalidOperationException>(() => DoThiBanDo.Doc("""{"anh":{"rong":1,"cao":1,"metMoiPx":1},"nut":[{"ma":"A","x":2,"y":0}],"canh":[]}"""));
        Assert.Throws<InvalidOperationException>(() => DoThiBanDo.Doc("""{"anh":{"rong":1,"cao":1,"metMoiPx":1},"nut":[{"ma":"A","x":0,"y":0}],"canh":[["A","B"]]}"""));
    }

    // ---------------- 2.3 Kết thúc qua camera khu ----------------

    [Fact]
    public async Task CameraVaoDungKhuDich_DaToi_TatChiDuong()
    {
        await VaoCong(2, 10);
        var dang = (await Phien())!;
        Assert.Equal((TrangThaiPhien.DangDan, R, R), (dang.TrangThai, dang.KhuDich.Id, dang.KhuGoiYId));
        Assert.NotNull(dang.Duong);

        var kq = await Camera().ThucHienAsync(100 + R, "29B112345", null, null);
        Assert.Equal(KetQuaCamera.DaGhi, kq.KetQua);
        var p = (await Phien())!;
        Assert.Equal((TrangThaiPhien.DaToi, R, (DateTime?)_clock.BayGio), (p.TrangThai, p.KhuThucTe!.Id, (DateTime?)p.KhuThucTe.VaoLuc));
        Assert.Null(p.Duong);
        Assert.Contains(_tb.DaGui, t => t.TaiKhoanId == 2 && t.Loai == LoaiTB.Khu && t.NoiDung.StartsWith("Đã đến bãi đỗ thành công"));
        Assert.Empty(_repo.DiSai);
    }

    [Fact]
    public async Task CameraVaoKhuKhacConCho_ToiKhuKhac_ChapNhan_NhaSuat_GhiLanDiSai()
    {
        await VaoCong(2, 10);
        var kq = await Camera().ThucHienAsync(100 + HAM, "29B112345", null, null);
        Assert.Equal(KetQuaCamera.DaGhi, kq.KetQua);
        Assert.Equal(HAM, _cong.TheoXe[2].ViTriKhuId);                     // tin camera: xe ở Hầm (+1 ở Hầm)
        var p = (await Phien())!;
        Assert.Equal((TrangThaiPhien.ToiKhuKhac, HAM, R), (p.TrangThai, p.KhuThucTe!.Id, p.KhuDich.Id));
        var muc = await Muc().ThucHienAsync();
        Assert.Equal((0, 10), (muc.Single(k => k.Id == R).SoGiu, muc.Single(k => k.Id == R).ConLai)); // nhả suất ở R
        var sai = Assert.Single(_repo.DiSai);
        Assert.Equal((R, HAM, false, 2), (sai.KhuChiDinhId, sai.KhuThucTeId, sai.KhuDay, sai.ChuXeId));
        Assert.Contains(_tb.DaGui, t => t.TaiKhoanId == 2 && t.NoiDung.Contains("khác khu được chỉ định Khu R"));
        Assert.Equal(1, (await new LayDiSaiKhuUseCase(_repo, _bai).ThucHienAsync()).TheoChuXe.Single(d => d.ChuXeId == 2).SoLan);
    }

    [Fact]
    public async Task CameraVaoKhuDaDay_KhongCong1_CanhBaoAdmin_PhienVanDanToiKhuDich()
    {
        XeKhacDangDo(S, 5);                     // S đầy
        await VaoCong(2, 10);                   // được chỉ R
        var kq = await Camera().ThucHienAsync(100 + S, "29B112345", null, null);

        Assert.Equal((KetQuaCamera.KhuDay, 5), (kq.KetQua, kq.SoXeTrongKhu));
        Assert.Null(_cong.TheoXe[2].ViTriKhuId);          // camera KHÔNG +1
        Assert.Empty(_repo.ViTri);
        var p = (await Phien())!;
        Assert.Equal((TrangThaiPhien.DangDan, R, S), (p.TrangThai, p.KhuDich.Id, p.CanhBao!.KhuId));
        Assert.Contains(_tb.DaGui, t => t.TaiKhoanId == 2 && t.NoiDung == "Khu S đã đầy, mời bạn sang Khu R. Mở bản đồ để xem đường đi.");
        var admin = Assert.Single(_tb.DaGui, t => t.TaiKhoanId is null);
        Assert.Equal(LoaiTB.DoiSoat, admin.Loai);
        Assert.Contains("29B112345", admin.NoiDung); Assert.Contains("Khu R", admin.NoiDung); Assert.Contains("Khu S", admin.NoiDung);
        var sai = Assert.Single(_repo.DiSai);
        Assert.Equal((R, S, true), (sai.KhuChiDinhId, sai.KhuThucTeId, sai.KhuDay));

        // camera tự quét mỗi giây: trong 30 giây không báo lại
        var soTb = _tb.DaGui.Count;
        _clock.BayGio = _clock.BayGio.AddSeconds(10);
        Assert.Equal(KetQuaCamera.KhuDay, (await Camera().ThucHienAsync(100 + S, "29B112345", null, null)).KetQua);
        Assert.Equal((soTb, 1), (_tb.DaGui.Count, _repo.DiSai.Count));
    }

    [Fact]
    public async Task KhuChiConDungSuatCuaChinhXe_VanChoVao()
    {
        _bai.Khu[0] = _bai.Khu[0] with { SucChua = 2 };
        XeKhacDangDo(R, 1);                     // R: 1 xe + suất của xe 2 = đầy với xe khác, nhưng chỗ đó là của xe 2
        await VaoCong(2, 10);
        Assert.Equal(KetQuaCamera.DaGhi, (await Camera().ThucHienAsync(100 + R, "29B112345", null, null)).KetQua);
        Assert.Equal(TrangThaiPhien.DaToi, (await Phien())!.TrangThai);
    }

    [Fact]
    public async Task QuaHan_HetHan_NhaSuat_BaoNguoiDungMotLan()
    {
        await VaoCong(2, 10);
        _clock.BayGio = _clock.BayGio.AddMinutes(5);
        Assert.Equal(TrangThaiPhien.HetHan, (await Phien())!.TrangThai); // chưa chạy tác vụ nền cũng đã thấy HET_HAN
        var uc = new HetHanGiuChoUseCase(_repo, _clock, _cong, _goiCho, _tb, _bai);
        Assert.Equal(1, await uc.ThucHienAsync());
        Assert.Equal(0, await uc.ThucHienAsync()); // lần hai (hoặc bản API thứ hai) không lấy lại
        Assert.Single(_tb.DaGui, t => t.TaiKhoanId == 2 && t.NoiDung.Contains("chưa xác nhận được vị trí xe"));
        Assert.Null(_cong.TheoXe[2].ViTriKhuId);                       // không tự gán khu
        Assert.Equal(10, (await Muc().ThucHienAsync()).Single(k => k.Id == R).ConLai);
    }

    [Fact]
    public async Task XeRaCongKhiDangDan_DongPhien_NhaSuat()
    {
        await VaoCong(2, 10);
        await Nghe().XuLyAsync(new XeDaRa(10, 2, _clock.BayGio.AddMinutes(1)), default);
        var g = _repo.Giu.Single();
        Assert.Equal(TrangThaiPhien.DaDong, TrangThaiPhien.Tu(g, _clock.BayGio));
        Assert.Equal(0, (await Muc().ThucHienAsync()).Single(k => k.Id == R).SoGiu);
    }

    // ---------------- 2.4 Người dùng đổi khu đích ----------------

    [Fact]
    public async Task DoiKhuConCho_SuatChuyenSangKhuMoi_GiuNguyenHan_CameraKhuMoi_DaToi()
    {
        await VaoCong(2, 10);
        var han = _repo.Giu.Single().HetHanLuc;
        _clock.BayGio = _clock.BayGio.AddMinutes(2);
        var p = await DoiKhu().ThucHienAsync(2, HAM);
        Assert.Equal((HAM, R, han, TrangThaiPhien.DangDan), (p.KhuDich.Id, p.KhuGoiYId, p.HetHanLuc, p.TrangThai));
        Assert.NotNull(p.Duong);
        var muc = await Muc().ThucHienAsync();
        Assert.Equal((0, 1), (muc.Single(k => k.Id == R).SoGiu, muc.Single(k => k.Id == HAM).SoGiu));

        await Camera().ThucHienAsync(100 + HAM, "29B112345", null, null);
        Assert.Equal(TrangThaiPhien.DaToi, (await Phien())!.TrangThai); // khu tự chọn là khu đích chính thức
        Assert.Empty(_repo.DiSai);
    }

    [Fact]
    public async Task DoiKhuDaDay_BiTuChoi()
    {
        XeKhacDangDo(S, 5);
        await VaoCong(2, 10);
        Assert.Equal("KHU_DA_DAY", (await Assert.ThrowsAsync<LoiNghiepVu>(() => DoiKhu().ThucHienAsync(2, S))).Ma);
        Assert.Equal(R, _repo.Giu.Single().KhuId);
        Assert.Equal("KHONG_TIM_THAY_KHU", (await Assert.ThrowsAsync<LoiNghiepVu>(() => DoiKhu().ThucHienAsync(2, 999))).Ma);
        Assert.Equal("SAI_LOAI_KHU", (await Assert.ThrowsAsync<LoiNghiepVu>(() => DoiKhu().ThucHienAsync(2, 1))).Ma); // khu ô tô
    }

    [Fact]
    public async Task DoiKhuKhiPhienKhongConDangDan_BiTuChoi()
    {
        await VaoCong(2, 10);
        await Camera().ThucHienAsync(100 + R, "29B112345", null, null); // đã tới
        Assert.Equal("PHIEN_DA_KET_THUC", (await Assert.ThrowsAsync<LoiNghiepVu>(() => DoiKhu().ThucHienAsync(2, HAM))).Ma);

        var g = GiuChoKhu.Tao(1, R, _clock.BayGio, 5);
        Assert.Equal("PHIEN_DA_KET_THUC", Assert.Throws<LoiNghiepVu>(() => g.DoiKhuDich(HAM, _clock.BayGio.AddMinutes(6))).Ma); // quá hạn
    }

    [Fact]
    public async Task DuongDiChiDoc_KhongTaoPhien()
    {
        var d = await new LayDuongDiUseCase(_bai).ThucHienAsync(HAM);
        Assert.Equal(HAM, d.DenKhuId);
        Assert.Empty(_repo.Giu);
    }
}
