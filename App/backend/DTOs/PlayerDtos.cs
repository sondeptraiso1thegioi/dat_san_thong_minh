namespace DatSan.Api.DTOs;

/// <summary>source: device (GPS gửi lên) | profile (địa chỉ đã ghim trong hồ sơ) | none.</summary>
public sealed record HomeLocationDto(string Source, string? Address, double? Lat, double? Lng);

public sealed record VenueCardDto(
    string Id, string Name, string Address, double Lat, double Lng, double? DistanceKm,
    IReadOnlyList<string> Sports, bool HasShelter, long? MinPricePerHour, string? PhotoUrl, bool IsVip,
    string OpenTime, string CloseTime, bool IsOpenNow, IReadOnlyList<string> Amenities);

public sealed record UpcomingBookingDto(
    string Id, string VenueId, string VenueName, string VenueAddress, string CourtName, string Sport,
    string Environment, DateTimeOffset StartAt, DateTimeOffset EndAt, string Status, long Price,
    DateTimeOffset? HoldExpiresAt);

public sealed record PlayerHomeResponse(
    HomeLocationDto Location, double RadiusKm,
    IReadOnlyList<VenueCardDto> Featured, IReadOnlyList<VenueCardDto> Nearby, UpcomingBookingDto? Upcoming);

/// <summary>Tham số tìm sân (query string). Ngày + giờ bắt đầu đi cùng nhau; thời lượng tính theo giờ.</summary>
public sealed class VenueSearchQuery
{
    public string? Q { get; init; }
    public string? Sport { get; init; }
    public double? RadiusKm { get; init; }
    public double? Lat { get; init; }
    public double? Lng { get; init; }
    public bool Sheltered { get; init; }
    /// <summary>Mã tiện ích, phân tách bằng dấu phẩy, ví dụ gui_xe,nha_tam.</summary>
    public string? Amenities { get; init; }
    /// <summary>Ngày chơi theo giờ Việt Nam, dạng yyyy-MM-dd.</summary>
    public DateOnly? Date { get; init; }
    public int? StartHour { get; init; }
    public int? Hours { get; init; }
}

public sealed record SearchSlotDto(DateTimeOffset StartAt, DateTimeOffset EndAt, int Hours);

/// <summary>FreeCourts/SlotPrice chỉ có khi tìm theo giờ chơi: số sân con còn trống và giá thấp nhất cho cả khung giờ.</summary>
public sealed record VenueSearchItemDto(VenueCardDto Venue, int? FreeCourts, long? SlotPrice);

public sealed record VenueSearchResponse(
    HomeLocationDto Location, double? RadiusKm, SearchSlotDto? Slot, IReadOnlyList<VenueSearchItemDto> Venues);
