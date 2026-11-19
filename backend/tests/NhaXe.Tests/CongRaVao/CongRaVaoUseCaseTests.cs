using NhaXe.Modules.AiBienSo.Contracts;
using NhaXe.Modules.CongRaVao.Application;
using NhaXe.Modules.CongRaVao.Contracts;
using NhaXe.Modules.CongRaVao.Domain;
using NhaXe.Modules.GoiCho.Contracts;
using NhaXe.SharedKernel;
using NhaXe.Tests.BaiXe;
using LoaiTB = NhaXe.Modules.ThongBao.Contracts.LoaiThongBao;
using NhaXe.Modules.ThongBao.Contracts;

namespace NhaXe.Tests.CongRaVao;

/// <summary>
/// Test use case cổng theo đề cương v10 với GoiCho, ThongBao, kho dữ liệu đều là bản giả.
/// Xe 1 "30A12345" (ô tô): chủ là tài khoản 2, chia sẻ cho 3. Xe 4 "51G67890": còn nợ phí quá hạn. Admin là 1.
/// Xe 2 "29B112345" (xe máy) hết gói. Xe 5 "59C112233" chưa duyệt. Xe 6 "29B198765" xe máy còn gói.
/// </summary>
public class CongRaVaoUseCaseTests
{
    private readonly FakeClock _clock = new() { BayGio = new DateTime(2026, 11, 11, 8, 0, 0) };
    private readonly FakeGoiChoModule _goiCho = new();
    private readonly FakeCongRaVaoRepository _repo = new();
    private readonly FakePhatSuKien _phat = new();
    private readonly FakeThongBaoModule _tb = new();
    private readonly FakeTaiKhoanModule _tk = new();
    private readonly FakeAiBienSoModule _ai = new();
    private static readonly byte[] AnhJpg = [255, 216, 255, 224];

    public CongRaVaoUseCaseTests()
    {
        _goiCho.TaiCong["30A12345"] = new XeTaiCong(1, "30A12345", "O_TO", 2, true, true, _clock.BayGio.AddDays(20), 0);
        _goiCho.TaiCong["29B112345"] = new XeTaiCong(2, "29B112345", "XE_MAY", 2, true, false, _clock.BayGio.AddDays(-1), 0);
        _goiCho.TaiCong["59C112233"] = new XeTaiCong(5, "59C112233", "O_TO", 5, false, false, null, 0);
        _goiCho.TaiCong["51G67890"] = new XeTaiCong(4, "51G67890", "O_TO", 4, true, false, _clock.BayGio.AddDays(-2), 100000);
        _goiCho.TaiCong["29B198765"] = new XeTaiCong(6, "29B198765", "XE_MAY", 2, true, true, _clock.BayGio.AddDays(9), 0);
        _goiCho.NguoiDung[1] = new() { 2, 3 };
        _goiCho.NguoiDung[4] = new() { 4 };
        foreach (var x in _goiCho.TaiCong.Values)
            _goiCho.Xe[x.XeId] = new TinhTrangXeGoi(x.XeId, x.BienSo, 1, x.LoaiXe, x.ChuXeId, x.HetHanLuc, 0, x.SoTienNo);
    }

    private NhanDienBangAi Ai() => new(_ai, new FakeCauHinhCong { GiayAi = 1 });
    private XeVaoUseCase Vao() => new(Ai(), _goiCho, _repo, _phat, _tb, _clock);
    private KetThucYeuCau KetThuc(NhaXe.Modules.ThongBao.Contracts.IThongBaoModule? tb = null) => new(_repo, _goiCho, _tk, tb ?? _tb, _phat, _clock);
    private XeRaUseCase Ra() => new(Ai(), _goiCho, _repo, _repo, new FakeCauHinhCong(), KetThuc(), _tb, _clock);
    private NguoiDungXeXacNhanUseCase NguoiDung() => new(_repo, _goiCho, _tk, KetThuc(), _clock);
    private AdminMoTayUseCase MoTay() => new(_repo, _goiCho, _tb, _phat, _clock);
    private async Task<int> RaVaTaoYeuCau(string bien = "30A12345") => (await Ra().ThucHienAsync(new AnhCong(bien))).YeuCauId!.Value;

