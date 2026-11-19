using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NhaXe.Modules.GoiCho.Application;
using NhaXe.Modules.GoiCho.Domain;
using NhaXe.Modules.TaiKhoan.Application;
using NhaXe.Modules.TaiKhoan.Contracts;
using NhaXe.SharedKernel;
using GoiChoEntity = NhaXe.Modules.GoiCho.Domain.GoiCho;
using NhaXe.Modules.ThongBao.Contracts;
using LoaiTB = NhaXe.Modules.ThongBao.Contracts.LoaiThongBao;
using TK = NhaXe.Modules.TaiKhoan.Domain.TaiKhoan;

namespace NhaXe.Tests.GoiCho;

/// <summary>
/// Truy vấn giả trong bộ nhớ, cùng ngữ nghĩa với SQL thật (XeQuery): tìm "chứa" trên biển đã chuẩn hoá, lọc theo trạng thái gói
/// (hạn xa nhất của gói không huỷ, &gt; 72 giờ: còn hạn, ≤ 72 giờ: sắp hết, còn lại: chưa có), phân trang theo id giảm dần.
/// </summary>
public class FakeXeQuery : IXeQuery
{
    public DateTime BayGio { get; set; } = new(2026, 11, 18, 8, 0, 0);
    public List<(Xe Xe, List<DangKyChoDto> Goi)> DaDuyet { get; } = new();
    public int ChoDuyet { get; set; }
    public string? QNhan { get; private set; }
    public string? LocNhan { get; private set; }

    private string TrangThai(List<DangKyChoDto> goi)
    {
        var het = goi.Where(g => g.TrangThai != "HUY").Select(g => (DateTime?)g.NgayKetThuc).Max();
        return het > BayGio.AddHours(72) ? "CON_HAN" : het > BayGio ? "SAP_HET" : "CHUA_CO";
    }

    public Task<(int Tong, IReadOnlyList<XeQuanLyDto> Ds)> TimXeDaDuyetAsync(string q, string loc, int boQua, int lay, CancellationToken ct)
    {
        QNhan = q; LocNhan = loc;
        var khop = DaDuyet.Where(d => d.Xe.BienSo.Contains(q) && (loc == "TAT_CA" || TrangThai(d.Goi) == loc))
            .OrderByDescending(d => d.Xe.Id).ToList();
        var ds = khop.Skip(boQua).Take(lay).Select(d => new XeQuanLyDto
        {
            XeId = d.Xe.Id, BienSo = d.Xe.BienSo, LoaiXeId = d.Xe.LoaiXeId, ChuXeId = d.Xe.ChuXeId, TrangThaiGoi = TrangThai(d.Goi),
        }).ToList();
        return Task.FromResult<(int, IReadOnlyList<XeQuanLyDto>)>((khop.Count, ds));
    }

