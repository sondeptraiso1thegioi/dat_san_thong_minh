using DatSan.Api.DTOs;

namespace DatSan.Api.Services.Interface;

public interface IPlayerBookingService
{
    /// <summary>Chi tiết cụm sân và lưới giờ của các sân con trong một ngày (mặc định hôm nay).</summary>
    Task<VenueDetailResponse> GetVenueDetailAsync(
        long userId, long venueId, DateOnly? date, double? lat, double? lng, CancellationToken cancellationToken);

    /// <summary>Tạo booking pending và giữ chỗ trong ai_settings.booking_hold_minutes phút.</summary>
    Task<BookingDetailDto> CreateHoldAsync(long userId, CreateBookingRequest request, CancellationToken cancellationToken);

    Task<BookingDetailDto?> GetActiveHoldAsync(long userId, CancellationToken cancellationToken);

    Task<PlayerBookingsResponse> GetBookingsAsync(
        long userId, string view, int page, int pageSize, CancellationToken cancellationToken);

    Task<BookingDetailDto> GetBookingAsync(long userId, long bookingId, CancellationToken cancellationToken);

    /// <summary>Hủy giữ chỗ hoặc booking đã xác nhận theo điều kiện 24 giờ.</summary>
    Task<BookingDetailDto> CancelBookingAsync(long userId, long bookingId, CancellationToken cancellationToken);
}
