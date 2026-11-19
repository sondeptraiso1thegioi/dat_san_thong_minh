using System.Globalization;

namespace NhaXe.Modules.GoiCho.Domain;

/// <summary>
/// Nội dung các thông báo do module GoiCho sinh ra. GoiCho chỉ SOẠN nội dung rồi gửi qua IThongBaoModule;
/// việc lưu thuộc module ThongBao. Trước đây phần này nằm trong thủ tục SQL (INSERT thẳng vào thong_bao).
/// </summary>
public static class NoiDungThongBao
{
    /// <summary>30000 → "30.000" (giống FORMAT(..., 'N0', 'vi-VN') của SQL Server, không phụ thuộc ngôn ngữ của máy chủ).</summary>
    public static string Tien(decimal soTien) => soTien.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');

    public static string KhachDaHuy(string tenGoi, string bienSo, decimal soTienHoan) =>
        $"Đã huỷ gói {tenGoi} của xe {bienSo}, hoàn {Tien(soTienHoan)} đ.";

    /// <summary>Báo cho Admin: ai huỷ, lý do, góp ý (giúp Admin biết vì sao khách bỏ gói).</summary>
    public static string BaoAdminKhachHuy(string hoTen, string sdt, string tenGoi, string bienSo, decimal soTienHoan,
                                          string? lyDo, string? gopY) =>
        $"{hoTen} ({sdt}) đã huỷ gói {tenGoi} của xe {bienSo}, hoàn {Tien(soTienHoan)} đ. Lý do: {lyDo ?? "không chọn"}"
        + (gopY is null ? ". Không có góp ý." : $". Góp ý: “{gopY}”");

    public static string XeBiGo(string bienSo, string lyDo) =>
        $"Xe {bienSo} bị gỡ khỏi hệ thống. Lý do: {lyDo}. Gói còn hạn đã bị huỷ, không hoàn tiền.";

    public static string SapHetHan(string tenGoi, string bienSo, DateTime hetHanLuc) =>
        $"Gói {tenGoi} của xe {bienSo} sẽ hết hạn lúc {hetHanLuc:HH:mm dd/MM/yyyy}. Gia hạn sớm để giữ suất trong bãi.";

    public static string DaHetHan(string tenGoi, string bienSo, decimal? phiNeuTrongBai) =>
        phiNeuTrongBai is null
            ? $"Gói {tenGoi} của xe {bienSo} đã hết hạn. Mua gói mới để tiếp tục gửi xe."
            : $"Gói {tenGoi} của xe {bienSo} đã hết hạn khi xe còn trong bãi. Từ hôm nay tính phí quá hạn theo ngày; " +
              "gia hạn hoặc lấy xe sớm để không phát sinh thêm.";

    public static string NhacQuaHan(string bienSo, int soNgay, decimal soTien) =>
        $"Xe {bienSo} đã quá hạn {soNgay} ngày, phí quá hạn hiện tại {soTien:N0} đ. Trả phí trên ứng dụng trước khi lấy xe.";

    /// <summary>"xe máy" / "ô tô" từ mã loại xe (bảng loai_xe).</summary>
    public static string TenLoai(string? maLoai) => maLoai switch { "XE_MAY" => "xe máy", "O_TO" => "ô tô", _ => maLoai ?? "xe" };

    /// <summary>Báo Admin: xe vừa có ảnh giấy đăng ký (lần đầu) hoặc chủ xe gửi lại sau khi bị từ chối.</summary>
    public static string XeChoDuyet(string bienSo, string tenLoai, string hoTen, bool guiLai) => guiLai
        ? $"Xe {bienSo} ({tenLoai}) của {hoTen} đã gửi lại ảnh giấy đăng ký sau khi bị từ chối, đang chờ duyệt lại."
        : $"Có xe mới chờ duyệt: {bienSo} ({tenLoai}) của {hoTen}.";

    public static string XeDaDuyet(string bienSo) => $"Xe {bienSo} đã được duyệt, bạn có thể mua gói.";

    public static string XeBiTuChoi(string bienSo, string lyDo) =>
        $"Xe {bienSo} bị từ chối: {lyDo}. Bạn có thể tải lại ảnh giấy đăng ký để gửi duyệt lại.";

    public static string AdminDaHuy(string tenGoi, string bienSo, string lyDo, decimal soTienHoan) =>
        $"Gói {tenGoi} của xe {bienSo} bị Admin huỷ. Lý do: {lyDo}"
        + (soTienHoan > 0 ? $". Hoàn {Tien(soTienHoan)} đ." : ". Không hoàn tiền.");
}
