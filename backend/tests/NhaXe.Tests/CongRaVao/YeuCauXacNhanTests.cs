using NhaXe.Modules.CongRaVao.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Tests.CongRaVao;

/// <summary>Quy tắc xác nhận xe ra (đề cương v10) nằm trong thực thể: test không cần DB, không cần use case.</summary>
public class YeuCauXacNhanTests
{
    private static readonly DateTime T0 = new(2026, 11, 11, 8, 0, 0);
    private static YeuCauXacNhan Moi() => YeuCauXacNhan.Tao(1, T0, 60);

    [Fact]
    public void NguoiTraLoiTruoc_DongY_LaKetThucNgay()
    {
        var y = Moi();
        y.NguoiDungXeTraLoi(3, true, T0.AddSeconds(9));
        Assert.Equal(KetQuaXacNhan.DongY, y.KetQua);
        Assert.Equal((3, T0.AddSeconds(9)), (y.ChuXeTraLoiId!.Value, y.KetThucLuc!.Value));
    }

    [Fact]
    public void NguoiDungXeBamKhongPhaiToi_TuChoiNgay_LaNghiMat()
    {
        var y = Moi();
        y.NguoiDungXeTraLoi(2, false, T0.AddSeconds(4));
        Assert.Equal(KetQuaXacNhan.TuChoi, y.KetQua);
        Assert.True(y.NguoiDungXeBaoKhongPhai);
    }

    [Fact]
    public void NguoiThuHaiBam_BaoDaCoNguoiTraLoi()
    {
        var y = Moi();
        y.NguoiDungXeTraLoi(2, true, T0);
        Assert.Equal("DA_CO_NGUOI_TRA_LOI", Assert.Throws<LoiNghiepVu>(() => y.NguoiDungXeTraLoi(3, false, T0.AddSeconds(1))).Ma);
        Assert.Equal(KetQuaXacNhan.DongY, y.KetQua);
    }

    [Fact]
    public void Qua60Giay_KhongTraLoiDuoc_TacVuNenKetThucHetHan()
    {
        var y = Moi();
        Assert.Equal("XAC_NHAN_HET_HAN", Assert.Throws<LoiNghiepVu>(() => y.NguoiDungXeTraLoi(2, true, T0.AddSeconds(60))).Ma);
        Assert.False(y.HetHan(T0.AddSeconds(59)));
        Assert.True(y.HetHan(T0.AddSeconds(60)));
        Assert.Equal(KetQuaXacNhan.HetHan, y.KetQua);
        Assert.Equal("Quá 60 giây không ai xác nhận, chuyển Admin xử lý", y.LyDo);
        Assert.False(y.HetHan(T0.AddSeconds(90)));
    }

    [Fact]
    public void AdminMoTay_DongYeuCauDangCho()
    {
        var y = Moi();
        y.AdminMoTay(1, "App lỗi", T0.AddSeconds(5));
        Assert.Equal((KetQuaXacNhan.DongY, (int?)1, (bool?)true), (y.KetQua, y.QuanLyId, y.QuanLyDongY));
    }

    [Fact]
    public void SoGiayConLai_LamTronLen_KhongAm()
    {
        var y = Moi();
        Assert.Equal(60, y.SoGiayConLai(T0));
        Assert.Equal(1, y.SoGiayConLai(T0.AddSeconds(59.2)));
        Assert.Equal(0, y.SoGiayConLai(T0.AddSeconds(70)));
    }

    [Fact]
    public void LuotGui_RaDuocKhiChoXacNhanHoacChoThanhToan_NghiMatThuRaLaiDuoc()
    {
        var l = LuotGui.Mo(1, T0, 0.9m);
        Assert.Equal("LUOT_KHONG_CHO_RA", Assert.Throws<LoiNghiepVu>(() => l.Ra(T0.AddHours(1))).Ma);
        l.BatDauChoXacNhan(0.95m);
        l.HuyChoRa(nghiMat: true);
        Assert.Equal(TrangThaiLuot.NghiMat, l.TrangThai);
        l.BatDauChoXacNhan(0.95m);
        l.ChoThanhToan();
        Assert.Equal(TrangThaiLuot.ChoThanhToan, l.TrangThai);
        l.Ra(T0.AddHours(1));
        Assert.Equal(TrangThaiLuot.DaRa, l.TrangThai);
        Assert.False(l.DangMo);
    }

    [Fact]
    public void LuotGui_GhiViTri_RaThiXoa_NguonSaiBiChan()
    {
        var l = LuotGui.Mo(1, T0, 0.9m);
        Assert.Equal("NGUON_VI_TRI_SAI", Assert.Throws<LoiNghiepVu>(() => l.GhiViTri(2, "QUET", T0)).Ma);
        l.GhiViTri(2, NguonViTri.CameraKhu, T0.AddMinutes(3));
        Assert.Equal((2, NguonViTri.CameraKhu), (l.ViTriKhuId!.Value, l.ViTriNguon));
        l.RaThuCong(T0.AddHours(2));
        Assert.Null(l.ViTriKhuId);
        l.GhiViTri(3, NguonViTri.CameraKhu, T0.AddHours(3)); // bản ghi đến muộn sau khi ra: bỏ qua
        Assert.Null(l.ViTriKhuId);
    }
}
