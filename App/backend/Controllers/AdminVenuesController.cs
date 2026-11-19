using DatSan.Api.DTOs;
using DatSan.Api.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DatSan.Api.Controllers;

/// <summary>Admin duyệt, từ chối, ẩn cụm sân (UC05, F12).</summary>
[ApiController]
[Route("api/admin/venues")]
[Authorize(Roles = "admin")]
public sealed class AdminVenuesController(IAdminVenueService venues) : ControllerBase
{
    /// <summary>status: pending (mặc định) | approved | rejected | deleted. Kèm số lượng theo từng trạng thái.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<AdminVenueListResponse>>> List([FromQuery] string? status, CancellationToken cancellationToken) =>
        Ok(new ApiResponse<AdminVenueListResponse>(await venues.ListAsync(status, cancellationToken)));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<AdminVenueDetailDto>>> Get(long id, CancellationToken cancellationToken) =>
        Ok(new ApiResponse<AdminVenueDetailDto>(await venues.GetAsync(id, cancellationToken)));

    [HttpPost("{id:long}/approve")]
    public async Task<ActionResult<ApiResponse<AdminVenueActionResult>>> Approve(long id, CancellationToken cancellationToken) =>
        Ok(new ApiResponse<AdminVenueActionResult>(await venues.ApproveAsync(id, cancellationToken)));

    [HttpPost("{id:long}/reject")]
    public async Task<ActionResult<ApiResponse<AdminVenueActionResult>>> Reject(
        long id, AdminVenueActionRequest request, CancellationToken cancellationToken) =>
        Ok(new ApiResponse<AdminVenueActionResult>(await venues.RejectAsync(id, request.Reason, cancellationToken)));

    /// <summary>Ẩn cụm sân: hủy booking sắp tới và đánh dấu cần hoàn tiền cho booking đã thanh toán.</summary>
    [HttpPost("{id:long}/hide")]
    public async Task<ActionResult<ApiResponse<AdminVenueActionResult>>> Hide(
        long id, AdminVenueActionRequest request, CancellationToken cancellationToken) =>
        Ok(new ApiResponse<AdminVenueActionResult>(await venues.HideAsync(id, request.Reason, cancellationToken)));
}
