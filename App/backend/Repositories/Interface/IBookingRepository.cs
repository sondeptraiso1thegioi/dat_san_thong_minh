namespace DatSan.Api.Repositories.Interface;

public sealed record UpcomingBooking(
    long Id, DateTimeOffset StartAt, DateTimeOffset EndAt, string Status, long Price, DateTimeOffset? HoldExpiresAt,
    string CourtName, string Sport, string Environment, long VenueId, string VenueName, string VenueAddress);

public interface IBookingRepository
{
    /// <summary>Trận gần nhất chưa kết thúc: đã xác nhận, hoặc đang chờ thanh toán và còn hạn giữ chỗ.</summary>
    Task<UpcomingBooking?> GetNextUpcomingAsync(long userId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Sân con đã có booking giữ chỗ (pending còn hạn) hoặc đã xác nhận chồng lên [start, end).</summary>
    Task<IReadOnlySet<long>> GetBusyCourtIdsAsync(
        DateTimeOffset start, DateTimeOffset end, DateTimeOffset now, CancellationToken cancellationToken);
}
