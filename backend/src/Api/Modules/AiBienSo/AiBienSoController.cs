using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NhaXe.Modules.AiBienSo.Application;
using NhaXe.Modules.AiBienSo.Contracts;
namespace NhaXe.Api.Modules.AiBienSo;

[ApiController, Authorize(Roles = "ADMIN")]
[Route("api/admin/ai-bien-so"), Tags("4. AI biển số")]
public class AiBienSoController : ControllerBase
{
    [HttpPut("nhan-dien"), Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<KetQuaDocBienSo> Doc(IFormFile anh, [FromServices] DocAnhTaiLenUseCase uc, CancellationToken ct)
    {
        await using var stream = anh.OpenReadStream();
        return await uc.ThucHienAsync(stream, anh.Length, anh.ContentType, ct);
    }
}
