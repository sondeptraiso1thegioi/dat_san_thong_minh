using DatSan.Api.DTOs;
using DatSan.Api.Services.Interface;
using DatSan.Api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DatSan.Api.Controllers;

/// <summary>Chủ sân đăng ký và quản lý cụm sân của mình (F08, F09 phần bảng giá, UC04).</summary>
[ApiController]
[Route("api/owner/venues")]
[Authorize(Roles = "owner")]
public sealed class OwnerVenuesController(IOwnerVenueService venues) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OwnerVenueListItemDto>>>> List(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyList<OwnerVenueListItemDto>>(await venues.ListAsync(User.GetUserId(), cancellationToken)));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<OwnerVenueDetailDto>>> Get(long id, CancellationToken cancellationToken) =>
        Ok(new ApiResponse<OwnerVenueDetailDto>(await venues.GetAsync(User.GetUserId(), id, cancellationToken)));

    /// <summary>Gửi đăng ký cụm sân mới; cụm sân ở trạng thái pending chờ admin duyệt.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<OwnerVenueSaveResult>>> Create(
        OwnerVenueInput input, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created,
            new ApiResponse<OwnerVenueSaveResult>(await venues.CreateAsync(User.GetUserId(), input, cancellationToken)));

    /// <summary>Sửa cụm sân. Đổi tên/địa chỉ/vị trí của sân đã duyệt, hoặc sửa sân bị từ chối, sẽ đưa sân về chờ duyệt.</summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<OwnerVenueSaveResult>>> Update(
        long id, OwnerVenueInput input, CancellationToken cancellationToken) =>
        Ok(new ApiResponse<OwnerVenueSaveResult>(await venues.UpdateAsync(User.GetUserId(), id, input, cancellationToken)));
}
