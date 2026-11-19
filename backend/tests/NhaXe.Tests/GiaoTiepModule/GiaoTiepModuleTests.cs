using NhaXe.Modules.CongRaVao.Application;
using NhaXe.Modules.CongRaVao.Contracts;
using NhaXe.Modules.GoiCho.Application;
using NhaXe.Modules.GoiCho.Domain;
using NhaXe.Modules.NhomGiaDinh.Application;
using NhaXe.Modules.ThongBao.Application;
using NhaXe.SharedKernel;
using NhaXe.Tests.ThongBao;
using LoaiTB = NhaXe.Modules.ThongBao.Contracts.LoaiThongBao;

namespace NhaXe.Tests.GiaoTiepModule;

// Test phần GIAO TIẾP giữa các module: mỗi module chỉ biết module khác qua Contracts,
// nên thay module kia bằng bản giả là kiểm tra được "hỏi đúng chỗ, gửi đúng chỗ".

/// <summary>GoiCho hỏi CongRaVao "xe có trong bãi không", huỷ xong thì gửi thông báo qua ThongBao.</summary>
public class HuyGoiGiaoTiepTests
{
    private readonly FakeClock _clock = new();

    // gói 1 tuần ĐANG DÙNG (bắt đầu hôm qua) của xe 2, chủ xe là tài khoản 2
    private FakeDangKyChoStoreHuy Store() => new()
    {
        Goi = new GoiDaMua(5, 2, 2, "1 tuần", _clock.BayGio.AddDays(-1), _clock.BayGio.AddDays(6), 30000, "CON_HAN", true,
                           XeDangTrongBai: false, BienSo: "29B112345")
    };

    [Fact]
    public async Task CongRaVaoBaoXeTrongBai_KhongHuyDuoc_KhongGuiThongBao()
    {
        var store = Store();
        var cong = new FakeCongRaVaoModule().CoXeTrongBai(xeId: 2, luotGuiId: 7);
        var tb = new FakeThongBaoModule();
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() =>
            new HuyGoiUseCase(store, new HoanTheoNgayConLai(), _clock, TaoModule.TraCuu(cong), tb, new FakeTaiKhoanModule())
                .ThucHienAsync(2, 5));
        Assert.Equal("XE_TRONG_BAI", loi.Ma);
        Assert.Null(store.DaHuyVoiTien);
        Assert.Empty(tb.DaGui);
    }

    [Fact]
    public async Task HuyXong_GuiChoKhach_VaChoMoiAdmin_CoTenSdtLyDoGopY()
    {
        var tb = new FakeThongBaoModule();
        await new HuyGoiUseCase(Store(), new KhongHoanTien(), _clock, TaoModule.TraCuu(), tb, new FakeTaiKhoanModule())
            .ThucHienAsync(2, 5, "Giá cao", "Nên có gói 3 tháng");

        Assert.Equal(2, tb.DaGui.Count);
        var khach = tb.DaGui[0];
        Assert.Equal((2, LoaiTB.HuyGoi), (khach.TaiKhoanId!.Value, khach.Loai));
        Assert.Equal("Đã huỷ gói 1 tuần của xe 29B112345, hoàn 0 đ.", khach.NoiDung);
        var admin = tb.DaGui[1];
        Assert.Null(admin.TaiKhoanId); // gửi cho MỌI Admin: ThongBao tự hỏi TaiKhoan danh sách Admin
        Assert.Equal(LoaiTB.KhachHuy, admin.Loai);
        Assert.Equal("TK2 (0900000002) đã huỷ gói 1 tuần của xe 29B112345, hoàn 0 đ. Lý do: Giá cao. Góp ý: “Nên có gói 3 tháng”",
                     admin.NoiDung);
    }

    [Fact]
    public async Task AdminHuy_GuiChoChuXe_KhongHoanTien()
    {
        var tb = new FakeThongBaoModule();
        var bang = new BangChinhSachHoan(new IChinhSachHoanTien[] { new HoanTheoNgayConLai(), new KhongHoanTien() });
        await new AdminHuyGoiUseCase(Store(), bang, _clock, tb).ThucHienAsync(1, 5, new AdminHuyGoiLenh("Vi phạm nội quy", "KHONG_HOAN"));
        var x = Assert.Single(tb.DaGui);
        Assert.Equal(2, x.TaiKhoanId);
        Assert.Equal("Gói 1 tuần của xe 29B112345 bị Admin huỷ. Lý do: Vi phạm nội quy. Không hoàn tiền.", x.NoiDung);
    }
}

/// <summary>Gỡ xe: GoiCho gỡ rồi báo chủ xe qua ThongBao.</summary>
public class GoXeGiaoTiepTests
{
    private class FakeQuanLyXe : IQuanLyXeStore
    {
        public int SoLanGo;
        public Task GoXeAsync(int adminId, int xeId, string lyDo, CancellationToken ct) { SoLanGo++; return Task.CompletedTask; }
    }