    // ------------------------------ Cổng vào ------------------------------

    [Fact]
    public async Task Vao_BangAnh_AiChac_ChoQuaVaGhiDoTinCay()
    {
        _ai.KetQua = new KetQuaDocBienSo("30A12345", .97, false, [new("30A12345", .97)]);
        var kq = await Vao().ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg"));
        Assert.True(kq.ChoQua); Assert.Equal("30A12345", kq.BienSo); Assert.Equal(.97m, kq.DoTinCay);
        Assert.Single(_repo.Luot);
    }

    // ---- Ảnh đối chứng và barie (module Hành trình xe) ----

    [Fact]
    public async Task Vao_ChoQua_LuuAnhDoiChung_Ra_LuuAnhRa_XemLaiDuoc()
    {
        var kho = new FakeKhoAnhCong();
        _ai.KetQua = new KetQuaDocBienSo("30A12345", .97, false, [new("30A12345", .97)]);
        var vao = await new XeVaoUseCase(Ai(), _goiCho, _repo, _phat, _tb, _clock, kho: kho).ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg"));
        Assert.True(vao.CoAnhVao); Assert.Equal(_clock.BayGio, vao.ThoiGianVao);
        Assert.NotNull(_repo.Luot.Single().AnhVao);

        _clock.BayGio = _clock.BayGio.AddHours(2);
        var ra = await new XeRaUseCase(Ai(), _goiCho, _repo, _repo, new FakeCauHinhCong(), KetThuc(), _tb, _clock, kho: kho)
            .ThucHienAsync(new AnhCong(null, [1, 2, 3], "image/jpeg"));
        Assert.True(ra.DangChoXacNhan); Assert.True(ra.CoAnhVao);
        Assert.Equal(_clock.BayGio.AddHours(-2), ra.ThoiGianVao);

        var luot = await new LayLuotCongUseCase(_repo).ThucHienAsync(vao.LuotGuiId!.Value);
        Assert.True(luot.CoAnhVao); Assert.True(luot.CoAnhRa); Assert.Equal(TrangThaiLuot.ChoXacNhan, luot.TrangThai);
        await using var anhRa = await new LayAnhDoiChungUseCase(_repo, kho).ThucHienAsync(luot.Id, "ra");
        Assert.Equal(3, anhRa!.Length);
        Assert.Null(await new LayAnhDoiChungUseCase(_repo, kho).ThucHienAsync(luot.Id, "khac"));
    }

    [Fact]
    public async Task Vao_TuChoi_KhongLuuAnh()
    {
        var kho = new FakeKhoAnhCong();
        _ai.KetQua = new KetQuaDocBienSo("29B112345", .95, false, [new("29B112345", .95)]); // hết gói
        var kq = await new XeVaoUseCase(Ai(), _goiCho, _repo, _phat, _tb, _clock, kho: kho).ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg"));
        Assert.False(kq.ChoQua); Assert.Empty(kho.Anh);
    }

    /// <summary>Lỗi thật 03:42: camera tự quét, mỗi khung AI đọc lệch 1 ký tự nhưng khớp gần đúng về cùng xe hết gói.</summary>
    [Fact]
    public async Task Vao_TuDong_DocLech1KyTu_CungXe_Trong30Giay_ChiTuChoiVaBao1Lan()
    {
        _goiCho.NguoiDung[2] = new() { 2 };
        var docLech = new[] { "29B112345", "29B112346", "29B112348", "29B112345" };
        foreach (var b in docLech)
        {
            _ai.KetQua = new KetQuaDocBienSo(b, .95, false, [new(b, .95)]);
            await Vao().ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg", TuDong: true));
            _clock.BayGio = _clock.BayGio.AddSeconds(1.5);
        }
        Assert.Single(_repo.SuKien);
        Assert.Single(_tb.DaGui, t => t.Loai == LoaiTB.HetHan);

        _clock.BayGio = _clock.BayGio.AddSeconds(30); // xe còn đứng đó sau 30 giây: báo lại một lần
        _ai.KetQua = new KetQuaDocBienSo("29B112346", .95, false, [new("29B112346", .95)]);
        await Vao().ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg", TuDong: true));
        Assert.Equal(2, _repo.SuKien.Count);
    }

