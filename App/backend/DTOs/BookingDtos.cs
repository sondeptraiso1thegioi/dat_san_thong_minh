namespace DatSan.Api.DTOs;

/// <summary>
/// Một ô trong lưới giờ của sân con. State: free | booked (đã có người đặt/giữ chỗ) |
/// past (đã qua) | unpriced (chủ sân chưa đặt giá cho giờ này).
/// </summary>
public sealed record SlotCellDto(int Hour, long? Price, string State);

public sealed record CourtScheduleDto(string Id, string Name, string Sport, string Environment, IReadOnlyList<SlotCellDto> Slots);

/// <summary>Chi tiết cụm sân (F04) cho một ngày. Hours: các giờ bắt đầu nằm trong giờ mở cửa, giống nhau cho mọi sân con.</summary>
public sealed record VenueDetailResponse(
    VenueCardDto Venue, string? Description, IReadOnlyList<string> Photos,
    DateOnly Date, DateOnly MaxDate, IReadOnlyList<int> Hours, int MaxHours, int HoldMinutes,
    DateTimeOffset ServerNow, IReadOnlyList<CourtScheduleDto> Courts);

/// <summary>Giữ chỗ: một sân con, ngày theo giờ Việt Nam, giờ bắt đầu chẵn, 1–3 giờ.</summary>
public sealed class CreateBookingRequest
{
    public string? CourtId { get; init; }
    public DateOnly? Date { get; init; }
    public int? StartHour { get; init; }
    public int? Hours { get; init; }
}

/// <summary>Status: pending | expired (giữ chỗ đã hết hạn) | confirmed | cancelled | completed.</summary>
public sealed record BookingDetailDto(
    string Id, string Status, string VenueId, string VenueName, string VenueAddress, double Lat, double Lng,
    string CourtName, string Sport, string Environment,
    DateTimeOffset StartAt, DateTimeOffset EndAt, int Hours, long Price,
    DateTimeOffset? HoldExpiresAt, string? CancelReason, DateTimeOffset CreatedAt, DateTimeOffset ServerNow);
