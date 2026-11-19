using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NhaXe.Api.Common;
using NhaXe.Modules.CongRaVao.Application;

namespace NhaXe.Api.Modules.CongRaVao;

/// <summary>Trang "Ra vào" của khách: xác nhận xe ra như 2FA và xem lịch sử. Id tài khoản lấy từ token.</summary>
[ApiController]
[Authorize(Roles = "USER")]
[Route("api/ra-vao")]
[Tags("5. CongRaVao · Khách: xác nhận ra, lịch sử")]
public class RaVaoController : ControllerBase
{
    [HttpGet]
    public Task<RaVaoCuaToiDto> Lay([FromServices] LayRaVaoCuaToiUseCase uc, CancellationToken ct) => uc.ThucHienAsync(User.TaiKhoanId(), ct);

    /// <summary>dongY = true: "Đúng là tôi". dongY = false: "Không phải tôi" (nghi mất xe, báo Admin). Người trả lời trước có hiệu lực.</summary>
    [HttpPut("yeu-cau/{id:int}")]
    public async Task<IActionResult> TraLoi(int id, [FromBody] TraLoiLenh lenh, [FromServices] NguoiDungXeXacNhanUseCase uc, CancellationToken ct)
    {
        await uc.ThucHienAsync(User.TaiKhoanId(), id, lenh.DongY, ct);
        return NoContent();
    }
}
