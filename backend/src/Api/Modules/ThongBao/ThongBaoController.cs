using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NhaXe.Api.Common;
using NhaXe.Modules.ThongBao.Application;

namespace NhaXe.Api.Modules.ThongBao;

/// <summary>Hộp thư của chính người đang đăng nhập (khách hoặc Admin). Id tài khoản lấy từ token, không lấy từ đường dẫn.</summary>
[ApiController]
[Authorize]
[Route("api/thong-bao")]
[Tags("6. ThongBao · Hộp thư của tôi")]
public class ThongBaoController : ControllerBase
{
    [HttpGet]
    public Task<HopThuDto> Lay([FromServices] LayHopThuUseCase uc, CancellationToken ct) => uc.ThucHienAsync(User.TaiKhoanId(), ct);

    /// <summary>Số thông báo chưa đọc (menu gọi định kỳ).</summary>
    [HttpGet("so-chua-doc")]
    public async Task<object> SoChuaDoc([FromServices] DemChuaDocUseCase uc, CancellationToken ct) =>
        new { soChuaDoc = await uc.ThucHienAsync(User.TaiKhoanId(), ct) };

    [HttpPut("{id:int}/da-doc")]
    public async Task<IActionResult> DaDoc(int id, [FromServices] DanhDauDaDocUseCase uc, CancellationToken ct)
    {
        await uc.ThucHienAsync(User.TaiKhoanId(), id, ct);
        return NoContent();
    }

    [HttpPut("da-doc-tat-ca")]
    public async Task<object> DocHet([FromServices] DocHetUseCase uc, CancellationToken ct) =>
        new { soDaDanhDau = await uc.ThucHienAsync(User.TaiKhoanId(), ct) };
}
