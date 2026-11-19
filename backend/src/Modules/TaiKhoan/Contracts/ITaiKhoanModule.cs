namespace NhaXe.Modules.TaiKhoan.Contracts;

/// <summary>
/// Cổng giao tiếp CÔNG KHAI của module Tài khoản (Buổi 12).
/// Module khác chỉ được gọi qua interface này, không được đọc bảng tai_khoan hay class nội bộ.
/// Sau này tách Tài khoản thành service riêng thì chỉ cần thay cài đặt bằng một HTTP/gRPC client.
/// </summary>
public interface ITaiKhoanModule
{
    Task<IReadOnlyDictionary<int, string>> LayHoTenAsync(IEnumerable<int> taiKhoanIds, CancellationToken ct = default);

    /// <summary>Họ tên và số điện thoại (dùng khi soạn thông báo, ví dụ báo Admin "ai đã huỷ gói").</summary>
    Task<IReadOnlyDictionary<int, ThongTinLienHe>> LayLienHeAsync(IEnumerable<int> taiKhoanIds, CancellationToken ct = default);

    /// <summary>Id của mọi tài khoản Admin (để module ThongBao gửi thông báo cho Admin).</summary>
    Task<IReadOnlyList<int>> LayAdminIdsAsync(CancellationToken ct = default);

    /// <summary>Phiên trong token còn là phiên hiện tại của tài khoản không (1 tài khoản đăng nhập 1 máy).</summary>
    Task<bool> PhienConHieuLucAsync(int taiKhoanId, string phien, CancellationToken ct = default);

    /// <summary>Mã đẩy (FCM) của máy đăng nhập gần nhất; tài khoản không có mã thì không có trong kết quả (v10).</summary>
    Task<IReadOnlyDictionary<int, string>> LayMaDayAsync(IEnumerable<int> taiKhoanIds, CancellationToken ct = default);
}

/// <summary>Thông tin liên hệ để hiển thị cho Admin. KHÔNG có mật khẩu, mã khôi phục hay thông tin nhạy cảm khác.</summary>
public record ThongTinLienHe(int Id, string HoTen, string Sdt, string? Email = null, DateTime? TaoLuc = null);
