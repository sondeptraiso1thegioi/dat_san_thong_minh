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

    Task<BookingDetailDto> GetBookingAsync(long userId, long bookingId, CancellationToken cancellationToken);

    /// <summary>Hủy chỗ đang giữ (chưa thanh toán). Booking đã thanh toán hủy theo quy tắc 24 giờ ở bước Lịch đặt.</summary>
    Task<BookingDetailDto> CancelHoldAsync(long userId, long bookingId, CancellationToken cancellationToken);
}
