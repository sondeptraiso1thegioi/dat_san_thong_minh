namespace NhaXe.Modules.CongRaVao.Domain;

/// <summary>
/// Nhật ký cổng: MỌI lần xe tới cổng đều ghi một dòng, kể cả bị từ chối (để tra cứu, thống kê).
/// Khác với lượt gửi: lượt gửi chỉ có khi xe thật sự vào bãi.
/// </summary>
public class SuKienCong
{
    public const string Vao = "VAO";
    public const string Ra = "RA";
    public const string ChoQua = "CHO_QUA";
    public const string TuChoi = "TU_CHOI";

    public int Id { get; private set; }
    public string Loai { get; private set; } = "";
    public string? BienSo { get; private set; }
    public decimal? DoTinCay { get; private set; }
    public int? LuotGuiId { get; private set; }
    public string KetQua { get; private set; } = "";
    public string? LyDo { get; private set; }
    public int? AdminId { get; private set; }
    public DateTime ThoiGian { get; private set; }
    /// <summary>Chỉ dùng lúc xe vào: lượt gửi mới chưa có Id, EF tự điền LuotGuiId khi lưu chung một lần.</summary>
    public LuotGui? Luot { get; private set; }

    private SuKienCong() { }

    public static SuKienCong ChoVao(string bienSo, decimal? doTinCay, LuotGui luot, DateTime luc) =>
        new() { Loai = Vao, BienSo = bienSo, DoTinCay = doTinCay, Luot = luot, KetQua = ChoQua, ThoiGian = luc };

    /// <summary>Cho ra theo xác nhận của người dùng xe (ghi tên người xác nhận vào lý do để tra cứu).</summary>
    public static SuKienCong ChoRa(string bienSo, decimal? doTinCay, int luotGuiId, string lyDo, DateTime luc) =>
        new() { Loai = Ra, BienSo = CatBienSo(bienSo), DoTinCay = doTinCay, LuotGuiId = luotGuiId, KetQua = ChoQua,
                LyDo = Cat(lyDo), ThoiGian = luc };

    /// <summary>Admin mở barrier thủ công (UC-A10): bắt buộc lý do (CHECK CK_skc_ly_do).</summary>
    public static SuKienCong MoTay(string loai, string? bienSo, int? luotGuiId, string lyDo, int adminId, DateTime luc) =>
        new() { Loai = loai, BienSo = CatBienSo(bienSo), DoTinCay = 1m, LuotGuiId = luotGuiId, KetQua = ChoQua,
                LyDo = Cat("Admin mở tay: " + lyDo), AdminId = adminId, ThoiGian = luc };

    /// <summary>Admin mở tay cổng VÀO (sửa biển số / chế độ thủ công): lượt gửi mới chưa có Id.</summary>
    public static SuKienCong MoTayVao(string bienSo, LuotGui luot, string lyDo, int adminId, DateTime luc) =>
        new() { Loai = Vao, BienSo = CatBienSo(bienSo), DoTinCay = 1m, Luot = luot, KetQua = ChoQua,
                LyDo = Cat("Admin xác nhận: " + lyDo), AdminId = adminId, ThoiGian = luc };

    private static string Cat(string s) => s.Length > 255 ? s[..255] : s;

    public static SuKienCong TuChoiXe(string loai, string? bienSo, decimal? doTinCay, int? luotGuiId, string lyDo, int? adminId, DateTime luc) =>
        new() { Loai = loai, BienSo = CatBienSo(bienSo), DoTinCay = doTinCay, LuotGuiId = luotGuiId, KetQua = TuChoi,
                LyDo = lyDo.Length > 255 ? lyDo[..255] : lyDo, AdminId = adminId, ThoiGian = luc };

    /// <summary>Biển số sai định dạng vẫn ghi lại (tối đa 15 ký tự, chỉ chữ và số) để biết xe nào đã tới.</summary>
    public static string? CatBienSo(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = new string(s.ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
        return t.Length == 0 ? null : t.Length > 15 ? t[..15] : t;
    }
}
