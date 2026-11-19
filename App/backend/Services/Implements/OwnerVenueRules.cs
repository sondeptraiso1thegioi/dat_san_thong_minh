using System.Globalization;
using System.Text.RegularExpressions;
using DatSan.Api.DTOs;
using DatSan.Api.Middleware;
using DatSan.Api.Models;

namespace DatSan.Api.Services.Implements;

/// <summary>Dữ liệu cụm sân sau khi đã kiểm tra và chuẩn hóa.</summary>
public sealed record ValidVenue(
    string Name, string Address, double Lat, double Lng, TimeOnly OpenTime, TimeOnly CloseTime,
    IReadOnlyList<string> Amenities, string? Description,
    IReadOnlyList<OwnerCourtInput> Courts, IReadOnlyList<OwnerPriceInput> Prices);

/// <summary>Quy tắc nghiệp vụ khi chủ sân tạo/sửa cụm sân (UC04). Tách riêng để kiểm thử không cần database.</summary>
public static class OwnerVenueRules
{
    public static readonly string[] Sports = ["tennis", "pickleball", "badminton", "table_tennis"];
    public static readonly string[] Environments = ["indoor", "covered", "outdoor"];
    public static readonly string[] AmenityCodes = ["gui_xe", "nuoc_uong", "phong_thay_do", "cho_thue_vot", "nha_tam"];
    public const int MaxCourts = 30;
    public const long MinPrice = 1_000;
    public const long MaxPrice = 10_000_000;

    // Cột time(0) không lưu được 24:00, nên "24:00" được lưu là 23:59:59 và hiển thị lại là 24:00.
    public static readonly TimeOnly Midnight = new(23, 59, 59);
    private static readonly Regex TimePattern = new(@"^(?:([01]\d|2[0-3]):(00|30)|24:00)$", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);
    private static readonly Dictionary<string, string> SportNames = new()
    {
        ["tennis"] = "Tennis", ["pickleball"] = "Pickleball", ["badminton"] = "Cầu lông", ["table_tennis"] = "Bóng bàn",
    };

    /// <summary>Đã duyệt mà đổi tên/địa chỉ/vị trí thì phải duyệt lại; bị từ chối mà sửa thì coi như gửi lại.</summary>
    public static string NextStatus(string current, bool identityChanged) => current switch
    {
        "pending" or "rejected" => "pending",
        "approved" => identityChanged ? "pending" : "approved",
        _ => throw new ApiException(409, "VENUE_DELETED", "Cụm sân đã bị ẩn nên không sửa được. Vui lòng liên hệ quản trị viên."),
    };

    public static bool IdentityChanged(Venue venue, ValidVenue input) =>
        venue.Name != input.Name || venue.Address != input.Address
        || Math.Abs(venue.Lat - input.Lat) > 1e-6 || Math.Abs(venue.Lng - input.Lng) > 1e-6;

    public static string FormatTime(TimeOnly time) =>
        time >= new TimeOnly(23, 59) ? "24:00" : time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <param name="existingCourts">Sân con hiện có (khi sửa) để kiểm tra id và tên trùng với sân không gửi lên.</param>
    public static ValidVenue Validate(OwnerVenueInput input, IReadOnlyCollection<Court> existingCourts)
    {
        var name = Spaces.Replace((input.Name ?? "").Trim(), " ");
        if (name.Length is < 3 or > 150) throw Invalid("Tên cụm sân từ 3 đến 150 ký tự.");
        var address = Spaces.Replace((input.Address ?? "").Trim(), " ");
        if (address.Length is < 5 or > 255) throw Invalid("Địa chỉ từ 5 đến 255 ký tự.");
        if (!double.IsFinite(input.Lat) || !double.IsFinite(input.Lng) || input.Lat is < 8 or > 24 || input.Lng is < 102 or > 110)
            throw Invalid("Vị trí đã ghim nằm ngoài khu vực Sun Sport hỗ trợ. Vui lòng ghim đúng vị trí cụm sân trên bản đồ.");

        var open = ParseTime(input.OpenTime, "Giờ mở cửa");
        var close = ParseTime(input.CloseTime, "Giờ đóng cửa");
        if (open == Midnight) throw Invalid("Giờ mở cửa không hợp lệ.");
        if (close <= open) throw Invalid("Giờ đóng cửa phải sau giờ mở cửa.");

        var amenities = (input.Amenities ?? []).Select(a => a.Trim()).Distinct().ToList();
        var unknown = amenities.FirstOrDefault(a => !AmenityCodes.Contains(a));
        if (unknown is not null) throw Invalid($"Tiện ích không hợp lệ: {unknown}.");
        var description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        if (description?.Length > 2000) throw Invalid("Mô tả tối đa 2000 ký tự.");

        var courts = ValidateCourts(input.Courts ?? [], existingCourts);
        var activeSports = courts.Where(c => c.IsActive).Select(c => c.Sport!).Distinct().ToList();
        var prices = ValidatePrices(input.Prices ?? [], activeSports);
        return new ValidVenue(name, address, input.Lat, input.Lng, open, close, amenities, description, courts, prices);
    }

