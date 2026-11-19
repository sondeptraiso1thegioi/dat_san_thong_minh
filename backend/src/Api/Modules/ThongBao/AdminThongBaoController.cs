using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NhaXe.Api.Common;
using NhaXe.Modules.ThongBao.Application;

namespace NhaXe.Api.Modules.ThongBao;

/// <summary>Hộp thư Admin: khách nào đã huỷ gói, lý do, góp ý.</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/admin/thong-bao")]
[Tags("6. ThongBao · Admin")]
public class AdminThongBaoController : ControllerBase
{
    [HttpGet]
    public Task<HopThuDto> Lay([FromServices] LayThongBaoAdminUseCase uc, CancellationToken ct) => uc.ThucHienAsync(User.TaiKhoanId(), ct);

    [HttpPut("{id:int}/da-doc")]
    public async Task<IActionResult> DaDoc(int id, [FromServices] DanhDauDaDocUseCase uc, CancellationToken ct)
    {
        await uc.ThucHienAsync(User.TaiKhoanId(), id, ct);
        return NoContent();
    }
}