    [Fact]
    public async Task Barie_MoTheoSuKienXeDaVao_XeDaRa()
    {
        var barie = new FakeBarie();
        await new MoBarieKhiXeVao(barie).XuLyAsync(new XeDaVao(7, 1, _clock.BayGio), default);
        await new MoBarieKhiXeRa(barie).XuLyAsync(new XeDaRa(7, 1, _clock.BayGio), default);
        Assert.Equal([("VAO", 7), ("RA", 7)], barie.DaMo);
    }

    [Fact]
    public async Task Ra_TuDong_KhongThayBien_BoQua()
    {
        _ai.KetQua = new KetQuaDocBienSo(null, 0, true, []);
        var kq = await Ra().ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg", 1, TuDong: true));
        Assert.True(kq.KhongThayBien); Assert.Empty(_repo.SuKien);
    }

    // ---- Màn hình camera tự quét (tuDong) ----

    [Fact]
    public async Task Vao_TuDong_KhongThayBien_BoQua_KhongGhiNhatKy()
    {
        _ai.KetQua = new KetQuaDocBienSo(null, 0, true, []);
        var kq = await Vao().ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg", 1, TuDong: true));
        Assert.True(kq.KhongThayBien); Assert.False(kq.ChoQua);
        Assert.Empty(_repo.SuKien); Assert.Empty(_repo.Luot);
    }

    [Fact]
    public async Task Vao_KhongTuDong_KhongThayBien_VanGhiTuChoi()
    {
        _ai.KetQua = new KetQuaDocBienSo(null, 0, true, []);
        var kq = await Vao().ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg"));
        Assert.False(kq.KhongThayBien);
        Assert.Single(_repo.SuKien);
    }

    [Fact]
    public async Task Vao_TuDong_CungBienTrong30Giay_BoQua_Sau30GiayXuLyLai()
    {
        _ai.KetQua = new KetQuaDocBienSo("29B112345", .95, false, [new("29B112345", .95)]); // xe máy hết gói → từ chối
        var lan1 = await Vao().ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg", 1, TuDong: true));
        Assert.False(lan1.KhongThayBien); Assert.Contains("hết hạn", lan1.ThongDiep);

        _clock.BayGio = _clock.BayGio.AddSeconds(10);
        var lan2 = await Vao().ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg", 1, TuDong: true));
        Assert.True(lan2.KhongThayBien);
        Assert.Single(_repo.SuKien); // không thêm dòng "từ chối" mỗi giây

        _clock.BayGio = _clock.BayGio.AddSeconds(25);
        var lan3 = await Vao().ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg", 1, TuDong: true));
        Assert.False(lan3.KhongThayBien);
        Assert.Equal(2, _repo.SuKien.Count);
    }

    [Fact]
    public async Task Vao_BangAnh_TraKhungBienDeVeOVuong()
    {
        var khung = new KhungBien(.34, .5, .27, .25);
        _ai.KetQua = new KetQuaDocBienSo("29B198765", .96, false, [new("29B198765", .96, khung)], "XE_MAY");
        var kq = await Vao().ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg", 1, TuDong: true));
        Assert.True(kq.ChoQua);
        Assert.Equal(khung, kq.KhungBien);
    }

