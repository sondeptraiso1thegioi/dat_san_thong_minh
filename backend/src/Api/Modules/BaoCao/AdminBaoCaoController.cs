using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NhaXe.Modules.BaoCao.Application;

namespace NhaXe.Api.Modules.BaoCao;

[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/admin/bao-cao")]
[Tags("9. BaoCao · Admin")]
public class AdminBaoCaoController : ControllerBase
{
    /// <summary>Lượt vào / ra, doanh thu, tỉ lệ định vị khu xe máy, tỉ lệ AI tự đọc biển. Mặc định: từ 00:00 hôm nay.</summary>
    [HttpGet]
    public Task<BaoCaoDto> Lay([FromQuery] DateTime? tu, [FromQuery] DateTime? den, [FromServices] LayBaoCaoUseCase uc, CancellationToken ct) =>
        uc.ThucHienAsync(tu, den, ct);
}
