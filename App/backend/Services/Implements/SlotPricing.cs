using DatSan.Api.Repositories.Interface;

namespace DatSan.Api.Services.Implements;

public static class SlotPricing
{
    /// <summary>
    /// Tổng tiền cho các giờ [startHour, startHour + hours) theo bảng giá của cụm sân, môn và thứ trong tuần.
    /// Thiếu giá cho bất kỳ giờ nào thì trả null (khung giờ đó chưa mở bán).
    /// </summary>
    public static long? PriceFor(IEnumerable<PriceRuleInfo> rules, long venueId, string sport, DayOfWeek weekday,
        int startHour, int hours)
    {
        var dayRules = rules.Where(r => r.VenueId == venueId && r.Sport == sport && r.Weekday == (short)weekday).ToList();
        long total = 0;
        for (var hour = startHour; hour < startHour + hours; hour++)
        {
            var rule = dayRules.FirstOrDefault(r => r.StartHour <= hour && hour < r.EndHour);
            if (rule is null) return null;
            total += rule.PricePerHour;
        }
        return total;
    }

    /// <summary>Khung giờ nằm trọn trong giờ mở cửa. Đóng cửa 24:00 được lưu là 23:59:59 nên tính như 24:00.</summary>
    public static bool WithinOpeningHours(TimeOnly open, TimeOnly close, int startHour, int hours) =>
        TimeSpan.FromHours(startHour) >= open.ToTimeSpan()
        && TimeSpan.FromHours(startHour + hours) <= (close >= new TimeOnly(23, 59) ? TimeSpan.FromHours(24) : close.ToTimeSpan());
}
