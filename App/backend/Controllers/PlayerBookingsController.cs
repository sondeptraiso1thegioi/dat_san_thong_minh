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

    [HttpGet("bookings/{bookingId:long}")]
    public async Task<ActionResult<ApiResponse<BookingDetailDto>>> Get(long bookingId, CancellationToken cancellationToken) =>
        Ok(new ApiResponse<BookingDetailDto>(await service.GetBookingAsync(User.GetUserId(), bookingId, cancellationToken)));

    /// <summary>Hủy chỗ đang giữ (chưa thanh toán).</summary>
    [HttpPost("bookings/{bookingId:long}/cancel")]
    public async Task<ActionResult<ApiResponse<BookingDetailDto>>> Cancel(long bookingId, CancellationToken cancellationToken) =>
        Ok(new ApiResponse<BookingDetailDto>(await service.CancelHoldAsync(User.GetUserId(), bookingId, cancellationToken)));
}
