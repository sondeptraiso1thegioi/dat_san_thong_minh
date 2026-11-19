using DatSan.Api.DTOs;
using DatSan.Api.Services.Interface;
using DatSan.Api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DatSan.Api.Controllers;

[ApiController]
[Route("api/player")]
[Authorize(Roles = "player")]
public sealed class PlayerController(IPlayerHomeService home, IVenueSearchService search) : ControllerBase
{
    /// <summary>Trang chủ người chơi. lat/lng (tùy chọn) là vị trí GPS; bỏ trống thì dùng vị trí trong hồ sơ.</summary>
    [HttpGet("home")]
    public async Task<ActionResult<ApiResponse<PlayerHomeResponse>>> Home(
        [FromQuery] double? lat, [FromQuery] double? lng, CancellationToken cancellationToken)
    {
        if (lat.HasValue != lng.HasValue || (lat is double a && lng is double b && !Geo.IsValid(a, b)))
            return BadRequest(new ApiError("Vị trí không hợp lệ.", "INVALID_LOCATION"));
        return Ok(new ApiResponse<PlayerHomeResponse>(
            await home.GetHomeAsync(User.GetUserId(), lat, lng, cancellationToken)));
    }

    /// <summary>
    /// Tìm sân (F02/UC03). Lọc theo tên/địa chỉ (không dấu), môn, bán kính, sân có mái, tiện ích;
    /// thêm date + startHour (+ hours) để chỉ lấy cụm còn sân trống và tính giá cho khung giờ đó.
    /// </summary>
    [HttpGet("venues")]
    public async Task<ActionResult<ApiResponse<VenueSearchResponse>>> Venues(
        [FromQuery] VenueSearchQuery query, CancellationToken cancellationToken) =>
        Ok(new ApiResponse<VenueSearchResponse>(await search.SearchAsync(User.GetUserId(), query, cancellationToken)));
}
