using NhaXe.SharedKernel;

namespace NhaXe.Modules.TimXe.Domain;

// Thực thể và quy tắc của module TimXe (đề cương v10, mục "Chia khu, ghi nhận xe vào khu và tìm xe").
// Phạm vi hiện tại: XE MÁY (định vị mức khu). Phần ô tô trong khu (PKLot, ô trống) chưa làm.

public static class TrangThaiGiu
{
    public const string DangGiu = "DANG_GIU";
    public const string DaVao = "DA_VAO";
    public const string HetHan = "HET_HAN";
    public const string Huy = "HUY";
    /// <summary>Camera VÀO của một khu KHÁC khu đích thấy xe (người dùng không theo chỉ dẫn). Tin camera, không phạt.</summary>
    public const string KhuKhac = "KHU_KHAC";
}

/// <summary>
/// Trạng thái PHIÊN ĐIỀU HƯỚNG trả cho app. Phiên chính là suất giữ (giu_cho_khu), nên chỉ đổi tên cho dễ hiểu:
/// DANG_GIU → DANG_DAN · DA_VAO → DA_TOI · KHU_KHAC → TOI_KHU_KHAC · HET_HAN → HET_HAN · HUY (xe ra cổng) → DA_DONG.
/// </summary>
public static class TrangThaiPhien
{
    public const string DangDan = "DANG_DAN";
    public const string DaToi = "DA_TOI";
    public const string ToiKhuKhac = "TOI_KHU_KHAC";
    public const string HetHan = "HET_HAN";
    public const string DaDong = "DA_DONG";

    /// <summary>Suất còn ghi DANG_GIU nhưng đã quá giờ (tác vụ nền chưa kịp chạy) thì app thấy luôn HET_HAN.</summary>
    public static string Tu(GiuChoKhu g, DateTime bayGio) => g.TrangThai switch
    {
        TrangThaiGiu.DangGiu => g.ConGiu(bayGio) ? DangDan : HetHan,
        TrangThaiGiu.DaVao => DaToi,
        TrangThaiGiu.KhuKhac => ToiKhuKhac,
        TrangThaiGiu.HetHan => HetHan,
        _ => DaDong,
    };
}

/// <summary>
/// Một suất được giữ trong khu sau khi xe qua cổng vào (bảng giu_cho_khu, UC-U22). Mặc định 5 phút.
/// Chỗ còn của khu = sức chứa − số xe đang ghi ở khu − số suất DANG_GIU chưa hết hạn.
/// </summary>
public class GiuChoKhu
{
    public int Id { get; private set; }
    public int LuotGuiId { get; private set; }
    public int KhuId { get; private set; }
    public DateTime BatDauLuc { get; private set; }
    public DateTime HetHanLuc { get; private set; }
    public string TrangThai { get; private set; } = TrangThaiGiu.DangGiu;
    public DateTime? KetThucLuc { get; private set; }
    /// <summary>Khu hệ thống gợi ý lúc xe qua cổng (KhuId là khu đích hiện tại: người dùng có thể đã đổi). V012.</summary>
    public int? KhuGoiYId { get; private set; }
    /// <summary>Khu camera thật sự thấy xe vào (DA_VAO, KHU_KHAC).</summary>
    public int? KhuThucTeId { get; private set; }
    /// <summary>Lần gần nhất xe tới một khu ĐÃ ĐẦY (nhân viên không cho vào, camera không +1): app báo "Khu X đã đầy".</summary>
    public int? CanhBaoKhuId { get; private set; }
    public DateTime? CanhBaoLuc { get; private set; }

    private GiuChoKhu() { }

