using NhaXe.Modules.BaiXe.Application;
using NhaXe.Modules.BaiXe.Domain;
using NhaXe.SharedKernel;
using NhaXe.Tests.CongRaVao;

namespace NhaXe.Tests.BaiXe;

public class QuanLyBaiTests
{
    private static readonly int[] LoaiXe = { 1, 2 };
    private static readonly DateTime T0 = new(2026, 11, 11, 8, 0, 0);

    private static KhuVuc Khu(int id, string ten, int loai = 1)
    {
        var k = KhuVuc.Tao(ten, loai, LoaiXe);
        FakeCongRaVaoRepository.DatId(k, id);
        return k;
    }

    [Theory]
    [InlineData("a01", "A01")]
    [InlineData(" b12 ", "B12")]
    public void MaO_VietHoa_MaQrSuyRaTuMaO(string nhap, string ma)
    {
        var o = ODo.Tao(Khu(1, "Khu A"), nhap, null);
        Assert.Equal((ma, "QR-" + ma), (o.MaO, o.MaQr));
    }

    [Theory]
    [InlineData("1A")]
    [InlineData("A")]
    [InlineData("A-01")]
    public void MaO_Sai_BaoLoi(string ma) =>
        Assert.Equal("MA_O_SAI", Assert.Throws<LoiNghiepVu>(() => ODo.Tao(Khu(1, "Khu A"), ma, null)).Ma);

    [Fact]
    public void CameraKhacKhu_KhongGanVaoO()
    {
        var cam = Camera.Tao(2, "Cam B", "rtsp://cam-b", true);
        Assert.Equal("CAMERA_KHAC_KHU", Assert.Throws<LoiNghiepVu>(() => ODo.Tao(Khu(1, "Khu A"), "A01", cam)).Ma);
    }

    [Theory]
    [InlineData(0, true, true)]
    [InlineData(80, true, true)]
    [InlineData(81, true, false)]   // khu xe máy tối đa 80 xe
    [InlineData(-1, true, false)]
    [InlineData(500, false, true)]  // khu ô tô không bị giới hạn 80
    public void SucChuaKhu_XeMayToiDa80(int sucChua, bool laXeMay, bool hopLe)
    {
        var k = Khu(1, "Khu R", 2);
        if (hopLe) { k.DatSucChua(sucChua, laXeMay); Assert.Equal(sucChua, k.SucChua); }
        else Assert.Equal("SUC_CHUA_KHU_SAI", Assert.Throws<LoiNghiepVu>(() => k.DatSucChua(sucChua, laXeMay)).Ma);
    }

    [Fact]
    public void KhuMoi_DangDung_TatDuoc()
    {
        var k = Khu(1, "Khu C", 2);
        Assert.True(k.DangDung);
        k.DatDangDung(false);
        Assert.False(k.DangDung);
    }

    [Fact]
    public void KhuLoaiXeKhongCo_BaoLoi() =>
        Assert.Equal("LOAI_XE_SAI", Assert.Throws<LoiNghiepVu>(() => KhuVuc.Tao("Khu Z", 9, LoaiXe)).Ma);

    [Fact]
    public void TrangThaiDoAiBao_QuaMuoiLamPhut_ThanhKhongRo_AdminDatThiGiuNguyen()
    {
        var ai = TrangThaiO.Tao(1, TrangThaiOMa.Trong, NguonTrangThai.Ai, T0);
        Assert.Equal(TrangThaiOMa.Trong, ai.HienThi(T0.AddMinutes(15)));
        Assert.Equal(TrangThaiOMa.KhongRo, ai.HienThi(T0.AddMinutes(16)));
        var admin = TrangThaiO.Tao(1, TrangThaiOMa.CoXe, NguonTrangThai.Admin, T0);
        Assert.Equal(TrangThaiOMa.CoXe, admin.HienThi(T0.AddDays(1)));
        Assert.Equal(TrangThaiOMa.KhongRo, TrangThaiO.HienThi(null, T0));
        Assert.Equal("TRANG_THAI_O_SAI", Assert.Throws<LoiNghiepVu>(() => admin.CapNhat("DAY", NguonTrangThai.Admin, T0)).Ma);
    }

    [Fact]
    public async Task ChoTrong_DemTheoKhu_KhongLoBienSo()
    {
        var q = new FakeCauHinhBaiQuery();
        var a = Khu(1, "Khu A");
        q.Khu.Add(a);
        foreach (var (id, ma) in new[] { (1, "A01"), (2, "A02"), (3, "A03") })
        {
            var o = ODo.Tao(a, ma, null);
            FakeCongRaVaoRepository.DatId(o, id);
            q.O.Add(o);
        }
        q.TrangThai.Add(TrangThaiO.Tao(1, TrangThaiOMa.Trong, NguonTrangThai.Admin, T0));
        q.TrangThai.Add(TrangThaiO.Tao(2, TrangThaiOMa.CoXe, NguonTrangThai.Ai, T0.AddMinutes(-1)));
        // ô 3 chưa ai báo → không rõ

        var kq = await new LayChoTrongUseCase(q, new FakeClock { BayGio = T0 }).ThucHienAsync();
        var k = Assert.Single(kq.Khu);
        Assert.Equal((3, 1, 1, 1), (k.SoO, k.SoTrong, k.SoCoXe, k.SoKhongRo));
    }

    [Fact]
    public async Task ChoTrong_KhuTamTat_KhongHien()
    {
        var q = new FakeCauHinhBaiQuery();
        var a = Khu(1, "Khu A");
        a.DatDangDung(false);
        q.Khu.Add(a);
        q.Khu.Add(Khu(2, "Khu R"));

        var kq = await new LayChoTrongUseCase(q, new FakeClock { BayGio = T0 }).ThucHienAsync();
        Assert.Equal("Khu R", Assert.Single(kq.Khu).Ten);
    }
}
