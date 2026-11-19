using NhaXe.SharedKernel;

namespace NhaXe.Modules.GoiCho.Contracts;

/// <summary>
/// Người có quyền dùng xe vừa trả xong phí quá hạn của lượt gửi đang mở (đề cương v10: "thanh toán xong thì barrier mới mở").
/// CongRaVao nghe để mở cổng cho lượt đang CHO_THANH_TOAN. GoiCho không gọi CongRaVao (tránh vòng phụ thuộc).
/// </summary>
public record DaTraPhiQuaHan(int LuotGuiId, int XeId, int TaiKhoanId, DateTime Luc) : ISuKien;