    public static GiuChoKhu Tao(int luotGuiId, int khuId, DateTime bayGio, int soPhut)
    {
        if (soPhut is < 1 or > 60)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "PHUT_GIU_SAI", "Thời gian giữ suất phải từ 1 đến 60 phút.");
        return new() { LuotGuiId = luotGuiId, KhuId = khuId, KhuGoiYId = khuId, BatDauLuc = bayGio, HetHanLuc = bayGio.AddMinutes(soPhut) };
    }

    /// <summary>
    /// Người dùng tự chọn khu khác khi đang được dẫn đường: suất giữ CHUYỂN sang khu mới, GIỮ NGUYÊN thời hạn còn lại
    /// (đổi khu không kéo dài được thời gian giữ). Kiểm tra khu mới còn chỗ là việc của use case (cần số liệu cả bãi).
    /// </summary>
    public void DoiKhuDich(int khuMoi, DateTime bayGio)
    {
        if (!ConGiu(bayGio))
            throw new LoiNghiepVu(LoaiLoi.XungDot, "PHIEN_DA_KET_THUC", "Phiên dẫn đường đã kết thúc, không đổi khu được nữa.");
        KhuId = khuMoi;
        CanhBaoKhuId = null; CanhBaoLuc = null;
    }

    /// <summary>Xe tới khu đã đầy (bị chặn): ghi để app báo "Khu X đã đầy, mời sang khu đích"; suất ở khu đích vẫn giữ.</summary>
    public void GhiToiKhuDay(int khuDayId, DateTime bayGio)
    {
        if (TrangThai != TrangThaiGiu.DangGiu) return;
        CanhBaoKhuId = khuDayId; CanhBaoLuc = bayGio;
    }

    public bool ConGiu(DateTime bayGio) => TrangThai == TrangThaiGiu.DangGiu && bayGio < HetHanLuc;

    /// <summary>
    /// Xe đã được ghi vào khu khuThucTe (camera, "Đã đỗ xe" hoặc Admin): suất kết thúc, xe chiếm chỗ thật.
    /// Đúng khu đích → DA_VAO; khu khác → KHU_KHAC (suất ở khu đích được nhả). Trả về true nếu là khu khác.
    /// </summary>
    public bool DaVao(int khuThucTe, DateTime bayGio)
    {
        if (TrangThai != TrangThaiGiu.DangGiu) return false;
        KhuThucTeId = khuThucTe;
        var khac = khuThucTe != KhuId;
        KetThuc(khac ? TrangThaiGiu.KhuKhac : TrangThaiGiu.DaVao, bayGio);
        return khac;
    }
    /// <summary>Quá thời gian giữ mà xe chưa vào khu nào: trả suất.</summary>
    public bool HetHan(DateTime bayGio)
    {
        if (TrangThai != TrangThaiGiu.DangGiu || bayGio < HetHanLuc) return false;
        KetThuc(TrangThaiGiu.HetHan, bayGio);
        return true;
    }
    /// <summary>Xe ra cổng chính khi suất còn giữ.</summary>
    public void Huy(DateTime bayGio) => KetThuc(TrangThaiGiu.Huy, bayGio);

    private void KetThuc(string tt, DateTime bayGio)
    {
        if (TrangThai != TrangThaiGiu.DangGiu) return;
        TrangThai = tt;
        KetThucLuc = bayGio < BatDauLuc ? BatDauLuc : bayGio;
    }
}

public static class HuongDi
{
    public const string Vao = "VAO";
    public const string Ra = "RA";
}

public static class NguonGhi
{
    public const string CameraKhu = "CAMERA_KHU";
    public const string NguoiDung = "NGUOI_DUNG";
    public const string Admin = "ADMIN";
}

/// <summary>Một lần ghi nhận xe ở khu (bảng lịch sử vi_tri_xe). Xe máy: chỉ mức khu, không có ô.</summary>
public class ViTriXe
{
    public long Id { get; private set; }
    public int LuotGuiId { get; private set; }
    public int? CameraId { get; private set; }
    public int KhuId { get; private set; }
    public string? BienSoDocDuoc { get; private set; }
    public decimal? DoTinCay { get; private set; }
    public string Nguon { get; private set; } = NguonGhi.CameraKhu;
    public string? Huong { get; private set; }
    public DateTime ThayLuc { get; private set; }

    private ViTriXe() { }

    /// <summary>Camera lối vào khu: bắt buộc camera, biển đọc được và hướng (CHECK CK_vtx_camera_khu).</summary>
    public static ViTriXe TuCamera(int luotGuiId, int cameraId, int khuId, string bienSo, decimal? doTinCay, string huong, DateTime luc) =>
        new() { LuotGuiId = luotGuiId, CameraId = cameraId, KhuId = khuId, BienSoDocDuoc = bienSo, DoTinCay = doTinCay,
                Nguon = NguonGhi.CameraKhu, Huong = huong, ThayLuc = luc };

    /// <summary>Người dùng bấm "Đã đỗ xe" hoặc Admin gán / gỡ.</summary>
    public static ViTriXe ThuCong(int luotGuiId, int khuId, string nguon, string huong, DateTime luc) =>
        new() { LuotGuiId = luotGuiId, KhuId = khuId, Nguon = nguon, Huong = huong, ThayLuc = luc };
}

/// <summary>
/// Một lần xe đi sai khu (bảng di_sai_khu, V012): tới khu khác khu được chỉ (còn chỗ: chấp nhận), hoặc tới khu đã đầy (bị chặn).
/// Admin xem để biết ai hay đi sai; chưa phạt tự động. KhuChiDinhId null: xe không có phiên dẫn đường mà tới khu đầy.
/// </summary>
public class DiSaiKhu
{
    public long Id { get; private set; }
    public int LuotGuiId { get; private set; }
    public int XeId { get; private set; }
    public int ChuXeId { get; private set; }
    public string BienSo { get; private set; } = "";
    public int? KhuChiDinhId { get; private set; }
    public int KhuThucTeId { get; private set; }
    public bool KhuDay { get; private set; }
    public DateTime Luc { get; private set; }

    private DiSaiKhu() { }

    public static DiSaiKhu Tao(int luotGuiId, int xeId, int chuXeId, string bienSo, int? khuChiDinh, int khuThucTe, bool khuDay, DateTime luc) =>
        new() { LuotGuiId = luotGuiId, XeId = xeId, ChuXeId = chuXeId, BienSo = bienSo, KhuChiDinhId = khuChiDinh,
                KhuThucTeId = khuThucTe, KhuDay = khuDay, Luc = luc };
}

