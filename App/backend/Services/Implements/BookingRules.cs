using System.Globalization;
using DatSan.Api.Utils;

namespace DatSan.Api.Services.Implements;

/// <summary>Quy tắc đặt sân dùng chung giữa Tìm sân, Chi tiết cụm sân và Giữ chỗ.</summary>
public static class BookingRules
{
    /// <summary>Mỗi lần đặt 1–3 giờ liên tiếp trên một sân con, bắt đầu đúng giờ chẵn.</summary>
    public const int MaxHours = 3;
    public const int MaxDaysAhead = 30;
    public const int DefaultHoldMinutes = 10;
    public const string ExpiredHoldReason = "Hết thời gian giữ chỗ";
    public const string PlayerCancelReason = "Người chơi hủy giữ chỗ";
    public const string PlayerPaidCancelReason = "Người chơi hủy booking trước giờ chơi ít nhất 24 giờ; chờ xử lý hoàn tiền";
    public static readonly TimeSpan PaidBookingCancellationWindow = TimeSpan.FromHours(24);

    /// <summary>Thời điểm bắt đầu khung giờ theo giờ Việt Nam.</summary>
    public static DateTimeOffset SlotStart(DateOnly date, int hour) =>
        new(date.ToDateTime(TimeOnly.MinValue).AddHours(hour), VietnamTime.Offset);

    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(VietnamTime.ToLocal(now).DateTime);

    /// <summary>ai_settings.booking_hold_minutes; giá trị lỗi hoặc ngoài 1–60 phút thì dùng 10 phút.</summary>
    public static int ParseHoldMinutes(string? value) =>
        int.TryParse(value?.Trim().Trim('"'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes)
            && minutes is >= 1 and <= 60 ? minutes : DefaultHoldMinutes;

    /// <summary>
    /// Trạng thái hiển thị: booking pending đã quá hạn giữ chỗ vẫn là 'pending' trong DB
    /// cho tới khi được dọn, nhưng với người dùng là 'expired'.
    /// </summary>
    public static string EffectiveStatus(string status, DateTimeOffset? holdExpiresAt, DateTimeOffset now) =>
        status == "pending" && (holdExpiresAt is null || holdExpiresAt <= now) ? "expired" : status;

    /// <summary>Trả về quyền hủy và thông tin hiển thị; backend là nguồn quyết định cuối cùng.</summary>
    public static (bool CanCancel, string? Info) CancellationAvailability(
        string status, DateTimeOffset? holdExpiresAt, DateTimeOffset startAt, string? paymentStatus, DateTimeOffset now)
    {
        var effective = EffectiveStatus(status, holdExpiresAt, now);
        if (effective == "pending")
            return (true, "Bạn có thể hủy giữ chỗ trước khi hết thời gian.");
        if (effective == "confirmed")
        {
            if (paymentStatus != "paid")
                return (false, "Chưa tìm thấy giao dịch đã thanh toán cho booking này.");
            if (startAt < now.Add(PaidBookingCancellationWindow))
                return (false, "Chỉ có thể hủy booking đã thanh toán khi còn ít nhất 24 giờ trước giờ chơi.");
            return (true, "Khi hủy, giao dịch sẽ chuyển sang chờ hoàn tiền để Admin xử lý.");
        }
        if (effective == "expired") return (false, "Thời gian giữ chỗ đã hết.");
        if (effective == "cancelled") return (false, "Booking này đã được hủy.");
        if (effective == "completed") return (false, "Booking đã hoàn thành nên không thể hủy.");
        return (false, "Booking này hiện không thể hủy.");
    }

    /// <summary>Hai khoảng [aStart, aEnd) và [bStart, bEnd) giao nhau — cùng điều kiện với trigger trg_bookings_no_overlap.</summary>
    public static bool Overlaps(DateTimeOffset aStart, DateTimeOffset aEnd, DateTimeOffset bStart, DateTimeOffset bEnd) =>
        aStart < bEnd && bStart < aEnd;
}
