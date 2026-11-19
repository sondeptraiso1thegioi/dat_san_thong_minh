using NhaXe.SharedKernel;

namespace NhaXe.Modules.CongRaVao.Contracts;

// Sự kiện CongRaVao PHÁT ra sau khi đã lưu lượt gửi. Module khác (GoiCho) nghe để tự cập nhật dữ liệu của mình,
// không phải hỏi ngược CongRaVao. CongRaVao không biết ai nghe.

/// <summary>Cổng vào đã cho xe qua, lượt gửi đã được mở.</summary>
public record XeDaVao(int LuotGuiId, int XeId, DateTime Luc) : ISuKien;

/// <summary>Cổng ra đã mở (người dùng xe xác nhận, trả phí xong, hoặc Admin mở tay), lượt gửi đã đóng, khu đã xoá.</summary>
public record XeDaRa(int LuotGuiId, int XeId, DateTime Luc) : ISuKien;
