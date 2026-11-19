namespace NhaXe.Modules.BaiXe.Contracts;

/// <summary>
/// Cổng giao tiếp CÔNG KHAI của module Bãi xe (số 7). Module AI ô trống (số 10) sẽ gọi interface này để báo
/// ô nào trống / có xe, KHÔNG ghi thẳng bảng trang_thai_o. Hôm nay chưa có AI nên Admin đặt tay trên trang Quản lý bãi.
/// </summary>
public interface IBaiXeModule
{
    /// <summary>Cập nhật trạng thái một ô. trangThai: TRONG, CO_XE, KHONG_RO. Ô không tồn tại thì báo lỗi KHONG_TIM_THAY_O.</summary>
    Task CapNhatTrangThaiOAsync(int oId, string trangThai, CancellationToken ct = default);

    /// <summary>Mọi khu, kèm sức chứa và khoảng cách đường đi từ cổng vào (Dijkstra; null khi chưa nối lối đi).</summary>
    Task<IReadOnlyList<KhuThongTin>> LayKhuAsync(CancellationToken ct = default);

    /// <summary>Một camera (TimXe hỏi camera lối vào khu thuộc khu nào). null: không có camera này.</summary>
    Task<CameraThongTin?> LayCameraAsync(int cameraId, CancellationToken ct = default);

    /// <summary>Đường ngắn nhất từ cổng vào tới đầu khu (UC-U18). null: khu chưa có nút lối đi hoặc không đi tới được.</summary>
    Task<DuongDiDto?> TimDuongAsync(int khuId, CancellationToken ct = default);
}

/// <param name="MaSoDo">Mã chấm trên ảnh sơ đồ (R, S, HAM). null: khu không có chấm.</param>
/// <param name="DangDung">false: khu tạm tắt, không chọn khi chỉ dẫn và không hiện cho người dùng.</param>
public record KhuThongTin(int Id, string Ten, int LoaiXeId, int SucChua, double? KhoangCachTuCong, string? MaSoDo = null, bool DangDung = true);
public record CameraThongTin(int Id, int KhuId, string Ten, string VaiTro, bool DangBat);
public record DiemDuong(int NutId, string Ten, double X, double Y);
public record DuongDiDto(int KhuId, double TongMet, IReadOnlyList<DiemDuong> Diem);

/// <summary>Các trạng thái ô (khớp CHECK CK_tto_trang_thai trong nha-xe.sql).</summary>
public static class VaiTroCameraCongKhai
{
    /// <summary>Camera khu soi hướng xe đi vào khu.</summary>
    public const string KhuVao = "KHU_VAO";
    /// <summary>Camera khu soi hướng xe đi ra khỏi khu.</summary>
    public const string KhuRa = "KHU_RA";
    /// <summary>Kiểu cũ: một camera ở lối vào khu dùng chung hai hướng.</summary>
    public const string LoiVaoKhu = "LOI_VAO_KHU";
    public const string GiamSat = "GIAM_SAT";
}

public static class TrangThaiOCongKhai
{
    public const string Trong = "TRONG";
    public const string CoXe = "CO_XE";
    public const string KhongRo = "KHONG_RO";
}
