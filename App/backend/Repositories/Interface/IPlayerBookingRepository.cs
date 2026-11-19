using DatSan.Api.Models;

namespace DatSan.Api.Repositories.Interface;

/// <summary>Booking đang chiếm sân (đã xác nhận, hoặc đang giữ chỗ và còn hạn).</summary>
public sealed record BusyInterval(long CourtId, DateTimeOffset StartAt, DateTimeOffset EndAt);

public sealed record BookingView(
    long Id, long UserId, string Status, long VenueId, string VenueName, string VenueAddress, double Lat, double Lng,
    string CourtName, string Sport, string Environment, DateTimeOffset StartAt, DateTimeOffset EndAt, long Price,
    DateTimeOffset? HoldExpiresAt, string? CancelReason, DateTimeOffset CreatedAt);

/// <summary>Busy: SQL Server chọn transaction này làm nạn nhân deadlock; người dùng chỉ cần thử lại.</summary>
public enum HoldResult { Created, SlotTaken, Busy }

public interface IPlayerBookingRepository
{
    /// <summary>Cụm sân đã duyệt kèm sân con đang hoạt động và bảng giá; null nếu không có hoặc chưa duyệt.</summary>
    Task<Venue?> GetApprovedVenueAsync(long venueId, CancellationToken cancellationToken);

    /// <summary>Cụm sân đã duyệt chứa sân con đang hoạt động này (kèm sân con và bảng giá); null nếu không đặt được.</summary>
    Task<Venue?> GetVenueOfActiveCourtAsync(long courtId, CancellationToken cancellationToken);

    Task<bool> IsVipAsync(long venueId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyList<BusyInterval>> GetBusyAsync(
        IReadOnlyCollection<long> courtIds, DateTimeOffset from, DateTimeOffset to, DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Id booking đang giữ chỗ còn hạn của người chơi, nếu có.</summary>
    Task<long?> GetActiveHoldIdAsync(long userId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Trong một transaction: khóa sân con, chuyển các giữ chỗ đã hết hạn của sân con đó sang cancelled
    /// (trigger trg_bookings_no_overlap vẫn coi chúng là đang chiếm sân), kiểm tra chồng giờ rồi thêm booking.
    /// </summary>
    Task<HoldResult> CreateHoldAsync(Booking booking, DateTimeOffset now, CancellationToken cancellationToken);

    Task<BookingView?> GetViewAsync(long bookingId, CancellationToken cancellationToken);

    /// <summary>Booking của người chơi để cập nhật (được theo dõi); null nếu không có hoặc của người khác.</summary>
    Task<Booking?> GetOwnedForUpdateAsync(long bookingId, long userId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