    [Fact]
    public async Task Vao_BangAnh_DuoiNguong_KhongMo_TraUngVienDeAdminChon()
    {
        _ai.KetQua = new KetQuaDocBienSo(null, .62, true, [new("30A12345", .62), new("30A12346", .55)]);
        var kq = await Vao().ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg"));
        Assert.False(kq.ChoQua); Assert.Null(kq.BienSo); Assert.True(kq.CanAdmin);
        Assert.Equal(["30A12345", "30A12346"], kq.UngVien!);
        Assert.Contains("0,85", kq.ThongDiep);
        Assert.Empty(_repo.Luot);
    }

    [Fact]
    public async Task Vao_AiLoi_ChuyenCheDoThuCong()
    {
        _ai.Loi = true;
        var kq = await Vao().ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg"));
        Assert.True(kq.CheDoThuCong); Assert.False(kq.ChoQua);
        Assert.Equal(NhanDienBangAi.LyDoThuCong, kq.ThongDiep);
    }

    [Fact]
    public async Task Vao_AiQuaThoiGian_ChuyenCheDoThuCong()
    {
        _ai.KetQua = new KetQuaDocBienSo("30A12345", .97, false, [new("30A12345", .97)]);
        _ai.Cham = TimeSpan.FromSeconds(3); // giới hạn trong test là 1 giây
        var kq = await Vao().ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg"));
        Assert.True(kq.CheDoThuCong);
        Assert.Empty(_repo.Luot);
    }

    [Fact]
    public async Task Vao_GoTayKemAnh_UuTienBienGoTay_GhiAdminVaoNhatKy()
    {
        _ai.KetQua = new KetQuaDocBienSo("51G67890", .99, false, [new("51G67890", .99)]);
        var kq = await Vao().ThucHienAsync(new AnhCong("30A-123.45", AnhJpg, "image/jpeg", AdminId: 1));
        Assert.True(kq.ChoQua); Assert.Equal("30A12345", kq.BienSo);
        var sk = Assert.Single(_repo.SuKien);
        Assert.Equal(1, sk.AdminId); Assert.NotNull(sk.LyDo);
    }

    [Fact]
    public async Task Vao_AiDocLech1KyTu_MotUngVien_KhopGanDung_ChoQua()
    {
        _ai.KetQua = new KetQuaDocBienSo("30A12346", .93, false, [new("30A12346", .93)]);
        var kq = await Vao().ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg"));
        Assert.True(kq.ChoQua); Assert.True(kq.KhopGanDung);
        Assert.Equal("30A12345", kq.BienSo);
    }

    [Fact]
    public async Task Vao_AiDocGanGiongNhieuXe_AdminChon()
    {
        _goiCho.TaiCong["30A12347"] = new XeTaiCong(7, "30A12347", "O_TO", 7, true, true, _clock.BayGio.AddDays(5), 0);
        _ai.KetQua = new KetQuaDocBienSo("30A12346", .93, false, [new("30A12346", .93)]);
        var kq = await Vao().ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg"));
        Assert.False(kq.ChoQua);
        Assert.Equal(2, kq.UngVien!.Count);
    }

    [Fact]
    public async Task Vao_LoaiBienKhongTrungLoaiXe_AdminXacNhan()
    {
        // AI đọc biển xe máy (6 số sau seri) nhưng biển đã đăng ký là ô tô
        _goiCho.TaiCong["30A123456"] = new XeTaiCong(8, "30A123456", "O_TO", 8, true, true, _clock.BayGio.AddDays(5), 0);
        _ai.KetQua = new KetQuaDocBienSo("30A123456", .95, false, [new("30A123456", .95)], "XE_MAY");
        var kq = await Vao().ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg"));
        Assert.False(kq.ChoQua);
        Assert.Contains("không trùng loại xe", kq.ThongDiep);
        Assert.Equal(["30A123456"], kq.UngVien!);
    }