    private static List<OwnerCourtInput> ValidateCourts(List<OwnerCourtInput> input, IReadOnlyCollection<Court> existing)
    {
        if (input.Count > MaxCourts) throw Invalid($"Mỗi cụm sân tối đa {MaxCourts} sân con.");
        var result = new List<OwnerCourtInput>();
        foreach (var court in input)
        {
            if (court.Sport is null || !Sports.Contains(court.Sport)) throw Invalid("Môn của sân con không hợp lệ.");
            if (court.Environment is null || !Environments.Contains(court.Environment)) throw Invalid("Loại sân con phải là trong nhà, có mái che hoặc ngoài trời.");
            var courtName = Spaces.Replace((court.Name ?? "").Trim(), " ");
            if (courtName.Length is < 1 or > 50) throw Invalid("Tên sân con từ 1 đến 50 ký tự.");
            if (court.Id is not null)
            {
                var current = existing.FirstOrDefault(c => c.Id.ToString(CultureInfo.InvariantCulture) == court.Id)
                    ?? throw Invalid("Sân con không thuộc cụm sân này.");
                // Đổi môn của sân đã có booking làm sai lịch sử đặt sân; muốn đổi môn thì ngừng sân cũ và thêm sân mới.
                if (current.Sport != court.Sport) throw Invalid($"Không đổi được môn của sân \"{current.Name}\". Hãy ngừng sân này và thêm sân mới.");
            }
            result.Add(new OwnerCourtInput { Id = court.Id, Sport = court.Sport, Name = courtName, Environment = court.Environment, IsActive = court.IsActive });
        }
        if (result.Count(c => c.Id is not null) != result.Where(c => c.Id is not null).Select(c => c.Id).Distinct().Count())
            throw Invalid("Một sân con bị gửi lặp lại.");
        if (!result.Any(c => c.IsActive)) throw Invalid("Cần ít nhất một sân con đang hoạt động.");

        // Tên sân con là duy nhất trong cụm (uq_courts_venue_name), tính cả sân cũ không gửi lên.
        var keptNames = existing.Where(c => !result.Any(r => r.Id == c.Id.ToString(CultureInfo.InvariantCulture))).Select(c => c.Name);
        var duplicate = result.Select(c => c.Name).Concat(keptNames)
            .GroupBy(n => n, StringComparer.CurrentCultureIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw Invalid($"Tên sân con \"{duplicate.Key}\" bị trùng.");
        return result;
    }

    private static List<OwnerPriceInput> ValidatePrices(List<OwnerPriceInput> input, List<string> activeSports)
    {
        foreach (var price in input)
        {
            if (price.Sport is null || !Sports.Contains(price.Sport)) throw Invalid("Môn trong bảng giá không hợp lệ.");
            if (!activeSports.Contains(price.Sport))
                throw Invalid($"Bảng giá có môn {SportNames[price.Sport]} nhưng cụm sân không có sân {SportNames[price.Sport]} đang hoạt động.");
            if (price.Weekday is < 0 or > 6) throw Invalid("Ngày trong tuần không hợp lệ.");
            if (price.StartHour < 0 || price.EndHour > 24 || price.EndHour <= price.StartHour)
                throw Invalid("Khung giờ trong bảng giá không hợp lệ.");
            if (price.PricePerHour is < MinPrice or > MaxPrice)
                throw Invalid("Giá mỗi giờ từ 1.000đ đến 10.000.000đ.");
            if (price.PricePerHour % 1000 != 0) throw Invalid("Giá mỗi giờ phải là bội số của 1.000đ.");
        }
        // Không chồng giờ trong cùng môn và cùng ngày, giống trigger trg_price_rules_no_overlap.
        foreach (var group in input.GroupBy(p => (Sport: p.Sport!, p.Weekday)))
        {
            var ordered = group.OrderBy(p => p.StartHour).ToList();
            for (var i = 1; i < ordered.Count; i++)
                if (ordered[i].StartHour < ordered[i - 1].EndHour)
                    throw Invalid($"Bảng giá {SportNames[group.Key.Sport]} ngày {DayName(group.Key.Weekday)} bị chồng giờ " +
                        $"({ordered[i - 1].StartHour:00}:00–{ordered[i - 1].EndHour:00}:00 và {ordered[i].StartHour:00}:00–{ordered[i].EndHour:00}:00).");
        }
        var missing = activeSports.FirstOrDefault(s => !input.Any(p => p.Sport == s));
        if (missing is not null) throw Invalid($"Môn {SportNames[missing]} chưa có bảng giá.");
        return input;
    }

    private static TimeOnly ParseTime(string? value, string label)
    {
        var text = (value ?? "").Trim();
        if (!TimePattern.IsMatch(text)) throw Invalid($"{label} phải có dạng HH:mm, phút là 00 hoặc 30.");
        return text == "24:00" ? Midnight : TimeOnly.ParseExact(text, "HH:mm", CultureInfo.InvariantCulture);
    }

    public static string DayName(int weekday) => weekday == 0 ? "Chủ nhật" : $"Thứ {weekday + 1}";

    private static ApiException Invalid(string message) => new(400, "VALIDATION_ERROR", message);
}
