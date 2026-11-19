namespace DatSan.Api.Repositories.Interface;

/// <summary>Cụm sân đã duyệt, kèm môn, môi trường sân con, giá thấp nhất và trạng thái VIP.</summary>
public sealed record VenueSummary(
    long Id, string Name, string Address, double Lat, double Lng,
    TimeOnly OpenTime, TimeOnly CloseTime, string AmenitiesJson, string PhotosJson,
    IReadOnlyList<string> Sports, IReadOnlyList<string> Environments, long? MinPricePerHour, bool IsVip);

public sealed record CourtInfo(long Id, long VenueId, string Sport, string Name, string Environment);

public sealed record PriceRuleInfo(long VenueId, string Sport, short Weekday, short StartHour, short EndHour, long PricePerHour);

public interface IVenueRepository
{
    Task<IReadOnlyList<VenueSummary>> GetApprovedSummariesAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Sân con đang hoạt động của các cụm sân đã duyệt.</summary>
    Task<IReadOnlyList<CourtInfo>> GetActiveCourtsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PriceRuleInfo>> GetPriceRulesAsync(CancellationToken cancellationToken);
}