    [Theory]
    [InlineData("43A99999", "Xe chưa đăng ký")]
    [InlineData("59C-112.233", "Xe chưa được duyệt")]
    [InlineData("12", "Biển số sai định dạng")]
    [InlineData("", "Không đọc được biển số")]
    public async Task XeVao_KhongHopLe_TuChoi_VanGhiNhatKy(string bienSo, string lyDo)
    {
        var kq = await Vao().ThucHienAsync(new AnhCong(bienSo));
        Assert.False(kq.ChoQua);
        Assert.Equal(lyDo, kq.ThongDiep);
        var sk = Assert.Single(_repo.SuKien);
        Assert.Equal((SuKienCong.Vao, SuKienCong.TuChoi, lyDo), (sk.Loai, sk.KetQua, sk.LyDo));
        Assert.Empty(_repo.Luot);
        Assert.Empty(_phat.DaPhat);
    }

    [Fact]
    public async Task XeVao_HetGoi_LyDoGhiRoNgayHetHan_GuiLoiMoiGiaHan_KemMuaGoiVaXeId()
    {
        _goiCho.NguoiDung[2] = new() { 2 };
        var kq = await Vao().ThucHienAsync(new AnhCong("29B1-123.45"));
        var ngay = _clock.BayGio.AddDays(-1).ToString("dd/MM/yyyy");
        Assert.False(kq.ChoQua);
        Assert.Equal($"Gói đã hết hạn ngày {ngay}", kq.ThongDiep);
        var tb = Assert.Single(_tb.DaGui, t => t.Loai == LoaiTB.HetHan);
        Assert.Contains($"gói đã hết hạn ngày {ngay}", tb.NoiDung);
        Assert.Equal(new DichThongBao(HanhDongThongBao.MuaGoi, 2), tb.Dich);
    }

    [Fact]
    public async Task XeVao_ChuaTungMuaGoi_LyDoXeChuaCoGoi()
    {
        _goiCho.TaiCong["29B155555"] = new XeTaiCong(7, "29B155555", "XE_MAY", 2, true, false, null, 0);
        _goiCho.NguoiDung[7] = new() { 2 };
        var kq = await Vao().ThucHienAsync(new AnhCong("29B155555"));
        Assert.Equal("Xe chưa có gói", kq.ThongDiep);
        Assert.Contains(_tb.DaGui, t => t.Loai == LoaiTB.HetHan && t.NoiDung.Contains("chưa có gói") && t.Dich == new DichThongBao(HanhDongThongBao.MuaGoi, 7));
    }

    [Theory]
    [InlineData(null, "Xe chưa có gói")]
    [InlineData(-3, "Gói đã hết hạn ngày")]
    [InlineData(5, "Gói chưa tới ngày bắt đầu")]
    public void LyDoKhongCoGoi_TachTheoLyDoThat(int? soNgay, string batDau)
    {
        var bayGio = new DateTime(2026, 11, 19, 3, 42, 0);
        Assert.StartsWith(batDau, NoiDungCong.LyDoKhongCoGoi(soNgay is int n ? bayGio.AddDays(n) : null, bayGio));
    }

    [Fact]
    public async Task XeVao_HopLe_MoLuotGui_PhatXeDaVao_BaoChuXeVaNguoiCungNhom_LanHaiBaoAdmin()
    {
        var kq = await Vao().ThucHienAsync(new AnhCong("30A-123.45"));

        Assert.True(kq.ChoQua);
        var luot = Assert.Single(_repo.Luot);
        Assert.Equal((1, TrangThaiLuot.DangGui), (luot.XeId, luot.TrangThai));
        Assert.Equal(luot.Id, Assert.Single(_repo.SuKien).LuotGuiId);
        Assert.Equal(1, _repo.SoLanLuu);
        Assert.Equal(new XeDaVao(luot.Id, 1, _clock.BayGio), Assert.Single(_phat.DaPhat));
        Assert.Equal(new int?[] { 2, 3 }, _tb.DaGui.Where(t => t.Loai == LoaiTB.Vao).Select(t => t.TaiKhoanId));

        var lan2 = await Vao().ThucHienAsync(new AnhCong("30A12345"));
        Assert.False(lan2.ChoQua);
        Assert.StartsWith("Xe này đang có lượt gửi chưa kết thúc", lan2.ThongDiep);
        Assert.Contains(_tb.DaGui, t => t.TaiKhoanId is null && t.Loai == LoaiTB.DoiSoat);
    }

