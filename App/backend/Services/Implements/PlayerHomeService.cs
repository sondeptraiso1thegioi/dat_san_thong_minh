using System.Globalization;
using DatSan.Api.DTOs;
using DatSan.Api.Middleware;
using DatSan.Api.Repositories.Interface;
using DatSan.Api.Services.Interface;
using DatSan.Api.Utils;

namespace DatSan.Api.Services.Implements;

public sealed class PlayerHomeService(
    IUserRepository users, IVenueRepository venues, IBookingRepository bookings,
    ISettingRepository settings, TimeProvider clock) : IPlayerHomeService
{
    public const int SectionSize = 6;
    public const double FallbackRadiusKm = 5;

    public async Task<PlayerHomeResponse> GetHomeAsync(
        long userId, double? lat, double? lng, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var user = await users.FindByIdAsync(userId, cancellationToken)
            ?? throw new ApiException(401, "UNAUTHORIZED", "Vui lòng đăng nhập lại.");

        var location = VenueCards.ResolveLocation(user, lat, lng);
        var radiusKm = ParseRadius(await settings.GetValueAsync("default_radius_km", cancellationToken));
        var localTime = TimeOnly.FromTimeSpan(VietnamTime.ToLocal(now).TimeOfDay);

        var cards = (await venues.GetApprovedSummariesAsync(now, cancellationToken))
            .Select(v => VenueCards.ToCard(v, location, localTime));
        var ordered = VenueCards.Order(cards, c => c);

        // Sân nổi bật = sân VIP còn hạn trong bán kính mặc định (UC06). "Sân gần bạn" giữ đúng thứ tự khoảng cách.
        var featured = ordered.Where(c => c.IsVip && (c.DistanceKm is null || c.DistanceKm <= radiusKm))
            .Take(SectionSize).ToList();
        var nearby = ordered.Take(SectionSize).ToList();
        var upcoming = await bookings.GetNextUpcomingAsync(userId, now, cancellationToken);
        return new PlayerHomeResponse(location, radiusKm, featured, nearby, upcoming is null ? null : ToDto(upcoming));
    }

    private static UpcomingBookingDto ToDto(UpcomingBooking b) => new(
        b.Id.ToString(CultureInfo.InvariantCulture), b.VenueId.ToString(CultureInfo.InvariantCulture),
        b.VenueName, b.VenueAddress, b.CourtName, b.Sport, b.Environment,
        b.StartAt, b.EndAt, b.Status, b.Price, b.HoldExpiresAt);

    public static double ParseRadius(string? value) =>
        double.TryParse(value?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var radius)
            && radius is > 0 and <= 100 ? radius : FallbackRadiusKm;
}
