using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NhaXe.Modules.BaiXe.Application;

namespace NhaXe.Api.Modules.BaiXe;

[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/admin/bai")]
[Tags("7. BaiXe · Admin: sơ đồ bãi")]
public class AdminSoDoBaiController : ControllerBase
{
    /// <summary>Sơ đồ bãi: sức chứa từng loại xe, các khu và ô, xe nào đang ở ô nào, còn hạn bao lâu.</summary>
    [HttpGet]
    public Task<SoDoBaiDto> SoDo([FromServices] LaySoDoBaiUseCase uc, CancellationToken ct) => uc.ThucHienAsync(ct);
}
