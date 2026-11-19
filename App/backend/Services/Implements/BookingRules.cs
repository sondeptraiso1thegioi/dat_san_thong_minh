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

    /// <summary>Hai khoảng [aStart, aEnd) và [bStart, bEnd) giao nhau — cùng điều kiện với trigger trg_bookings_no_overlap.</summary>
    public static bool Overlaps(DateTimeOffset aStart, DateTimeOffset aEnd, DateTimeOffset bStart, DateTimeOffset bEnd) =>
        aStart < bEnd && bStart < aEnd;
}
