namespace NhaXe.Modules.ThongBao.Contracts;

/// <summary>
/// Cổng giao tiếp CÔNG KHAI của module Thông báo (số 6). Module khác muốn báo cho người dùng thì gọi interface này,
/// KHÔNG tự INSERT vào bảng thong_bao. Sau này đổi sang đẩy thông báo lên điện thoại (FCM) hay qua RabbitMQ
/// thì chỉ sửa trong module ThongBao, các module gọi không đổi.
/// </summary>
public interface IThongBaoModule
{
    /// <summary>Gửi một thông báo cho một tài khoản. dich: người nhận bấm vào thì tới đâu (null = chỉ để đọc).</summary>
    Task GuiAsync(int taiKhoanId, string loai, string noiDung, DichThongBao? dich = null, CancellationToken ct = default);

    /// <summary>Gửi cùng một thông báo cho nhiều tài khoản (ví dụ chủ xe và người cùng nhóm được chia sẻ xe).</summary>
    Task GuiNhieuAsync(IEnumerable<int> taiKhoanIds, string loai, string noiDung, DichThongBao? dich = null, CancellationToken ct = default);

    /// <summary>Gửi cùng một thông báo cho mọi Admin (danh sách Admin do module ThongBao tự hỏi module TaiKhoan).</summary>
    Task GuiChoMoiAdminAsync(string loai, string noiDung, DichThongBao? dich = null, CancellationToken ct = default);
}

/// <summary>
/// Bấm vào thông báo thì đi đâu: HanhDong là việc / chỗ cần xem theo NGHIỆP VỤ (không phải đường dẫn trang, backend không biết
/// route của frontend), ThamChieuId là id đối tượng (luot_gui_id, xe_id...). Frontend quy đổi ở thong-bao/dichDen.ts.
/// </summary>
public record DichThongBao(string HanhDong, int? ThamChieuId = null);

/// <summary>Các hành động (cột thong_bao.hanh_dong). Tên khớp HANH_DONG_KHACH / HANH_DONG_ADMIN trong frontend dichDen.ts.</summary>
public static class HanhDongThongBao
{
    // Người dùng
    /// <summary>luot_gui_id: xe đang ở cổng ra, xác nhận trong 60 giây.</summary>
    public const string XacNhanRa = "XAC_NHAN_RA";
    /// <summary>luot_gui_id: xem lượt gửi (đã vào, đã ra, cổng không mở).</summary>
    public const string XemLuot = "XEM_LUOT";
    /// <summary>xe_id: trả phí quá hạn.</summary>
    public const string TraPhi = "TRA_PHI";
    /// <summary>luot_gui_id: mở màn hình chỉ đường tới khu.</summary>
    public const string DanDuong = "DAN_DUONG";
    /// <summary>xe_id: xe đang đỗ ở khu nào.</summary>
    public const string XemViTri = "XEM_VI_TRI";
    /// <summary>Sơ đồ chỗ trống (bãi hết chỗ, khu đầy mà không có chỉ dẫn).</summary>
    public const string XemSoDo = "XEM_SO_DO";
    /// <summary>xe_id: mua / gia hạn gói, chọn sẵn xe.</summary>
    public const string MuaGoi = "MUA_GOI";
    /// <summary>xe_id: xe bị từ chối, tải lại ảnh giấy đăng ký.</summary>
    public const string GuiLaiGiay = "GUI_LAI_GIAY";
    /// <summary>xe_id: xe bị gỡ / gói bị huỷ.</summary>
    public const string XemXe = "XEM_XE";
    /// <summary>Nhóm gia đình.</summary>
    public const string XemNhom = "XEM_NHOM";
    /// <summary>Dự phòng cho cảnh báo đăng nhập lạ (chưa có nơi gửi).</summary>
    public const string DoiMatKhau = "DOI_MAT_KHAU";

    // Admin
    /// <summary>xe_id: xe chờ duyệt.</summary>
    public const string DuyetXe = "DUYET_XE";
    /// <summary>xe_id: khung chi tiết xe (khách huỷ gói).</summary>
    public const string XemXeAdmin = "XEM_XE_ADMIN";
    /// <summary>luot_gui_id (có thể null): xử lý ở camera cổng ra, mở barie tay.</summary>
    public const string XuLyCongRa = "XU_LY_CONG_RA";
    /// <summary>Cổng vào: biển trùng lượt đang mở.</summary>
    public const string XemCongVao = "XEM_CONG_VAO";
    /// <summary>di_sai_khu.id: tô sáng dòng đi sai khu.</summary>
    public const string XemDiSaiKhu = "XEM_DI_SAI_KHU";
    /// <summary>Số xe các khu (mọi khu đầy, người dùng báo khác camera).</summary>
    public const string XemSoXeKhu = "XEM_SO_XE_KHU";
    /// <summary>Camera khu (xe sai loại đi qua).</summary>
    public const string XemCameraKhu = "XEM_CAMERA_KHU";

    public static readonly IReadOnlyCollection<string> TatCa = new[]
    {
        XacNhanRa, XemLuot, TraPhi, DanDuong, XemViTri, XemSoDo, MuaGoi, GuiLaiGiay, XemXe, XemNhom, DoiMatKhau,
        DuyetXe, XemXeAdmin, XuLyCongRa, XemCongVao, XemDiSaiKhu, XemSoXeKhu, XemCameraKhu,
    };
}

/// <summary>Các loại thông báo module khác được gửi (khớp CHECK CK_tb_loai trong nha-xe.sql).</summary>
public static class LoaiThongBao
{
    public const string HuyGoi = "HUY_GOI";
    public const string XeBiGo = "XE_BI_GO";
    public const string KhachHuy = "KHACH_HUY";
    public const string SapHetHan = "SAP_HET_HAN";
    public const string QuaHan = "QUA_HAN";
    public const string Vao = "VAO";
    public const string Ra = "RA";
    public const string NghiMat = "NGHI_MAT";
    public const string HetHan = "HET_HAN";
    /// <summary>Chỉ dẫn khu, camera ghi xe vào khu (module TimXe).</summary>
    public const string Khu = "KHU";
    /// <summary>Nhóm gia đình: có yêu cầu tham gia, được duyệt.</summary>
    public const string Nhom = "NHOM";
    /// <summary>Gửi Admin: biển lạ trong bãi, lệch khu, xe chưa định vị.</summary>
    public const string DoiSoat = "DOI_SOAT";
    /// <summary>Đã xác nhận ra nhưng còn phí quá hạn: trả phí rồi barrier mới mở.</summary>
    public const string ChoThanhToan = "CHO_THANH_TOAN";
    /// <summary>Gửi Admin: có xe mới (đã có ảnh giấy đăng ký) hoặc xe gửi lại sau khi bị từ chối, đang chờ duyệt (V011).</summary>
    public const string XeChoDuyet = "XE_CHO_DUYET";
    /// <summary>Gửi chủ xe: xe được duyệt / bị từ chối (V011).</summary>
    public const string KetQuaDuyet = "KET_QUA_DUYET";
}
