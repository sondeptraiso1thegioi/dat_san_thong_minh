using System.Text.RegularExpressions;
using NhaXe.SharedKernel;
namespace NhaXe.Modules.AiBienSo.Domain;

/// <summary>Luật nghiệp vụ của việc đọc biển số. Không biết mô hình nào chạy bên dưới (YOLO, PaddleOCR hay gì khác).</summary>
public static class QuyTacDocBienSo
{
    public const int KichThuocToiDa = 5 * 1024 * 1024;
    /// <summary>Điểm thấp hơn ngưỡng này thì không tự nhận, Admin phải xác nhận (đề cương v10: mặc định 0,85).</summary>
    public const double NguongTinCay = 0.85;

    public const string BienOTo = "O_TO";
    public const string BienXeMay = "XE_MAY";
    public const string BienKhongRo = "KHONG_RO";

    // Biển dân sự VN sau khi bỏ "-" và ".": 2 số tỉnh + seri (1–2 chữ, có thể thêm 1 số) + 4–5 số.
    // VD: 30A12345, 51G31691, 60LD00584, 43D106544, 60MĐ101657 (xe máy 2 dòng ghép lại). Phủ 91,9% nhãn của tập OCR;
    // phần còn lại là biển quân đội / ngoại giao (KP4423, 62037ME), bãi chỉ nhận xe thành viên nên không cần.
    private static readonly Regex DinhDang = new(@"\A[0-9]{2}[A-ZĐ]{1,2}[0-9]?[0-9]{4,5}\z", RegexOptions.Compiled);

    public static void KiemTraAnh(byte[]? noiDung, string loai)
    {
        if (noiDung is null || noiDung.Length == 0 || noiDung.Length > KichThuocToiDa)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "ANH_SAI_KICH_THUOC", "Ảnh phải có dữ liệu và không quá 5 MB.");
        var png = noiDung.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpg = noiDung.Length >= 3 && noiDung[0] == 255 && noiDung[1] == 216 && noiDung[2] == 255;
        if (!(loai == "image/png" && png || loai == "image/jpeg" && jpg))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "ANH_SAI_DINH_DANG", "Chỉ nhận ảnh JPEG hoặc PNG đúng định dạng.");
    }

    public static string? ChuanHoa(string? bienSo)
    {
        var value = Regex.Replace((bienSo ?? "").ToUpperInvariant(), @"[\s.\-]", "");
        return DinhDang.IsMatch(value) ? value : null;
    }

    /// <summary>
    /// Mỗi vùng biển được đọc hai cách: cả vùng là MỘT dòng (biển dài ô tô) và tách đôi thành HAI dòng
    /// (biển vuông ô tô, biển xe máy). Chọn cách cho chuỗi đúng định dạng; cả hai đúng thì lấy cách có điểm cao hơn.
    /// Không dựa vào tỉ lệ khung vì ảnh camera có thể bị kéo giãn.
    /// </summary>
    public static (string BienSo, double DoTinCay)? ChonCachDoc((string Chu, double Diem) motDong, (string Chu, double Diem) haiDong)
    {
        var a = ChuanHoa(motDong.Chu); var b = ChuanHoa(haiDong.Chu);
        if (a is null && b is null) return null;
        if (b is null || a is not null && motDong.Diem >= haiDong.Diem) return (a!, motDong.Diem);
        return (b, haiDong.Diem);
    }

    /// <summary>
    /// Loại biển suy từ chuỗi đã chuẩn hoá (đề cương v10: kiểm chéo với loại xe đã đăng ký).
    /// - Seri 1–2 chữ + 6 số (29B112345, 60MĐ101657): XE_MAY (chữ số đầu là số của seri xe máy).
    /// - Seri 1–2 chữ + 4 số (30A1234, biển ô tô cũ): O_TO.
    /// - Seri 2 chữ (không phải MĐ) + 5 số (60LD00584): O_TO.
    /// - Seri 1 chữ + 5 số (30A12345 ô tô mới, nhưng cũng trùng dạng xe máy cũ 29B1 2345): KHONG_RO, không dùng để chặn.
    /// </summary>
    public static string LoaiBien(string? bienSoChuan)
    {
        if (bienSoChuan is null) return BienKhongRo;
        var m = Regex.Match(bienSoChuan, @"\A[0-9]{2}([A-ZĐ]{1,2})([0-9]+)\z");
        if (!m.Success) return BienKhongRo;
        var seri = m.Groups[1].Value; var so = m.Groups[2].Value.Length;
        if (so == 6 || seri == "MĐ") return BienXeMay;
        if (so == 4) return BienOTo;
        if (so == 5 && seri.Length == 2) return BienOTo;
        return BienKhongRo;
    }
}