    [Fact]
    public async Task GoXong_BaoChuXe()
    {
        var repo = new FakeXeRepository();
        repo.Ds.Add(Xe.DungLai(6, 5, 1, "30F11111", TrangThaiDuyet.DaDuyet));
        var store = new FakeQuanLyXe();
        var tb = new FakeThongBaoModule();
        await new GoXeUseCase(repo, store, tb).ThucHienAsync(1, 6, "  Dùng biển số giả ");
        Assert.Equal(1, store.SoLanGo);
        var x = Assert.Single(tb.DaGui);
        Assert.Equal((5, LoaiTB.XeBiGo), (x.TaiKhoanId!.Value, x.Loai));
        Assert.Equal("Xe 30F11111 bị gỡ khỏi hệ thống. Lý do: Dùng biển số giả. Gói còn hạn đã bị huỷ, không hoàn tiền.", x.NoiDung);
    }

    [Fact]
    public async Task LyDoSai_KhongGo_KhongGuiThongBao()
    {
        var repo = new FakeXeRepository();
        repo.Ds.Add(Xe.DungLai(6, 5, 1, "30F11111", TrangThaiDuyet.DaDuyet));
        var tb = new FakeThongBaoModule();
        await Assert.ThrowsAsync<LoiNghiepVu>(() => new GoXeUseCase(repo, new FakeQuanLyXe(), tb).ThucHienAsync(1, 6, "abc"));
        Assert.Empty(tb.DaGui);
    }
}

/// <summary>Quyền dùng xe: GoiCho hỏi NhomGiaDinh "ai cùng nhóm", quy tắc nằm trong Xe.DuocDungBoi.</summary>
public class QuyenDungXeTests
{
    private static readonly int[] Rong = Array.Empty<int>();

    [Fact] public void ChuXe_DuocDung() => Assert.True(Xe.DungLai(1, 2, 1, "30A12345", TrangThaiDuyet.DaDuyet).DuocDungBoi(2, Rong));
    [Fact] public void CungNhom_XeChiaSe_DuocDung() =>
        Assert.True(Xe.DungLai(1, 2, 1, "30A12345", TrangThaiDuyet.DaDuyet, chiaSeNhom: true).DuocDungBoi(3, new[] { 2 }));
    [Fact] public void CungNhom_XeKhongChiaSe_KhongDuocDung() =>
        Assert.False(Xe.DungLai(1, 2, 1, "30A12345", TrangThaiDuyet.DaDuyet).DuocDungBoi(3, new[] { 2 }));
    [Fact] public void KhacNhom_KhongDuocDung() =>
        Assert.False(Xe.DungLai(1, 2, 1, "30A12345", TrangThaiDuyet.DaDuyet, chiaSeNhom: true).DuocDungBoi(4, Rong));

    private static (FakeXeRepository, FakeNhomGiaDinhModule, FakeCongRaVaoModule, FakePhiQuaHanStore) DuLieu()
    {
        var repo = new FakeXeRepository();
        repo.Ds.Add(Xe.DungLai(4, 4, 1, "51G67890", TrangThaiDuyet.DaDuyet, chiaSeNhom: true)); // ô tô của Cúc (4)
        var nhom = new FakeNhomGiaDinhModule();
        nhom.CungNhom[6] = new() { 4 };                                                        // tài khoản 6 cùng nhóm với Cúc
        var cong = new FakeCongRaVaoModule().CoXeTrongBai(xeId: 4, luotGuiId: 4);
        var phi = new FakePhiQuaHanStore();
        phi.TheoLuot[4] = new PhiQuaHanDto { LuotGuiId = 4, SoNgayConNo = 2, SoTienConNo = 100000 };
        return (repo, nhom, cong, phi);
    }

    [Fact]
    public async Task XemNo_NguoiCungNhom_XemDuoc_NoLayTheoLuotGuiCuaCongRaVao()
    {
        var (repo, nhom, cong, phi) = DuLieu();
        var no = await new XemPhiQuaHanUseCase(new QuyenDungXe(repo, nhom), TaoModule.TraCuu(cong, phi)).ThucHienAsync(6, 4);
        Assert.Equal(100000, no!.SoTienConNo);
        Assert.Equal(4, no.LuotGuiId);
    }

