using System.Text.RegularExpressions;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.GoiCho.Domain;

/// <summary>
/// Value object Biển số: chuẩn hoá một lần ở Domain (viết hoa, bỏ dấu chấm/gạch/khoảng trắng),
/// nên "29-B1 123.45" và "29b112345" là cùng một biển. Khớp CHECK CK_xe_bien_so trong nha-xe.sql.
/// </summary>
public sealed record BienSo
{
    private static readonly Regex MauHopLe = new(@"^\d{2}[A-Z]{1,2}\d{4,6}$", RegexOptions.Compiled);

    public string GiaTri { get; }

    private BienSo(string giaTri) => GiaTri = giaTri;

    public static BienSo Tao(string? nhap)
    {
        var chuan = new string((nhap ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (!MauHopLe.IsMatch(chuan))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "BIEN_SO_SAI",
                $"Biển số \"{nhap}\" không hợp lệ. Ví dụ đúng: 30A-123.45 hoặc 29B1-123.45.");
        return new BienSo(chuan);
    }

    public override string ToString() => GiaTri;

    /// <summary>
    /// Chuẩn hoá CHUỖI TÌM giống cách lưu biển số (viết hoa, bỏ chấm / gạch / khoảng trắng), nhưng không bắt đủ dạng biển:
    /// "29a-1", "29A.12" → "29A1", "29A12" để tìm "chứa". Chỉ giữ A–Z và 0–9 nên an toàn khi đưa vào LIKE (không có % hay _).
    /// </summary>
    public static string ChuanHoaTim(string? nhap) =>
        new string((nhap ?? "").ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9').Take(15).ToArray());
}
