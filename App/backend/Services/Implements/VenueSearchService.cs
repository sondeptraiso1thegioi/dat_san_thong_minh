using System.Globalization;
using DatSan.Api.DTOs;
using DatSan.Api.Middleware;
using DatSan.Api.Repositories.Interface;
using DatSan.Api.Services.Interface;
using DatSan.Api.Utils;

namespace DatSan.Api.Services.Implements;

public sealed class VenueSearchService(
    IUserRepository users, IVenueRepository venues, IBookingRepository bookings,
    ISettingRepository settings, TimeProvider clock) : IVenueSearchService
{
    public const int MaxResults = 50;
    public const int MaxHours = BookingRules.MaxHours;
    public const int MaxDaysAhead = BookingRules.MaxDaysAhead;
    private static readonly string[] Sports = ["tennis", "pickleball", "badminton", "table_tennis"];

    public async Task<VenueSearchResponse> SearchAsync(long userId, VenueSearchQuery query, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var localNow = VietnamTime.ToLocal(now);
        Validate(query, localNow);
        var user = await users.FindByIdAsync(userId, cancellationToken)
            ?? throw new ApiException(401, "UNAUTHORIZED", "Vui lòng đăng nhập lại.");
        var location = VenueCards.ResolveLocation(user, query.Lat, query.Lng);
        double? radiusKm = location.Lat is null ? null
            : query.RadiusKm ?? PlayerHomeService.ParseRadius(await settings.GetValueAsync("default_radius_km", cancellationToken));

        var summaries = await venues.GetApprovedSummariesAsync(now, cancellationToken);
        var courtsByVenue = (await venues.GetActiveCourtsAsync(cancellationToken)).ToLookup(c => c.VenueId);
        var slot = BuildSlot(query);
        IReadOnlySet<long> busy = new HashSet<long>();
        IReadOnlyList<PriceRuleInfo> rules = [];
        if (slot is not null)
        {
            busy = await bookings.GetBusyCourtIdsAsync(slot.StartAt, slot.EndAt, now, cancellationToken);
            rules = await venues.GetPriceRulesAsync(cancellationToken);
        }

        var text = string.IsNullOrWhiteSpace(query.Q) ? null : VenueCards.Fold(query.Q.Trim());
        var wantedAmenities = (query.Amenities ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var localTime = TimeOnly.FromTimeSpan(localNow.TimeOfDay);
        var results = new List<VenueSearchItemDto>();

        foreach (var summary in summaries)
        {
            if (text is not null && !VenueCards.Fold($"{summary.Name} {summary.Address}").Contains(text)) continue;
            var amenities = VenueCards.ReadStrings(summary.AmenitiesJson);
            if (!wantedAmenities.All(amenities.Contains)) continue;

            // Sân con phù hợp với môn và yêu cầu có mái (xét theo từng sân con, không theo cả cụm).
            var courts = courtsByVenue[summary.Id]
                .Where(c => query.Sport is null || c.Sport == query.Sport)
                .Where(c => !query.Sheltered || VenueCards.IsSheltered(c.Environment))
                .ToList();
            if (courts.Count == 0) continue;

            var card = VenueCards.ToCard(summary, location, localTime);
            if (radiusKm is double radius && card.DistanceKm > radius) continue;

            int? freeCourts = null;
            long? slotPrice = null;
            if (slot is not null)
            {
                if (!SlotPricing.WithinOpeningHours(summary.OpenTime, summary.CloseTime, query.StartHour!.Value, slot.Hours)) continue;
                var prices = courts.Where(c => !busy.Contains(c.Id))
                    .Select(c => SlotPricing.PriceFor(rules, summary.Id, c.Sport, query.Date!.Value.DayOfWeek, query.StartHour.Value, slot.Hours))
                    .Where(p => p is not null)
                    .Select(p => p!.Value)
                    .ToList();
                if (prices.Count == 0) continue;
                freeCourts = prices.Count;
                slotPrice = prices.Min();
            }
            results.Add(new VenueSearchItemDto(card, freeCourts, slotPrice));
        }

        var ordered = VenueCards.Order(results, r => r.Venue).Take(MaxResults).ToList();
        return new VenueSearchResponse(location, radiusKm, slot, ordered);
    }

    private static void Validate(VenueSearchQuery query, DateTimeOffset localNow)
    {
        if (query.Sport is not null && !Sports.Contains(query.Sport))
            throw Invalid("INVALID_SPORT", "Môn thể thao không hợp lệ.");
        if (query.RadiusKm is double radius && radius is < 0.5 or > 50)
            throw Invalid("INVALID_RADIUS", "Bán kính từ 0,5 đến 50 km.");
        if (query.Lat.HasValue != query.Lng.HasValue || (query.Lat is double lat && query.Lng is double lng && !Geo.IsValid(lat, lng)))
            throw Invalid("INVALID_LOCATION", "Vị trí không hợp lệ.");
        if (query.Date.HasValue != query.StartHour.HasValue)
            throw Invalid("INVALID_SLOT", "Vui lòng chọn cả ngày và giờ bắt đầu.");
        if (query.Hours.HasValue && query.StartHour is null)
            throw Invalid("INVALID_SLOT", "Vui lòng chọn giờ bắt đầu trước khi chọn thời lượng.");
        if (query.StartHour is null) return;

        var hours = query.Hours ?? 1;
        if (query.StartHour is < 0 or > 23 || hours is < 1 or > MaxHours || query.StartHour + hours > 24)
            throw Invalid("INVALID_SLOT", $"Giờ chơi không hợp lệ (tối đa {MaxHours} giờ, kết thúc trước 24:00).");
        var today = DateOnly.FromDateTime(localNow.DateTime);
        if (query.Date > today.AddDays(MaxDaysAhead))
            throw Invalid("INVALID_SLOT", $"Chỉ tìm được sân trong {MaxDaysAhead} ngày tới.");
        var start = new DateTimeOffset(query.Date!.Value.ToDateTime(new TimeOnly(query.StartHour.Value, 0)), VietnamTime.Offset);
        if (start < localNow)
            throw Invalid("SLOT_IN_PAST", "Giờ chơi đã qua. Vui lòng chọn giờ khác.");
    }

    private static SearchSlotDto? BuildSlot(VenueSearchQuery query)
    {
        if (query.Date is null || query.StartHour is null) return null;
        var hours = query.Hours ?? 1;
        var start = new DateTimeOffset(query.Date.Value.ToDateTime(new TimeOnly(query.StartHour.Value, 0)), VietnamTime.Offset);
        return new SearchSlotDto(start, start.AddHours(hours), hours);
    }

    private static ApiException Invalid(string code, string message) => new(400, code, message);
}