    [Fact]
    public async Task Vao_ThongBaoLoi_XeVanDuocVao_KhongNemLoi()
    {
        var uc = new XeVaoUseCase(Ai(), _goiCho, _repo, _phat, new ThongBaoHong(), _clock);
        var kq = await uc.ThucHienAsync(new AnhCong("30A12345"));
        Assert.True(kq.ChoQua);
        Assert.Single(_repo.Luot);
    }

    private sealed class ThongBaoHong : NhaXe.Modules.ThongBao.Contracts.IThongBaoModule
    {
        public Task GuiAsync(int taiKhoanId, string loai, string noiDung, DichThongBao? dich = null, CancellationToken ct = default) => throw new InvalidOperationException("hỏng");
        public Task GuiChoMoiAdminAsync(string loai, string noiDung, DichThongBao? dich = null, CancellationToken ct = default) => throw new InvalidOperationException("hỏng");
        public Task GuiNhieuAsync(IEnumerable<int> ids, string loai, string noiDung, DichThongBao? dich = null, CancellationToken ct = default) => throw new InvalidOperationException("hỏng");
    }

    // ------------------------------ Cổng ra ------------------------------

    [Fact]
    public async Task XeRa_TaoYeuCau60Giay_GuiMoiNguoiDungXe_QuetLaiKhongGuiThem()
    {
        var luot = _repo.CoLuot(1, _clock.BayGio.AddHours(-2));
        var kq = await Ra().ThucHienAsync(new AnhCong("30A12345"));

        Assert.True(kq.DangChoXacNhan); Assert.False(kq.ChoQua);
        Assert.Equal(60, kq.SoGiayConLai);
        Assert.Equal("Đang chờ xác nhận", kq.ThongDiep);
        Assert.Equal(TrangThaiLuot.ChoXacNhan, luot.TrangThai);
        Assert.Equal(2, _tb.DaGui.Count(t => t.Loai == LoaiTB.Ra)); // chủ xe và người cùng nhóm
        // bấm thông báo → trang Ra vào, đúng yêu cầu của lượt này
        Assert.All(_tb.DaGui.Where(t => t.Loai == LoaiTB.Ra), t => Assert.Equal(new DichThongBao(HanhDongThongBao.XacNhanRa, luot.Id), t.Dich));

        _clock.BayGio = _clock.BayGio.AddSeconds(10);
        var lai = await Ra().ThucHienAsync(new AnhCong("30A12345"));
        Assert.Equal(kq.YeuCauId, lai.YeuCauId);
        Assert.Equal(50, lai.SoGiayConLai);
        Assert.Single(_repo.YeuCau);
        Assert.Equal(2, _tb.DaGui.Count(t => t.Loai == LoaiTB.Ra));
    }

    [Fact]
    public async Task XeRa_KhongThayLuot_KhopGanDungVoiXeTrongBai()
    {
        _repo.CoLuot(1, _clock.BayGio.AddHours(-2));
        var kq = await Ra().ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg") with { });
        Assert.False(kq.DangChoXacNhan); // không ảnh đọc được: AI giả trả null

        _ai.KetQua = new KetQuaDocBienSo("30A12845", .9, false, [new("30A12845", .9)]);
        var gan = await Ra().ThucHienAsync(new AnhCong(null, AnhJpg, "image/jpeg"));
        Assert.True(gan.DangChoXacNhan); Assert.True(gan.KhopGanDung);
        Assert.Equal("30A12345", gan.BienSo);
    }

    [Fact]
    public async Task XeRa_KhongCoLuotGuiNao_BaoAdmin()
    {
        var kq = await Ra().ThucHienAsync(new AnhCong("30A12345"));
        Assert.False(kq.ChoQua); Assert.True(kq.CanAdmin);
        Assert.Contains(_tb.DaGui, t => t.TaiKhoanId is null && t.Loai == LoaiTB.DoiSoat);
    }

