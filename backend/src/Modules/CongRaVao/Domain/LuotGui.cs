using NhaXe.SharedKernel;

namespace NhaXe.Modules.CongRaVao.Domain;

/// <summary>Trạng thái lượt gửi (khớp CHECK CK_lg_trang_thai).</summary>
public static class TrangThaiLuot
{
    public const string DangGui = "DANG_GUI";
    public const string ChoXacNhan = "CHO_XAC_NHAN";
    public const string ChoThanhToan = "CHO_THANH_TOAN";
    public const string DaRa = "DA_RA";
    public const string NghiMat = "NGHI_MAT";
}

/// <summary>Nguồn của vị trí khu (khớp CHECK CK_lg_vi_tri_nguon).</summary>
public static class NguonViTri
{
    public const string CameraKhu = "CAMERA_KHU";
    public const string NguoiDung = "NGUOI_DUNG";
    public const string Admin = "ADMIN";
    public static bool HopLe(string n) => n is CameraKhu or NguoiDung or Admin;
}

/// <summary>
/// Một lần xe ở trong bãi: từ lúc cổng vào cho qua tới lúc cổng ra mở (đề cương v10, UC-U06/U07/U08).
/// Máy trạng thái:
///   DANG_GUI ──xe tới cổng ra──▶ CHO_XAC_NHAN ──"Đúng là tôi", gói còn hạn──▶ DA_RA
///        ▲                          │   └──"Đúng là tôi", còn phí quá hạn──▶ CHO_THANH_TOAN ──trả phí xong──▶ DA_RA
///        └────── hết 60 giây ───────┤
///                                   └──"Không phải tôi"──▶ NGHI_MAT (Admin xử lý)
///   Admin mở barrier thủ công (UC-A10, bắt buộc lý do) được ở mọi trạng thái còn mở.
/// Khu mới nhất (vi_tri_*) là bản sao nhanh do module TimXe báo qua ICongRaVaoModule.GhiViTriAsync;
/// xe ra thì xoá khu.
/// </summary>
public class LuotGui
{
    public int Id { get; private set; }
    public int XeId { get; private set; }
    public DateTime ThoiGianVao { get; private set; }
    public DateTime? ThoiGianRa { get; private set; }
    public string TrangThai { get; private set; } = TrangThaiLuot.DangGui;
    public decimal? DoTinCayVao { get; private set; }
    public decimal? DoTinCayRa { get; private set; }
    public int? ViTriKhuId { get; private set; }
    public DateTime? ViTriLuc { get; private set; }
    public string? ViTriNguon { get; private set; }
    /// <summary>Ảnh đối chứng (khoá tệp trong kho ảnh): chụp ở cổng vào / cổng ra. null = nhập tay, không có ảnh.</summary>
    public string? AnhVao { get; private set; }
    public string? AnhRa { get; private set; }

    private LuotGui() { }

    /// <summary>Cổng vào cho qua: mở lượt gửi mới.</summary>
    public static LuotGui Mo(int xeId, DateTime luc, decimal? doTinCay, string? anhVao = null) =>
        new() { XeId = xeId, ThoiGianVao = luc, DoTinCayVao = doTinCay, TrangThai = TrangThaiLuot.DangGui, AnhVao = anhVao };

    public bool DangMo => ThoiGianRa is null;

    /// <summary>Xe tới cổng ra: chờ người có quyền dùng xe xác nhận.</summary>
    public void BatDauChoXacNhan(decimal? doTinCay, string? anhRa = null)
    {
        if (TrangThai is not (TrangThaiLuot.DangGui or TrangThaiLuot.NghiMat))
            throw new LoiNghiepVu(LoaiLoi.XungDot, "LUOT_KHONG_CHO_RA", "Lượt gửi này không ở trạng thái cho phép ra.");
        TrangThai = TrangThaiLuot.ChoXacNhan;
        DoTinCayRa = doTinCay;
        if (anhRa is not null) AnhRa = anhRa;
    }

    /// <summary>Yêu cầu ra không thành: quay lại "đang gửi", hoặc "nghi mất" nếu người dùng xe nói không phải mình.</summary>
    public void HuyChoRa(bool nghiMat)
    {
        if (TrangThai != TrangThaiLuot.ChoXacNhan) return;
        TrangThai = nghiMat ? TrangThaiLuot.NghiMat : TrangThaiLuot.DangGui;
    }

    /// <summary>"Đúng là tôi" nhưng còn phí quá hạn: barrier chưa mở, chờ trả phí trên app.</summary>
    public void ChoThanhToan()
    {
        if (TrangThai != TrangThaiLuot.ChoXacNhan)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "LUOT_KHONG_CHO_RA", "Lượt gửi chưa ở bước chờ xác nhận.");
        TrangThai = TrangThaiLuot.ChoThanhToan;
    }

    /// <summary>Mở barrier theo xác nhận của người dùng xe (hoặc sau khi trả phí): đóng lượt gửi, xoá khu.</summary>
    public void Ra(DateTime luc)
    {
        if (TrangThai is not (TrangThaiLuot.ChoXacNhan or TrangThaiLuot.ChoThanhToan))
            throw new LoiNghiepVu(LoaiLoi.XungDot, "LUOT_KHONG_CHO_RA", "Lượt gửi chưa ở bước chờ xác nhận.");
        DongLuot(luc);
    }

    /// <summary>Admin mở barrier thủ công (UC-A10). Lý do do use case kiểm và ghi vào nhật ký cổng.</summary>
    public void RaThuCong(DateTime luc)
    {
        if (!DangMo)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "LUOT_DA_DONG", "Lượt gửi này đã kết thúc.");
        DongLuot(luc);
    }

    /// <summary>Ghi khu mới nhất (chỉ module TimXe gọi qua ICongRaVaoModule.GhiViTriAsync). khuId = null: xoá khu.</summary>
    public void GhiViTri(int? khuId, string? nguon, DateTime luc)
    {
        if (!DangMo) return; // xe đã ra: bỏ qua bản ghi đến muộn
        if (khuId is null) { XoaViTri(); return; }
        if (nguon is null || !NguonViTri.HopLe(nguon))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "NGUON_VI_TRI_SAI", "Nguồn vị trí không hợp lệ.");
        ViTriKhuId = khuId; ViTriNguon = nguon; ViTriLuc = luc;
    }

    private void DongLuot(DateTime luc)
    {
        TrangThai = TrangThaiLuot.DaRa;
        ThoiGianRa = luc < ThoiGianVao ? ThoiGianVao : luc;
        XoaViTri();
    }

    private void XoaViTri() { ViTriKhuId = null; ViTriNguon = null; ViTriLuc = null; }
}