    [Fact]
    public async Task XemNo_NguoiNgoai_BaoKhongTimThayXe()
    {
        var (repo, nhom, cong, phi) = DuLieu();
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() =>
            new XemPhiQuaHanUseCase(new QuyenDungXe(repo, nhom), TaoModule.TraCuu(cong, phi)).ThucHienAsync(99, 4));
        Assert.Equal("KHONG_TIM_THAY_XE", loi.Ma);
    }

    [Fact]
    public async Task XeKhongTrongBai_KhongCoNo()
    {
        var (repo, nhom, _, phi) = DuLieu();
        Assert.Null(await new XemPhiQuaHanUseCase(new QuyenDungXe(repo, nhom), TaoModule.TraCuu(new FakeCongRaVaoModule(), phi))
            .ThucHienAsync(4, 4));
    }
}

/// <summary>Nội dung thông báo của GoiCho: định dạng tiền giống SQL Server cũ.</summary>
public class NoiDungThongBaoTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(30000, "30.000")]
    [InlineData(1234567, "1.234.567")]
    public void Tien_DinhDangViet(int soTien, string mongDoi) => Assert.Equal(mongDoi, NoiDungThongBao.Tien(soTien));

    [Fact]
    public void BaoAdmin_KhongLyDo_KhongGopY() =>
        Assert.Equal("An (0900000002) đã huỷ gói 1 tuần của xe 29B112345, hoàn 30.000 đ. Lý do: không chọn. Không có góp ý.",
                     NoiDungThongBao.BaoAdminKhachHuy("An", "0900000002", "1 tuần", "29B112345", 30000, null, null));
}

/// <summary>Module ThongBao: kiểm tra loại, cắt nội dung dài, tự hỏi TaiKhoan danh sách Admin.</summary>
public class ThongBaoModuleTests
{
    [Fact]
    public async Task GuiChoMoiAdmin_HoiTaiKhoan_GhiMoiAdminMotDong()
    {
        var store = new FakeThongBaoStore();
        var tk = new FakeTaiKhoanModule(); tk.AdminIds.Clear(); tk.AdminIds.AddRange(new[] { 1, 7 });
        await new ThongBaoModule(store, tk).GuiChoMoiAdminAsync(LoaiTB.KhachHuy, "An đã huỷ gói");
        Assert.Equal(new[] { 1, 7 }, store.Ds.Select(d => d.TaiKhoan));
    }

    [Fact]
    public async Task LoaiSai_BaoLoi_KhongGhi()
    {
        var store = new FakeThongBaoStore();
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => new ThongBaoModule(store, new FakeTaiKhoanModule()).GuiAsync(2, "LINH_TINH", "x"));
        Assert.Equal("LOAI_THONG_BAO_SAI", loi.Ma);
        Assert.Empty(store.Ds);
    }

    [Fact]
    public async Task NoiDungQua500KyTu_BiCat()
    {
        var store = new FakeThongBaoStore();
        await new ThongBaoModule(store, new FakeTaiKhoanModule()).GuiAsync(2, LoaiTB.HuyGoi, new string('a', 600));
        Assert.Equal(500, store.Ds.Single().Tb.NoiDung.Length);
    }
}

/// <summary>Module NhomGiaDinh và CongRaVao: cổng công khai lọc dữ liệu trước khi trả cho module khác.</summary>
public class CongCongKhaiTests
{
    private class FakeThanhVien : IThanhVienNhomQuery
    {
        public Task<IReadOnlyList<int>> LayNguoiCungNhomAsync(int taiKhoanId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<int>>(new[] { 3, taiKhoanId, 3, 5 });
    }

    [Fact]
    public async Task NguoiCungNhom_BoChinhMinh_BoTrung() =>
        Assert.Equal(new[] { 3, 5 }, await new NhomGiaDinhModule(new FakeThanhVien()).LayNguoiCungNhomAsync(2));

    private class FakeLuotGui : ILuotGuiQuery
    {
        public int SoLanGoi;
        public Task<IReadOnlyList<LuotGuiDangMo>> LayDangMoAsync(IReadOnlyCollection<int>? xeIds, CancellationToken ct)
        { SoLanGoi++; return Task.FromResult<IReadOnlyList<LuotGuiDangMo>>(new[] { new LuotGuiDangMo(9, 4, DateTime.Today, null, null, null) }); }
    }

    [Fact]
    public async Task LuotGuiCuaXe_DanhSachRong_KhongHoiDatabase()
    {
        var q = new FakeLuotGui();
        Assert.Empty(await new CongRaVaoModule(q).LayLuotGuiDangMoCuaXeAsync(Array.Empty<int>()));
        Assert.Equal(0, q.SoLanGoi);
    }

    [Fact]
    public async Task LuotGuiCuaXe_KhoaTheoXeId()
    {
        var kq = await new CongRaVaoModule(new FakeLuotGui()).LayLuotGuiDangMoCuaXeAsync(new[] { 4, 4 });
        Assert.Equal(9, kq[4].LuotGuiId);
    }
}
