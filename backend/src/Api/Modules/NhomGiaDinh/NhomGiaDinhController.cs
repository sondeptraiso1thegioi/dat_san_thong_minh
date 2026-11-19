using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NhaXe.Api.Common;
using NhaXe.Modules.NhomGiaDinh.Application;
namespace NhaXe.Api.Modules.NhomGiaDinh;

[ApiController, Authorize]
[Route("api/nhom-gia-dinh"), Tags("3. NhomGiaDinh")]
public class NhomGiaDinhController : ControllerBase
{
    [HttpGet]
    public Task<TrangThaiNhomDto> Xem([FromServices] XemNhomUseCase uc, CancellationToken ct) => uc.ThucHienAsync(User.TaiKhoanId(), ct);
    [HttpPost]
    public async Task<IActionResult> Tao(TaoNhomLenh lenh, [FromServices] TaoNhomUseCase uc, CancellationToken ct) =>
        StatusCode(201, await uc.ThucHienAsync(User.TaiKhoanId(), lenh, ct));
    [HttpPost("yeu-cau")]
    public async Task<IActionResult> XinThamGia(XinThamGiaLenh lenh, [FromServices] XinThamGiaUseCase uc, CancellationToken ct) =>
        StatusCode(201, new { id = await uc.ThucHienAsync(User.TaiKhoanId(), lenh, ct) });
    [HttpPut("yeu-cau/{id:int}")]
    public Task<NhomDto> XuLy(int id, XuLyYeuCauLenh lenh, [FromServices] XuLyYeuCauUseCase uc, CancellationToken ct) =>
        uc.ThucHienAsync(User.TaiKhoanId(), id, lenh.DongY, ct);
    [HttpPost("ma-moi")]
    public Task<NhomDto> DoiMa([FromServices] DoiMaMoiUseCase uc, CancellationToken ct) => uc.ThucHienAsync(User.TaiKhoanId(), ct);
    [HttpPost("roi-nhom")]
    public async Task<IActionResult> Roi([FromServices] RoiNhomUseCase uc, CancellationToken ct)
    { await uc.ThucHienAsync(User.TaiKhoanId(), User.TaiKhoanId(), ct); return NoContent(); }
    [HttpPost("thanh-vien/{id:int}/xoa")]
    public async Task<IActionResult> XoaThanhVien(int id, [FromServices] RoiNhomUseCase uc, CancellationToken ct)
    { await uc.ThucHienAsync(User.TaiKhoanId(), id, ct); return NoContent(); }
    [HttpPost("giai-tan")]
    public async Task<IActionResult> GiaiTan([FromServices] GiaiTanNhomUseCase uc, CancellationToken ct)
    { await uc.ThucHienAsync(User.TaiKhoanId(), ct); return NoContent(); }
}