public static class MucCho
{
    public const string ConNhieu = "CON_NHIEU";
    public const string SapDay = "SAP_DAY";
    public const string Day = "DAY";
}

/// <summary>Quy tắc thuần (không DB) của TimXe: tính chỗ còn, mức chỗ, chọn khu, hướng đi qua lối vào khu.</summary>
public static class QuyTacKhu
{
    /// <summary>Đề cương v10: còn trên 20% là "Còn nhiều", từ 20% trở xuống là "Sắp đầy", hết là "Đầy".</summary>
    public static string Muc(int sucChua, int conLai, int phanTramSapDay)
    {
        if (conLai <= 0 || sucChua <= 0) return MucCho.Day;
        return conLai * 100 > sucChua * phanTramSapDay ? MucCho.ConNhieu : MucCho.SapDay;
    }

    public static int ConLai(int sucChua, int soXe, int soGiu) => Math.Max(0, sucChua - soXe - soGiu);

    /// <param name="ConLai">Chỗ còn THỰC TẾ = sức chứa − xe đang ở khu − suất đang giữ chưa hết hạn (xe đang được chỉ tới mà chưa tới).</param>
    /// <param name="KhoangCach">Độ dài đường đi từ cổng (Dijkstra trên đồ thị mốc); null: khu chưa nối đường đi.</param>
    public record UngVienKhu(int KhuId, int SucChua, int ConLai, double? KhoangCach);

    /// <summary>
    /// Chọn khu khi xe máy qua cổng (quy tắc cố định):
    /// 1) bỏ khu đã đầy (chỗ còn thực tế ≤ 0); 2) trong các khu "Còn nhiều" (&gt; phanTramSapDay % sức chứa), chọn khu GẦN cổng nhất;
    /// 3) không khu nào "Còn nhiều" thì chọn khu còn NHIỀU CHỖ NHẤT; 4) tất cả đầy: null (không chỉ đường).
    /// Bằng nhau thì khu gần hơn, rồi khu id nhỏ trước. Khu chưa nối đường đi xếp sau cùng khi so khoảng cách.
    /// </summary>
    public static int? ChonKhu(IEnumerable<UngVienKhu> ds, int phanTramSapDay = 20)
    {
        var con = ds.Where(k => k.ConLai > 0).ToList();
        var conNhieu = con.Where(k => Muc(k.SucChua, k.ConLai, phanTramSapDay) == MucCho.ConNhieu)
            .OrderBy(k => k.KhoangCach ?? double.MaxValue).ThenBy(k => k.KhuId).FirstOrDefault();
        if (conNhieu is not null) return conNhieu.KhuId;
        return con.OrderByDescending(k => k.ConLai).ThenBy(k => k.KhoangCach ?? double.MaxValue).ThenBy(k => k.KhuId)
                  .Select(k => (int?)k.KhuId).FirstOrDefault();
    }

    /// <summary>
    /// Camera VÀO có được +1 cho xe này không. Khu "đầy với xe này" khi sức chứa − xe đang ở khu − suất giữ của XE KHÁC ≤ 0
    /// (suất của chính xe ở khu này là chỗ đã dành cho nó). Đầy thì nhân viên không cho vào, camera không ghi.
    /// </summary>
    public static bool KhuDayVoiXe(int sucChua, int soXe, int soGiu, bool xeCoSuatOKhuNay) =>
        sucChua - soXe - (soGiu - (xeCoSuatOKhuNay ? 1 : 0)) <= 0;

    /// <summary>
    /// Lối vào khu dùng chung cho vào và ra: xe chưa ghi ở khu này thì là VÀO (đang ghi ở khu khác thì chuyển hẳn sang khu mới);
    /// xe đang ghi ở chính khu này thì là RA.
    /// </summary>
    public static string Huong(int? khuDangGhi, int khuCamera) => khuDangGhi == khuCamera ? HuongDi.Ra : HuongDi.Vao;

    /// <summary>
    /// Hướng theo vai trò camera: KHU_VAO (camera soi hướng vào) luôn là VÀO, KHU_RA (camera soi hướng ra) luôn là RA.
    /// Camera kiểu cũ LOI_VAO_KHU dùng chung hai hướng thì suy theo khu đang ghi (<see cref="Huong"/>).
    /// </summary>
    public static string HuongTheoCamera(string vaiTroCamera, int? khuDangGhi, int khuCamera) => vaiTroCamera switch
    {
        "KHU_VAO" => HuongDi.Vao,
        "KHU_RA" => HuongDi.Ra,
        _ => Huong(khuDangGhi, khuCamera),
    };

    /// <summary>Cùng biển, cùng camera trong khoảng chống đọc lặp thì bỏ qua (xe dừng trước camera).</summary>
    public static bool DocLap(DateTime? lanTruocCungCamera, DateTime bayGio, int giay) =>
        lanTruocCungCamera is DateTime t && (bayGio - t).TotalSeconds < giay;
}
