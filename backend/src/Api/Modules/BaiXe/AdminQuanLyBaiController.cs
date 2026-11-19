using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NhaXe.Modules.BaiXe.Application;

namespace NhaXe.Api.Modules.BaiXe;

/// <summary>Admin cấu hình bãi: khu, ô, camera và đặt tay trạng thái ô. Controller mỏng: chỉ chuyển request sang use case.</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/admin/bai")]
[Tags("7. BaiXe · Admin: quản lý khu, ô, camera")]
public class AdminQuanLyBaiController : ControllerBase
{
    [HttpGet("cau-hinh")]
    public Task<CauHinhBaiDto> CauHinh([FromServices] LayCauHinhBaiUseCase uc, CancellationToken ct) => uc.ThucHienAsync(ct);

    // ----- Khu -----
    [HttpPost("khu")]
    public async Task<object> ThemKhu([FromBody] KhuLenh lenh, [FromServices] ThemKhuUseCase uc, CancellationToken ct) =>
        new { id = await uc.ThucHienAsync(lenh, ct) };

    [HttpPut("khu/{id:int}")]
    public async Task<IActionResult> SuaKhu(int id, [FromBody] KhuLenh lenh, [FromServices] SuaKhuUseCase uc, CancellationToken ct)
    { await uc.ThucHienAsync(id, lenh, ct); return NoContent(); }

    [HttpDelete("khu/{id:int}")]
    public async Task<IActionResult> XoaKhu(int id, [FromServices] XoaKhuUseCase uc, CancellationToken ct)
    { await uc.ThucHienAsync(id, ct); return NoContent(); }

    // ----- Ô -----
    [HttpPost("o")]
    public async Task<object> ThemO([FromBody] OLenh lenh, [FromServices] ThemOUseCase uc, CancellationToken ct) =>
        new { id = await uc.ThucHienAsync(lenh, ct) };

    [HttpPut("o/{id:int}")]
    public async Task<IActionResult> SuaO(int id, [FromBody] OLenh lenh, [FromServices] SuaOUseCase uc, CancellationToken ct)
    { await uc.ThucHienAsync(id, lenh, ct); return NoContent(); }

    [HttpDelete("o/{id:int}")]
    public async Task<IActionResult> XoaO(int id, [FromServices] XoaOUseCase uc, CancellationToken ct)
    { await uc.ThucHienAsync(id, ct); return NoContent(); }

    /// <summary>Đặt tay trạng thái ô (TRONG, CO_XE, KHONG_RO). Khi có AI ô trống, AI gọi IBaiXeModule thay cho bước này.</summary>
    [HttpPut("o/{id:int}/trang-thai")]
    public async Task<IActionResult> DatTrangThai(int id, [FromBody] TrangThaiOLenh lenh, [FromServices] DatTrangThaiOUseCase uc, CancellationToken ct)
    { await uc.ThucHienAsync(id, lenh.TrangThai, ct); return NoContent(); }

    // ----- Camera -----
    [HttpPost("camera")]
    public async Task<object> ThemCamera([FromBody] CameraLenh lenh, [FromServices] ThemCameraUseCase uc, CancellationToken ct) =>
        new { id = await uc.ThucHienAsync(lenh, ct) };

    [HttpPut("camera/{id:int}")]
    public async Task<IActionResult> SuaCamera(int id, [FromBody] CameraLenh lenh, [FromServices] SuaCameraUseCase uc, CancellationToken ct)
    { await uc.ThucHienAsync(id, lenh, ct); return NoContent(); }

    [HttpDelete("camera/{id:int}")]
    public async Task<IActionResult> XoaCamera(int id, [FromServices] XoaCameraUseCase uc, CancellationToken ct)
    { await uc.ThucHienAsync(id, ct); return NoContent(); }
}
