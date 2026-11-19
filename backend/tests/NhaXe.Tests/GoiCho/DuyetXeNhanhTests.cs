using NhaXe.Modules.GoiCho.Application;
using NhaXe.Modules.GoiCho.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Tests;

/// <summary>Ảnh giấy đăng ký: nhận dạng tệp, quyền, gửi lại sau khi bị từ chối.</summary>
public class AnhGiayDangKyTests
{
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };
    private static readonly byte[] Jpg = { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 };

    [Fact]
    public void NhanDien_TheoNoiDung_KhongTheoDuoi()
    {
        Assert.Equal("png", TepAnh.NhanDien(Png, Png.Length).Duoi);
        Assert.Equal("jpg", TepAnh.NhanDien(Jpg, Jpg.Length).Duoi);
        var pdf = "%PDF-1.7"u8.ToArray(); // tệp PDF đổi tên thành .jpg vẫn bị chặn
        Assert.Equal("ANH_SAI_DINH_DANG", Assert.Throws<LoiNghiepVu>(() => TepAnh.NhanDien(pdf, pdf.Length)).Ma);
    }

    [Fact]
    public void AnhQua5MB_BiChan() =>
        Assert.Equal("ANH_QUA_LON", Assert.Throws<LoiNghiepVu>(() => TepAnh.NhanDien(Png, TepAnh.KichThuocToiDa + 1)).Ma);

    private static (TaiAnhGiayDangKyUseCase uc, FakeXeRepository repo, FakeLuuTruTep kho) Tao()
    {
        var repo = new FakeXeRepository(); var kho = new FakeLuuTruTep();
        return (new TaiAnhGiayDangKyUseCase(repo, kho, new FakeCauHinh(), new FakeGoiChoRepository(), new FakeTaiKhoanModule(), new FakeThongBaoModule()), repo, kho);
    }

    [Fact]
    public async Task ChuXeTaiAnh_LuuKhoaVaoXe()
    {
        var (uc, repo, kho) = Tao();
        repo.Ds.Add(Xe.DungLai(9, 4, 2, "59D145678", TrangThaiDuyet.ChoDuyet));
        await uc.ThucHienAsync(4, 9, new MemoryStream(Png), Png.Length);
        Assert.Equal("giay-dk/1.png", repo.Ds[0].AnhGiayDangKy);
        Assert.Single(kho.Tep);
    }

    [Fact]
    public async Task NguoiKhacTaiAnh_BiChan_VaKhongGhiTep()
    {
        var (uc, repo, kho) = Tao();
        repo.Ds.Add(Xe.DungLai(9, 4, 2, "59D145678", TrangThaiDuyet.ChoDuyet));
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => uc.ThucHienAsync(2, 9, new MemoryStream(Png), Png.Length));
        Assert.Equal("KHONG_PHAI_CHU_XE", loi.Ma);
        Assert.Empty(kho.Tep);
    }

    [Fact]
    public async Task XeBiTuChoi_TaiAnhMoi_LaGuiLai()
    {
        var (uc, repo, _) = Tao();
        repo.Ds.Add(Xe.DungLai(9, 4, 2, "59D145678", TrangThaiDuyet.TuChoi, "giay-dk/cu.png", "Ảnh mờ"));
        await uc.ThucHienAsync(4, 9, new MemoryStream(Jpg), Jpg.Length);
        Assert.Equal(TrangThaiDuyet.ChoDuyet, repo.Ds[0].TrangThaiDuyet);
        Assert.Null(repo.Ds[0].LyDoTuChoi);
    }

    [Fact]
    public async Task GuiLai_VanTinhGioiHan5XeChoDuyet()
    {
        var (uc, repo, _) = Tao();
        for (var i = 1; i <= 5; i++) repo.Ds.Add(Xe.DungLai(i, 4, 2, $"59D10000{i}", TrangThaiDuyet.ChoDuyet));
        repo.Ds.Add(Xe.DungLai(9, 4, 2, "59D145678", TrangThaiDuyet.TuChoi, "giay-dk/cu.png", "Ảnh mờ"));
        var loi = await Assert.ThrowsAsync<LoiNghiepVu>(() => uc.ThucHienAsync(4, 9, new MemoryStream(Png), Png.Length));
        Assert.Equal("QUA_SO_XE_CHO_DUYET", loi.Ma);
    }

    [Fact]
    public void ChiChuXeVaAdminXemDuocAnh()
    {
        var xe = Xe.DungLai(9, 4, 2, "59D145678", TrangThaiDuyet.ChoDuyet, "giay-dk/1.png");
        xe.KiemTraXemAnhDuoc(4, false);
        xe.KiemTraXemAnhDuoc(1, true);
        Assert.Equal("KHONG_CO_QUYEN", Assert.Throws<LoiNghiepVu>(() => xe.KiemTraXemAnhDuoc(2, false)).Ma);
    }
}

