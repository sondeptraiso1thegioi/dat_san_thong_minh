using NhaXe.SharedKernel;

namespace NhaXe.Modules.ThongBao.Domain;

/// <summary>
/// Quy tắc về loại thông báo trong bảng thong_bao. Module khác sinh ra thông báo (GoiCho: HUY_GOI, XE_BI_GO, KHACH_HUY;
/// sau này CongRaVao: VAO, RA...) và gửi qua IThongBaoModule; ThongBao kiểm tra, lưu và cho đọc.
/// </summary>
public static class LoaiThongBao
{
    public const string KhachHuy = "KHACH_HUY";
    public const int DoDaiToiDa = 500;

    /// <summary>Khớp CHECK CK_tb_loai trong nha-xe.sql.</summary>
    public static readonly IReadOnlyCollection<string> HopLe =
        new[] { "VAO", "RA", "SAP_HET_HAN", "HET_HAN", "QUA_HAN", "NGHI_MAT", "HUY_GOI", "XE_BI_GO", "KHACH_HUY",
                "KHU", "NHOM", "DOI_SOAT", "CHO_THANH_TOAN", "XE_CHO_DUYET", "KET_QUA_DUYET" };

    /// <summary>Những loại Admin thấy trong hộp thư Admin: khách huỷ gói, xe nghi mất, xe ra không đủ xác nhận.
    /// Thêm loại mới chỉ cần thêm vào đây (OCP).</summary>
    public static readonly IReadOnlyCollection<string> HopThuAdmin = new[] { KhachHuy, "NGHI_MAT", "RA", "DOI_SOAT", "XE_CHO_DUYET" };

    /// <summary>Kiểm tra trước khi lưu; nội dung dài quá thì cắt (như LEFT(..., 500) trong SQL cũ).</summary>
    public static string ChuanHoaNoiDung(string loai, string noiDung)
    {
        if (!HopLe.Contains(loai))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "LOAI_THONG_BAO_SAI", $"Loại thông báo không hợp lệ: {loai}.");
        if (string.IsNullOrWhiteSpace(noiDung))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "THONG_BAO_RONG", "Nội dung thông báo không được rỗng.");
        return noiDung.Length > DoDaiToiDa ? noiDung[..DoDaiToiDa] : noiDung;
    }
}