    public Task<IReadOnlyList<DangKyChoDto>> LayLichSuGoiAsync(int xeId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DangKyChoDto>>(DaDuyet.FirstOrDefault(d => d.Xe.Id == xeId).Goi ?? new List<DangKyChoDto>());
    public Task<int> DemChoDuyetAsync(CancellationToken ct) => Task.FromResult(ChoDuyet);

    public Task<IReadOnlyList<XeDuocDungDto>> LayXeDuocDungAsync(int taiKhoanId, IReadOnlyCollection<int> nguoiCungNhom, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<ChoConBanDto>> LayChoConBanAsync(CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<TinhTrangXeRow>> LayTinhTrangXeAsync(IReadOnlyCollection<int> xeIds, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<XeQuanLyDto>> LayXeDaDuyetAsync(CancellationToken ct) => throw new NotSupportedException();
    public Task<XeTaiCongRow?> TimXeTaiCongAsync(string bienSoChuan, CancellationToken ct) => throw new NotSupportedException();
}

public class QuanLyXeTests
{
    private static readonly DateTime T0 = new(2026, 11, 18, 8, 0, 0);
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };

    private readonly FakeXeRepository _xe = new();
    private readonly FakeThongBaoModule _tb = new();
    private readonly FakeClock _clock = new() { BayGio = T0 };

    private TaiAnhGiayDangKyUseCase TaiAnh() =>
        new(_xe, new FakeLuuTruTep(), new FakeCauHinh(), new FakeGoiChoRepository(), new FakeTaiKhoanModule(), _tb);

    private static DangKyChoDto Goi(int id, DateTime bd, DateTime kt, string tt = "CON_HAN", string ten = "1 tháng", decimal tien = 100_000) =>
        new() { Id = id, TenGoi = ten, NgayBatDau = bd, NgayKetThuc = kt, SoTien = tien, TrangThai = tt, MuaLuc = bd };

    // ---------------- Thông báo ----------------

    [Fact]
    public async Task ThemXeKemAnh_AdminCoDung1ThongBao_DoiAnhKhiVanChoDuyet_KhongBaoLan2()
    {
        _xe.Ds.Add(Xe.DungLai(9, 4, 2, "29A12345", TrangThaiDuyet.ChoDuyet));
        await TaiAnh().ThucHienAsync(4, 9, new MemoryStream(Png), Png.Length);
        var tb = Assert.Single(_tb.DaGui);
        Assert.Equal((null, LoaiTB.XeChoDuyet), (tb.TaiKhoanId, tb.Loai));
        Assert.Equal("Có xe mới chờ duyệt: 29A12345 (xe máy) của TK4.", tb.NoiDung);
        Assert.Equal(new DichThongBao(HanhDongThongBao.DuyetXe, 9), tb.Dich); // Admin bấm → tab Chờ duyệt, đúng xe 9

        await TaiAnh().ThucHienAsync(4, 9, new MemoryStream(Png), Png.Length); // thay ảnh, xe vẫn đang chờ
        Assert.Single(_tb.DaGui);
    }

    [Fact]
    public async Task GuiLaiAnhSauKhiBiTuChoi_AdminCoThongBaoMoi()
    {
        _xe.Ds.Add(Xe.DungLai(9, 4, 2, "29A12345", TrangThaiDuyet.TuChoi, "giay-dk/cu.png", "Ảnh mờ"));
        await TaiAnh().ThucHienAsync(4, 9, new MemoryStream(Png), Png.Length);
        var tb = Assert.Single(_tb.DaGui);
        Assert.Equal(LoaiTB.XeChoDuyet, tb.Loai);
        Assert.Contains("gửi lại", tb.NoiDung);
    }

    [Fact]
    public async Task Duyet_GhiNguoiDuyetVaLuc_ChuXeCoThongBao()
    {
        _xe.Ds.Add(Xe.DungLai(9, 4, 2, "29A12345", TrangThaiDuyet.ChoDuyet, "giay-dk/1.png"));
        await new DuyetXeUseCase(_xe, _clock, _tb).ThucHienAsync(1, 9, true);
        Assert.Equal((TrangThaiDuyet.DaDuyet, (DateTime?)T0, (int?)1), (_xe.Ds[0].TrangThaiDuyet, _xe.Ds[0].DuyetLuc, _xe.Ds[0].DuyetBoiId));
        var tb = Assert.Single(_tb.DaGui);
        Assert.Equal((4, LoaiTB.KetQuaDuyet, "Xe 29A12345 đã được duyệt, bạn có thể mua gói."), (tb.TaiKhoanId!.Value, tb.Loai, tb.NoiDung));
        Assert.Equal(new DichThongBao(HanhDongThongBao.MuaGoi, 9), tb.Dich); // chủ xe bấm → Mua gói, chọn sẵn xe 9
    }

    [Fact]
    public async Task TuChoi_ChuXeNhanLyDo_KhongLyDoThiLoiVaKhongBao()
    {
        _xe.Ds.Add(Xe.DungLai(9, 4, 2, "29A12345", TrangThaiDuyet.ChoDuyet, "giay-dk/1.png"));
        var uc = new DuyetXeUseCase(_xe, _clock, _tb);
        Assert.Equal("THIEU_LY_DO", (await Assert.ThrowsAsync<LoiNghiepVu>(() => uc.ThucHienAsync(1, 9, false, "  "))).Ma);
        Assert.Empty(_tb.DaGui);

        await uc.ThucHienAsync(1, 9, false, "Ảnh mờ, không đọc được");
        Assert.Null(_xe.Ds[0].DuyetLuc);
        var tb = Assert.Single(_tb.DaGui);
        Assert.StartsWith("Xe 29A12345 bị từ chối: Ảnh mờ, không đọc được.", tb.NoiDung);
        Assert.Equal(new DichThongBao(HanhDongThongBao.GuiLaiGiay, 9), tb.Dich);
    }

    [Fact]
    public async Task DuyetHangLoat_MoiXeDuyetDuocDeuBaoChuXe()
    {
        _xe.Ds.Add(Xe.DungLai(1, 4, 2, "29A10001", TrangThaiDuyet.ChoDuyet, "giay-dk/1.png"));
        _xe.Ds.Add(Xe.DungLai(2, 5, 2, "29A10002", TrangThaiDuyet.ChoDuyet)); // thiếu ảnh: lỗi, không báo
        await new DuyetHangLoatUseCase(_xe, _clock, _tb).ThucHienAsync(7, new DuyetHangLoatLenh(new[] { 1, 2 }, true, null));
        Assert.Equal(new int?[] { 4 }, _tb.DaGui.Select(t => t.TaiKhoanId));
        Assert.Equal((int?)7, _xe.Ds[0].DuyetBoiId);
    }

    // ---------------- Chặn mua gói ----------------

    [Theory]
    [InlineData(TrangThaiDuyet.ChoDuyet, "XE_CHUA_DUYET")]
    [InlineData(TrangThaiDuyet.TuChoi, "XE_BI_TU_CHOI")]
    [InlineData(TrangThaiDuyet.BiGo, "XE_BI_GO")]
    public async Task MuaGoi_XeChuaDuyetTuChoiHoacBiGo_BiChanMaLoiRo_KhongThuTien(string trangThai, string ma)
    {
        _xe.Ds.Add(Xe.DungLai(5, 4, 2, "29A12345", trangThai, "giay-dk/1.png", "Ảnh mờ"));
        var goi = new FakeGoiChoRepository(); goi.Ds.Add(new GoiChoEntity(4, 2, "1 tuần", 7, 30000));
        var tt = new FakeThanhToan();
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => new MuaGoiUseCase(_xe, goi, tt, new FakeDangKyChoStore()).ThucHienAsync(4, new MuaGoiLenh(5, 4)));
        Assert.Equal(ma, loi.Ma);
        Assert.Equal(0, tt.SoLanThu);
    }

    // ---------------- Tìm kiếm, lọc, phân trang ----------------

    [Theory]
    [InlineData("29a-1", "29A1")]
    [InlineData("29A.12", "29A12")]
    [InlineData(" 29 a 1 ", "29A1")]
    [InlineData("29A-123.45", "29A12345")]
    [InlineData("%_'; DROP", "DROP")] // ký tự đặc biệt của LIKE / SQL bị bỏ
    [InlineData(null, "")]
    public void ChuanHoaChuoiTim_GiongCachLuuBienSo(string? nhap, string mongDoi) => Assert.Equal(mongDoi, BienSo.ChuanHoaTim(nhap));

    private FakeXeQuery QueryCo(int soXe)
    {
        var q = new FakeXeQuery { BayGio = T0 };
        q.DaDuyet.Add((Xe.DungLai(1, 4, 2, "29A12345", TrangThaiDuyet.DaDuyet), new() { Goi(1, T0.AddDays(-5), T0.AddDays(25)) }));
        for (var i = 2; i <= soXe; i++)
            q.DaDuyet.Add((Xe.DungLai(i, 5, 2, $"59C{10000 + i}", TrangThaiDuyet.DaDuyet), new()));
        return q;
    }

    private static TimXeDaDuyetUseCase Tim(FakeXeQuery q) => new(q, new FakeTaiKhoanModule(), TaoModule.TraCuu());

    [Fact]
    public async Task Tim29a1_RaXe29A12345_ChuoiKhongCo_DanhSachRong()
    {
        var q = QueryCo(5);
        var kq = await Tim(q).ThucHienAsync("29a-1", "TAT_CA");
        Assert.Equal("29A1", q.QNhan);
        Assert.Equal((1, "29A12345", "TK4"), (kq.Tong, Assert.Single(kq.Ds).BienSo, kq.Ds[0].TenChuXe));

        var rong = await Tim(q).ThucHienAsync("99Z", null);
        Assert.Equal(0, rong.Tong);
        Assert.Empty(rong.Ds);
    }

    [Fact]
    public async Task PhanTrang_DungTong_TrangCuoiConPhanDu()
    {
        var q = QueryCo(45);
        var t1 = await Tim(q).ThucHienAsync(null, null, 1, 20);
        var t3 = await Tim(q).ThucHienAsync(null, null, 3, 20);
        Assert.Equal((45, 20), (t1.Tong, t1.Ds.Count));
        Assert.Equal((45, 5, 3), (t3.Tong, t3.Ds.Count, t3.Trang));
        Assert.Empty((await Tim(q).ThucHienAsync(null, null, 9, 20)).Ds); // quá trang cuối
        Assert.Equal(100, (await Tim(q).ThucHienAsync(null, null, 1, 5000)).SoDong); // chặn tối đa 100 xe / trang
    }

    [Fact]
    public async Task LocTheoTrangThaiGoi_VaBoLocSaiBaoLoi()
    {
        var q = QueryCo(3);
        q.DaDuyet.Add((Xe.DungLai(4, 5, 2, "30F55555", TrangThaiDuyet.DaDuyet), new() { Goi(4, T0.AddDays(-29), T0.AddHours(30)) }));
        Assert.Equal("29A12345", Assert.Single((await Tim(q).ThucHienAsync(null, "con_han")).Ds).BienSo);
        Assert.Equal("30F55555", Assert.Single((await Tim(q).ThucHienAsync(null, "SAP_HET")).Ds).BienSo);
        Assert.Equal(2, (await Tim(q).ThucHienAsync(null, "CHUA_CO")).Tong);
        Assert.Equal("BO_LOC_SAI", (await Assert.ThrowsAsync<LoiNghiepVu>(() => Tim(q).ThucHienAsync(null, "XYZ"))).Ma);
    }

    // ---------------- Chi tiết xe ----------------

    [Fact]
    public void GoiHienTai_ConHan_DaHet_ChuaMua()
    {
        var con = GoiHienTai.Tinh(new[] { Goi(1, T0.AddDays(-10), T0.AddDays(20).AddHours(3)) }, T0, 0)!;
        Assert.Equal((true, 21, 0), (con.ConHan, con.SoNgayConLai, con.SoNgayQuaHan));

        var het = GoiHienTai.Tinh(new[] { Goi(1, T0.AddDays(-40), T0.AddDays(-10)), Goi(2, T0.AddDays(-70), T0.AddDays(-40)) }, T0, 30_000)!;
        Assert.Equal((1, false, 0, 10, 30_000m), (het.DangKyChoId, het.ConHan, het.SoNgayConLai, het.SoNgayQuaHan, het.PhiQuaHan));

        Assert.Null(GoiHienTai.Tinh(Array.Empty<DangKyChoDto>(), T0, 0));
        // gói bị huỷ không tính là gói hiện tại
        Assert.Null(GoiHienTai.Tinh(new[] { Goi(1, T0.AddDays(-1), T0.AddDays(6), "HUY") }, T0, 0));
    }

    [Fact]
    public async Task ChiTietXe_DuThongTin_ChuXeKhongCoThongTinNhayCam()
    {
        var q = new FakeXeQuery { BayGio = T0 };
        var lichSu = new List<DangKyChoDto> { Goi(2, T0.AddDays(-3), T0.AddDays(27)), Goi(1, T0.AddDays(-33), T0.AddDays(-3)) };
        var xe = Xe.DungLai(9, 4, 2, "29A12345", TrangThaiDuyet.DaDuyet, "giay-dk/1.png", chiaSeNhom: true, duyetLuc: T0.AddDays(-40), duyetBoiId: 1, taoLuc: T0.AddDays(-41));
        q.DaDuyet.Add((xe, lichSu));
        _xe.Ds.Add(xe);
        var cong = new FakeCongRaVaoModule().CoXeTrongBai(9, 300);
        var kq = await new LayChiTietXeAdminUseCase(_xe, q, new FakeTaiKhoanModule(), TaoModule.TraCuu(cong), _clock).ThucHienAsync(9);

        Assert.Equal(("29A12345", true, (DateTime?)T0.AddDays(-40), "TK1"), (kq.Xe.BienSo, kq.Xe.ChiaSeNhom, kq.Xe.DuyetLuc, kq.Xe.TenAdminDuyet));
        Assert.Equal(("TK4", "0900000004", "tk4@vi-du.vn"), (kq.ChuXe.HoTen, kq.ChuXe.Sdt, kq.ChuXe.Email));
        Assert.True(kq.TrongBai.DangTrongBai);
        Assert.Equal((2, true, 27), (kq.GoiHienTai!.DangKyChoId, kq.GoiHienTai.ConHan, kq.GoiHienTai.SoNgayConLai));
        Assert.Equal(2, kq.LichSuGoi.Count);
        Assert.DoesNotContain(typeof(ChuXeAdminDto).GetProperties(), p => p.Name.Contains("MatKhau") || p.Name.Contains("Hash"));
    }

    [Fact]
    public async Task ChiTietXe_XeKhongTonTai_KhongTimThay() =>
        Assert.Equal("KHONG_TIM_THAY_XE", (await Assert.ThrowsAsync<LoiNghiepVu>(() =>
            new LayChiTietXeAdminUseCase(_xe, new FakeXeQuery(), new FakeTaiKhoanModule(), TaoModule.TraCuu(), _clock).ThucHienAsync(404))).Ma);
}

/// <summary>Người dùng thường (vai trò USER) gọi API Admin → 403; chưa đăng nhập → 401. Không cần CSDL.</summary>
public class QuanLyXeQuyenTests(WebApplicationFactory<Program> f) : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient Client(out ITokenService token)
    {
        var app = f.WithWebHostBuilder(b =>
        {
            b.UseSetting("environment", "Development");
            b.ConfigureTestServices(s => s.AddScoped<ITaiKhoanModule, FakeTaiKhoanModule>()); // phiên luôn hợp lệ, không hỏi DB
        });
        token = app.Services.GetRequiredService<ITokenService>();
        return app.CreateClient();
    }

    [Theory]
    [InlineData("/api/admin/xe/cho-duyet/so-luong")]
    [InlineData("/api/admin/xe/da-duyet?q=29a")]
    [InlineData("/api/admin/xe/9")]
    public async Task NguoiDungThuong_GoiApiAdmin_403(string url)
    {
        var http = Client(out var token);
        var tk = TK.DangKy("Khách", "0912345678", "khach@vi-du.vn", "hash");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.TaoToken(tk, "phien-1"));
        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task ChuaDangNhap_401()
    {
        var http = Client(out _);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/admin/xe/cho-duyet/so-luong")).StatusCode);
    }
}