/// <summary>Duyệt: phải có ảnh; từ chối: phải có lý do; duyệt hàng loạt xử lý từng xe.</summary>
public class DuyetHangLoatTests
{
    [Fact]
    public void DuyetXeChuaCoAnh_BiChan() =>
        Assert.Equal("THIEU_ANH_GIAY_DK",
            Assert.Throws<LoiNghiepVu>(() => Xe.DungLai(9, 4, 2, "59D145678", TrangThaiDuyet.ChoDuyet).Duyet(1, new DateTime(2026, 11, 18, 8, 0, 0))).Ma);

    [Fact]
    public void TuChoiKhongLyDo_BiChan() =>
        Assert.Equal("THIEU_LY_DO",
            Assert.Throws<LoiNghiepVu>(() => Xe.DungLai(9, 4, 2, "59D145678", TrangThaiDuyet.ChoDuyet).TuChoi(" ")).Ma);

    [Fact]
    public async Task DuyetNhieuXe_XeLoiBaoRieng_XeKhacVanDuyet()
    {
        var repo = new FakeXeRepository();
        repo.Ds.Add(Xe.DungLai(1, 4, 2, "59D100001", TrangThaiDuyet.ChoDuyet, "giay-dk/1.png"));
        repo.Ds.Add(Xe.DungLai(2, 4, 2, "59D100002", TrangThaiDuyet.ChoDuyet));                  // thiếu ảnh
        repo.Ds.Add(Xe.DungLai(3, 5, 2, "59D100003", TrangThaiDuyet.DaDuyet, "giay-dk/3.png"));  // đã xử lý
        repo.Ds.Add(Xe.DungLai(4, 5, 2, "59D100004", TrangThaiDuyet.ChoDuyet, "giay-dk/4.png"));

        var kq = await new DuyetHangLoatUseCase(repo, new FakeClock(), new FakeThongBaoModule()).ThucHienAsync(1, new DuyetHangLoatLenh(new[] { 1, 2, 3, 4, 99, 1 }, true, null));

        Assert.Equal(new[] { 1, 4 }, kq.ThanhCong);
        Assert.Equal(new[] { "THIEU_ANH_GIAY_DK", "DA_XU_LY", "KHONG_TIM_THAY_XE" }, kq.Loi.Select(l => l.Ma));
        Assert.Equal(TrangThaiDuyet.DaDuyet, repo.Ds[3].TrangThaiDuyet);
    }

    [Fact]
    public async Task TuChoiHangLoat_CungMotLyDo()
    {
        var repo = new FakeXeRepository();
        repo.Ds.Add(Xe.DungLai(1, 4, 2, "59D100001", TrangThaiDuyet.ChoDuyet, "giay-dk/1.png"));
        repo.Ds.Add(Xe.DungLai(2, 5, 2, "59D100002", TrangThaiDuyet.ChoDuyet));
        var kq = await new DuyetHangLoatUseCase(repo, new FakeClock(), new FakeThongBaoModule()).ThucHienAsync(1, new DuyetHangLoatLenh(new[] { 1, 2 }, false, "Ảnh không khớp biển số"));
        Assert.Equal(2, kq.ThanhCong.Count);
        Assert.All(repo.Ds, x => Assert.Equal("Ảnh không khớp biển số", x.LyDoTuChoi));
    }

    [Fact]
    public async Task TuChoiHangLoatThieuLyDo_KhongXuLyXeNao()
    {
        var repo = new FakeXeRepository();
        repo.Ds.Add(Xe.DungLai(1, 4, 2, "59D100001", TrangThaiDuyet.ChoDuyet, "giay-dk/1.png"));
        await Assert.ThrowsAsync<LoiNghiepVu>(() => new DuyetHangLoatUseCase(repo, new FakeClock(), new FakeThongBaoModule()).ThucHienAsync(1, new DuyetHangLoatLenh(new[] { 1 }, false, "")));
        Assert.Equal(TrangThaiDuyet.ChoDuyet, repo.Ds[0].TrangThaiDuyet);
    }
}
