using System.Globalization;

namespace NhaXe.Modules.CongRaVao.Domain;

/// <summary>Câu chữ thông báo của module Cổng ra vào (để một chỗ, dễ sửa, dễ test).</summary>
public static class NoiDungCong
{
    public static string DaVao(string bienSo, DateTime luc) => $"Xe {bienSo} đã vào bãi lúc {luc:HH:mm dd/MM/yyyy}.";
    public static string ChoXacNhan(string bienSo, int soGiay) =>
        $"Xe {bienSo} đang ở cổng ra. Mở mục Ra vào và xác nhận trong {soGiay} giây nếu đúng là bạn.";
    public static string DaRa(string bienSo, DateTime luc) => $"Xe {bienSo} đã ra khỏi bãi lúc {luc:HH:mm dd/MM/yyyy}.";
    /// <summary>Đề cương v10: "Xe [biển số] đã được [tên] xác nhận ra bãi lúc …".</summary>
    public static string DaRaBoi(string bienSo, string ten, DateTime luc) =>
        $"Xe {bienSo} đã được {ten} xác nhận ra bãi lúc {luc:HH:mm dd/MM/yyyy}.";
    public static string DaDuocXacNhan(string ten) => $"Đã được {ten} xác nhận.";
    public static string AdminMoTay(string bienSo, string lyDo, DateTime luc) =>
        $"Xe {bienSo} được Admin mở cổng ra lúc {luc:HH:mm dd/MM/yyyy}. Lý do: {lyDo}.";
    public static string ChoThanhToan(string bienSo, string ten, decimal soTien) =>
        $"Xe {bienSo} đã được {ten} xác nhận nhưng còn phí quá hạn {Tien(soTien)} đ. Trả phí trên ứng dụng, barrier sẽ mở ngay.";
    public static string HetGioChoAdmin(string bienSo) =>
        $"Xe {bienSo} ở cổng ra: quá 60 giây không ai xác nhận, cổng không mở. Admin kiểm tra và mở tay nếu hợp lệ.";
    /// <summary>
    /// Lý do cổng vào từ chối xe đã duyệt mà không có gói đang hiệu lực, tách theo lý do thật (thay cho câu chung
    /// "chưa có chỗ hoặc gói đã hết hạn"): chưa từng mua / gói đã hết hạn ngày … / gói đã mua nhưng chưa tới ngày bắt đầu.
    /// hetHanLuc: ngày kết thúc muộn nhất của các gói không bị huỷ (null = chưa có gói nào).
    /// </summary>
    public static string LyDoKhongCoGoi(DateTime? hetHanLuc, DateTime bayGio) => hetHanLuc switch
    {
        null => "Xe chưa có gói",
        var h when h <= bayGio => $"Gói đã hết hạn ngày {h:dd/MM/yyyy}",
        _ => "Gói chưa tới ngày bắt đầu",
    };

    /// <summary>Lời mời mua / gia hạn gửi người dùng xe, theo đúng lý do bị từ chối ở cổng vào.</summary>
    public static string MoiMuaGoi(string bienSo, DateTime? hetHanLuc, DateTime bayGio) => hetHanLuc switch
    {
        null => $"Xe {bienSo} vừa tới cổng vào nhưng chưa có gói. Mua gói trên ứng dụng rồi quay lại.",
        var h when h <= bayGio => $"Xe {bienSo} vừa tới cổng vào nhưng gói đã hết hạn ngày {h:dd/MM/yyyy}. Gia hạn gói trên ứng dụng rồi quay lại.",
        _ => $"Xe {bienSo} vừa tới cổng vào nhưng gói chưa tới ngày bắt đầu. Xem lịch sử gói trên ứng dụng.",
    };
    public static string TrungLuot(string bienSo) =>
        $"Xe {bienSo} tới cổng vào trong khi đang có lượt gửi chưa kết thúc. Kiểm tra biển trùng hoặc giả.";
    public static string KhongThayLuot(string bienSo) =>
        $"Cổng ra: biển {bienSo} không khớp lượt gửi nào đang mở. Kiểm tra và mở tay nếu hợp lệ.";
    public static string TenLoai(string loai) => loai switch { "O_TO" => "ô tô", "XE_MAY" => "xe máy", _ => "không rõ" };
    public static string KhongMo(string bienSo, string lyDo) => $"Cổng ra không mở cho xe {bienSo}: {lyDo}.";
    public static string NghiMat(string bienSo) =>
        $"Người dùng xe {bienSo} báo KHÔNG phải họ đang lấy xe ở cổng ra. Giữ xe lại và kiểm tra.";
    public static string ConNo(string bienSo, decimal soTien) =>
        $"Xe {bienSo} còn nợ phí quá hạn {Tien(soTien)} đ. Thanh toán trên app rồi quay lại cổng ra.";

    /// <summary>100000 → "100.000" (dấu chấm ngăn nghìn kiểu Việt Nam, không phụ thuộc máy chủ).</summary>
    public static string Tien(decimal soTien) => soTien.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');
}
