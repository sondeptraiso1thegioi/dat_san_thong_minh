using DatSan.Api.DTOs;
using DatSan.Api.Repositories.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DatSan.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize(Roles = "player,owner,admin")]
public sealed class MeController(IUserRepository users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<UserDto>>> Get(CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirst("sub")?.Value, out var id))
            return Unauthorized(new ApiError("Phiên đăng nhập không hợp lệ.", "UNAUTHORIZED"));
        var user = await users.FindByIdAsync(id, cancellationToken);
        if (user is null || user.Status != "active")
            return Unauthorized(new ApiError("Vui lòng đăng nhập lại.", "UNAUTHORIZED"));
        return Ok(new ApiResponse<UserDto>(UserDto.From(user)));
    }
}
