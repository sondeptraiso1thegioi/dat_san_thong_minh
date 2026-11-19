using DatSan.Api.Data;
using DatSan.Api.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace DatSan.Api.Repositories.Implements;

public sealed class VenueRepository(AppDbContext db) : IVenueRepository
{
    private static readonly string[] SportOrder = ["tennis", "pickleball", "badminton", "table_tennis"];

    private static int SportRank(string sport)
    {
        var index = Array.IndexOf(SportOrder, sport);
        return index < 0 ? int.MaxValue : index;
    }

    // Bốn truy vấn đơn giản rồi ghép trong bộ nhớ: dữ liệu cụm sân của đồ án nhỏ,
    // và cách này tránh các phép LINQ lồng nhau khó dịch sang SQL.
    public async Task<IReadOnlyList<VenueSummary>> GetApprovedSummariesAsync(
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var venues = await db.Venues.AsNoTracking()
            .Where(v => v.Status == "approved")
            .Select(v => new { v.Id, v.Name, v.Address, v.Lat, v.Lng, v.OpenTime, v.CloseTime, v.Amenities, v.Photos })
            .ToListAsync(cancellationToken);
        var courts = await db.Courts.AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new { c.VenueId, c.Sport, c.Environment })
            .ToListAsync(cancellationToken);
        var minPrices = await db.PriceRules.AsNoTracking()
            .GroupBy(p => p.VenueId)
            .Select(g => new { VenueId = g.Key, Min = g.Min(p => p.PricePerHour) })
            .ToDictionaryAsync(x => x.VenueId, x => x.Min, cancellationToken);
        var vipIds = (await db.VipSubscriptions.AsNoTracking()
            .Where(s => s.Status == "active" && s.StartAt <= now && s.EndAt > now)
            .Select(s => s.VenueId)
            .Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();

        var courtsByVenue = courts.ToLookup(c => c.VenueId);
        return venues
            .Where(v => courtsByVenue[v.Id].Any()) // Cụm chưa có sân con hoạt động thì chưa đặt được.
            .Select(v => new VenueSummary(
                v.Id, v.Name, v.Address, v.Lat, v.Lng, v.OpenTime, v.CloseTime, v.Amenities, v.Photos,
                courtsByVenue[v.Id].Select(c => c.Sport).Distinct()
                    .OrderBy(SportRank).ToList(),
                courtsByVenue[v.Id].Select(c => c.Environment).Distinct().ToList(),
                minPrices.TryGetValue(v.Id, out var min) ? min : null,
                vipIds.Contains(v.Id)))
            .ToList();
    }

    public async Task<IReadOnlyList<CourtInfo>> GetActiveCourtsAsync(CancellationToken cancellationToken) =>
        await db.Courts.AsNoTracking()
            .Where(c => c.IsActive && c.Venue!.Status == "approved")
            .OrderBy(c => c.Id)
            .Select(c => new CourtInfo(c.Id, c.VenueId, c.Sport, c.Name, c.Environment))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PriceRuleInfo>> GetPriceRulesAsync(CancellationToken cancellationToken) =>
        await db.PriceRules.AsNoTracking()
            .Select(p => new PriceRuleInfo(p.VenueId, p.Sport, p.Weekday, p.StartHour, p.EndHour, p.PricePerHour))
            .ToListAsync(cancellationToken);
}
