using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NhaXe.Modules.BaiXe.Application;

namespace NhaXe.Api.Modules.BaiXe;

[ApiController]
[Authorize]
[Route("api/bai")]
[Tags("7. BaiXe · Khách: chỗ trống")]
public class ChoTrongController : ControllerBase
{
    /// <summary>Số ô trống / có xe / không rõ theo từng khu. Không trả biển số hay chủ xe.</summary>
    [HttpGet("cho-trong")]
    public Task<ChoTrongDto> ChoTrong([FromServices] LayChoTrongUseCase uc, CancellationToken ct) => uc.ThucHienAsync(ct);
}
