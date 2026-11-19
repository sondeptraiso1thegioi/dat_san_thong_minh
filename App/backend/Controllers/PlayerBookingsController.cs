using DatSan.Api.DTOs;
using DatSan.Api.Services.Interface;
using DatSan.Api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DatSan.Api.Controllers;

[ApiController]
[Route("api/player")]
[Authorize(Roles = "player")]
public sealed class PlayerBookingsController(IPlayerBookingService service) : ControllerBase
{
    /// <summary>Chi tiết cụm sân (F04) và lưới giờ trống của các sân con. date = yyyy-MM-dd theo giờ Việt Nam, mặc định hôm nay.</summary>
    [HttpGet("venues/{venueId:long}")]
    public async Task<ActionResult<ApiResponse<VenueDetailResponse>>> Venue(
        long venueId, [FromQuery] DateOnly? date, [FromQuery] double? lat, [FromQuery] double? lng,
        CancellationToken cancellationToken) =>
        Ok(new ApiResponse<VenueDetailResponse>(
            await service.GetVenueDetailAsync(User.GetUserId(), venueId, date, lat, lng, cancellationToken)));

    /// <summary>Giữ chỗ: tạo booking pending, có hạn thanh toán (ai_settings.booking_hold_minutes).</summary>
    [HttpPost("bookings")]
    public async Task<ActionResult<ApiResponse<BookingDetailDto>>> Create(
        [FromBody] CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var booking = await service.CreateHoldAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { bookingId = booking.Id }, new ApiResponse<BookingDetailDto>(booking));
    }

    /// <summary>Chỗ đang giữ còn hạn của người chơi; data = null nếu không có.</summary>
    [HttpGet("bookings/active-hold")]
    public async Task<ActionResult<ApiResponse<BookingDetailDto?>>> ActiveHold(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<BookingDetailDto?>(await service.GetActiveHoldAsync(User.GetUserId(), cancellationToken)));

    /// <summary>Danh sách lịch đặt. view = all | upcoming | history; pageSize tối đa 50.</summary>
    [HttpGet("bookings")]
    public async Task<ActionResult<ApiResponse<PlayerBookingsResponse>>> List(
        [FromQuery] string view = "all", [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(new ApiResponse<PlayerBookingsResponse>(
            await service.GetBookingsAsync(User.GetUserId(), view, page, pageSize, cancellationToken)));

    [HttpGet("bookings/{bookingId:long}")]
    public async Task<ActionResult<ApiResponse<BookingDetailDto>>> Get(long bookingId, CancellationToken cancellationToken) =>
        Ok(new ApiResponse<BookingDetailDto>(await service.GetBookingAsync(User.GetUserId(), bookingId, cancellationToken)));

    /// <summary>
    /// Hủy giữ chỗ chưa thanh toán; booking đã thanh toán chỉ hủy nếu còn ít nhất 24 giờ,
    /// payment chuyển sang refund_pending để Admin xử lý hoàn tiền.
    /// </summary>
    [HttpPost("bookings/{bookingId:long}/cancel")]
    public async Task<ActionResult<ApiResponse<BookingDetailDto>>> Cancel(long bookingId, CancellationToken cancellationToken) =>
        Ok(new ApiResponse<BookingDetailDto>(await service.CancelBookingAsync(User.GetUserId(), bookingId, cancellationToken)));
}
