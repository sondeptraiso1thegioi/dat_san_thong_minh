using NhaXe.SharedKernel;

namespace NhaXe.Modules.GoiCho.Domain;

/// <summary>
/// Ảnh giấy đăng ký xe người dùng tải lên. Kiểm tra bằng NỘI DUNG tệp (chữ ký đầu tệp),
/// không tin đuôi tên hay Content-Type do trình duyệt gửi, vì hai thứ đó đổi được tuỳ ý.
/// </summary>
public sealed record TepAnh(string Duoi, string ContentType)
{
    public const long KichThuocToiDa = 5 * 1024 * 1024; // 5 MB
    public const int SoByteDauCanDoc = 8;

    public static readonly TepAnh Jpg = new("jpg", "image/jpeg");
    public static readonly TepAnh Png = new("png", "image/png");

    public static TepAnh NhanDien(ReadOnlySpan<byte> dauTep, long kichThuoc)
    {
        if (kichThuoc <= 0)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "THIEU_ANH", "Chưa chọn ảnh giấy đăng ký.");
        if (kichThuoc > KichThuocToiDa)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "ANH_QUA_LON", "Ảnh tối đa 5 MB.");
        if (dauTep.Length >= 3 && dauTep[0] == 0xFF && dauTep[1] == 0xD8 && dauTep[2] == 0xFF) return Jpg;
        if (dauTep.Length >= 8 && dauTep[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return Png;
        throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "ANH_SAI_DINH_DANG", "Chỉ nhận ảnh JPG hoặc PNG.");
    }

    public static TepAnh TuDuoi(string khoa) =>
        khoa.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? Png : Jpg;
}
