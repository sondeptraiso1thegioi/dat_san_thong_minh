using System.Globalization;
using System.Text;
using System.Text.Json;
using DatSan.Api.DTOs;
using DatSan.Api.Models;
using DatSan.Api.Repositories.Interface;
using DatSan.Api.Utils;

namespace DatSan.Api.Services.Implements;

/// <summary>Phần dùng chung giữa Trang chủ và Tìm sân: vị trí, thẻ cụm sân, sắp xếp.</summary>
public static class VenueCards
{
    public static readonly StringComparer NameOrder = StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), true);

    // Ưu tiên vị trí thiết bị gửi lên, sau đó vị trí đã ghim trong hồ sơ.
    public static HomeLocationDto ResolveLocation(User user, double? lat, double? lng) =>
        lat is double deviceLat && lng is double deviceLng
            ? new HomeLocationDto("device", null, deviceLat, deviceLng)
            : user.Lat is double profileLat && user.Lng is double profileLng
                ? new HomeLocationDto("profile", user.Address, profileLat, profileLng)
                : new HomeLocationDto("none", user.Address, null, null);

    public static VenueCardDto ToCard(VenueSummary v, HomeLocationDto location, TimeOnly localTime)
    {
        double? distance = location.Lat is double lat && location.Lng is double lng
            ? Math.Round(Geo.DistanceKm(lat, lng, v.Lat, v.Lng), 1)
            : null;
        return new VenueCardDto(
            v.Id.ToString(CultureInfo.InvariantCulture), v.Name, v.Address, v.Lat, v.Lng, distance, v.Sports,
            v.Environments.Any(IsSheltered), v.MinPricePerHour,
            ReadStrings(v.PhotosJson).FirstOrDefault(), v.IsVip,
            OwnerVenueRules.FormatTime(v.OpenTime), OwnerVenueRules.FormatTime(v.CloseTime),
            localTime >= v.OpenTime && (localTime < v.CloseTime || v.CloseTime >= OwnerVenueRules.Midnight),
            ReadStrings(v.AmenitiesJson));
    }

    /// <summary>Có khoảng cách thì gần trước; không có vị trí thì theo tên. VIP không được đẩy lên.</summary>
    public static List<T> Order<T>(IEnumerable<T> items, Func<T, VenueCardDto> card) =>
        items.OrderBy(x => card(x).DistanceKm ?? double.MaxValue).ThenBy(x => card(x).Name, NameOrder).ToList();

    public static bool IsSheltered(string environment) => environment is "indoor" or "covered";

    // JSON lỗi hoặc không phải mảng chuỗi thì coi như rỗng, không làm hỏng cả trang.
    public static IReadOnlyList<string> ReadStrings(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    /// <summary>Bỏ dấu tiếng Việt và viết thường, để "cau giay" tìm được "Cầu Giấy".</summary>
    public static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(c);
        return builder.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D').ToLowerInvariant();
    }
}