    [Fact]
    public async Task NguoiCungNhomTraLoiTruoc_MoCongNgay_KhongCanAdmin_NguoiSauThayDaDuocXacNhan()
    {
        var luot = _repo.CoLuot(1, _clock.BayGio.AddHours(-2));
        luot.GhiViTri(3, NguonViTri.CameraKhu, _clock.BayGio.AddHours(-1));
        var yc = await RaVaTaoYeuCau();

        _clock.BayGio = _clock.BayGio.AddSeconds(8);
        await NguoiDung().ThucHienAsync(3, yc, true); // người cùng nhóm xác nhận hộ

        Assert.Equal(TrangThaiLuot.DaRa, luot.TrangThai);
        Assert.Null(luot.ViTriKhuId);                  // xe ra thì xoá khu
        Assert.Contains(new XeDaRa(luot.Id, 1, _clock.BayGio), _phat.DaPhat);
        var ra = _repo.SuKien.Single(s => s.Loai == SuKienCong.Ra);
        Assert.Equal((SuKienCong.ChoQua, (int?)null, "Đã được TK3 xác nhận."), (ra.KetQua, ra.AdminId, ra.LyDo));
        Assert.Contains(_tb.DaGui, t => t.TaiKhoanId == 2 && t.NoiDung.Contains("đã được TK3 xác nhận ra bãi"));

        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => NguoiDung().ThucHienAsync(2, yc, true));
        Assert.Equal(("DA_CO_NGUOI_TRA_LOI", "Đã được TK3 xác nhận."), (loi.Ma, loi.Message));
    }

    [Fact]
    public async Task DongY_ConNo_ChoThanhToan_TraPhiXongThiMoCong()
    {
        var luot = _repo.CoLuot(4, _clock.BayGio.AddDays(-5));
        var yc = await RaVaTaoYeuCau("51G67890"); // còn nợ vẫn tạo yêu cầu (v10: xét phí sau khi xác nhận)
        await NguoiDung().ThucHienAsync(4, yc, true);

        Assert.Equal(TrangThaiLuot.ChoThanhToan, luot.TrangThai);
        Assert.DoesNotContain(_phat.DaPhat, e => e is XeDaRa);
        Assert.Contains(_tb.DaGui, t => t.TaiKhoanId == 4 && t.Loai == LoaiTB.ChoThanhToan && t.NoiDung.Contains("100.000"));

        // quét lại khi chưa trả: vẫn chờ
        var cho = await Ra().ThucHienAsync(new AnhCong("51G67890"));
        Assert.True(cho.ChoThanhToan); Assert.False(cho.ChoQua);

        // GoiCho báo đã trả phí → mở cổng
        _goiCho.No[4] = 0;
        await new NgheDaTraPhi(_repo, _goiCho, KetThuc()).XuLyAsync(new DaTraPhiQuaHan(luot.Id, 4, 4, _clock.BayGio), default);
        Assert.Equal(TrangThaiLuot.DaRa, luot.TrangThai);
        Assert.Contains(_phat.DaPhat, e => e is XeDaRa);
    }

    [Fact]
    public async Task NguoiKhongDungXe_KhongXacNhanDuoc()
    {
        _repo.CoLuot(1, _clock.BayGio.AddHours(-2));
        var yc = await RaVaTaoYeuCau();
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => NguoiDung().ThucHienAsync(4, yc, true));
        Assert.Equal("KHONG_TIM_THAY_YEU_CAU", loi.Ma);
    }

    [Fact]
    public async Task KhongPhaiToi_LuotThanhNghiMat_BaoMoiAdmin()
    {
        var luot = _repo.CoLuot(1, _clock.BayGio.AddHours(-2));
        var yc = await RaVaTaoYeuCau();
        await NguoiDung().ThucHienAsync(2, yc, false);

        Assert.Equal(TrangThaiLuot.NghiMat, luot.TrangThai);
        Assert.Contains(_tb.DaGui, t => t.TaiKhoanId is null && t.Loai == LoaiTB.NghiMat);
        Assert.Equal(SuKienCong.TuChoi, _repo.SuKien.Last().KetQua);
        Assert.DoesNotContain(_phat.DaPhat, e => e is XeDaRa);
    }

    [Fact]
    public async Task Het60Giay_KhongMoCong_ChuyenAdmin_AdminMoTayCoLyDo()
    {
        var luot = _repo.CoLuot(1, _clock.BayGio.AddHours(-2));
        await RaVaTaoYeuCau();

        _clock.BayGio = _clock.BayGio.AddSeconds(30);
        Assert.Equal(0, await new HetHanXacNhanUseCase(_repo, KetThuc(), _clock).ThucHienAsync());
        _clock.BayGio = _clock.BayGio.AddSeconds(31);
        Assert.Equal(1, await new HetHanXacNhanUseCase(_repo, KetThuc(), _clock).ThucHienAsync());

        Assert.Equal(KetQuaXacNhan.HetHan, _repo.YeuCau.Single().KetQua);
        Assert.Equal(TrangThaiLuot.DangGui, luot.TrangThai);
        Assert.Contains(_tb.DaGui, t => t.TaiKhoanId is null && t.NoiDung.Contains("quá 60 giây không ai xác nhận")
                                        && t.Dich == new DichThongBao(HanhDongThongBao.XuLyCongRa, luot.Id));

        var thieu = await Assert.ThrowsAsync<LoiNghiepVu>(() => MoTay().ThucHienAsync(1, luot.Id, " "));
        Assert.Equal("THIEU_LY_DO", thieu.Ma);
        var kq = await MoTay().ThucHienAsync(1, luot.Id, "Đã gọi chủ xe, đúng người");
        Assert.True(kq.ChoQua);
        Assert.Equal(TrangThaiLuot.DaRa, luot.TrangThai);
        var sk = _repo.SuKien.Last();
        Assert.Equal((SuKienCong.ChoQua, (int?)1), (sk.KetQua, sk.AdminId));
        Assert.StartsWith("Admin mở tay", sk.LyDo);
    }

    [Fact]
    public async Task AdminMoTay_KhiYeuCauConCho_DongYeuCau()
    {
        var luot = _repo.CoLuot(1, _clock.BayGio.AddHours(-2));
        await RaVaTaoYeuCau();
        await MoTay().ThucHienAsync(1, luot.Id, "App người dùng lỗi");
        var y = _repo.YeuCau.Single();
        Assert.Equal((KetQuaXacNhan.DongY, (int?)1), (y.KetQua, y.QuanLyId));
    }

    [Fact]
    public async Task HaiNguoiBamCungLuc_NguoiLuuSauThuLai_ThayDaCoNguoiTraLoi()
    {
        _repo.CoLuot(1, _clock.BayGio.AddHours(-2));
        var yc = await RaVaTaoYeuCau();
        _repo.SoLanXungDot = 1;
        await NguoiDung().ThucHienAsync(2, yc, false);
        Assert.Equal(1, _repo.SoLanBoTheoDoi);
        Assert.Equal(KetQuaXacNhan.TuChoi, _repo.YeuCau.Single().KetQua);
    }

    [Fact]
    public async Task DongY_ThongBaoLoi_VanGhiXeRa_KhongNemLoi()
    {
        var luot = _repo.CoLuot(1, _clock.BayGio.AddHours(-2));
        var yc = await RaVaTaoYeuCau();
        await new NguoiDungXeXacNhanUseCase(_repo, _goiCho, _tk, KetThuc(new ThongBaoHong()), _clock).ThucHienAsync(2, yc, true);
        Assert.Equal(TrangThaiLuot.DaRa, luot.TrangThai);
        Assert.Contains(_phat.DaPhat, e => e is XeDaRa);
    }
}
