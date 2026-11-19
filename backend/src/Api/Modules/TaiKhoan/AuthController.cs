using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NhaXe.Api.Common;
using NhaXe.Modules.TaiKhoan.Infrastructure;
using NhaXe.Modules.TaiKhoan.Application;

namespace NhaXe.Api.Modules.TaiKhoan;

/// <summary>Controller chỉ làm việc của HTTP: nhận request, gọi use case, trả kết quả (SRP).</summary>
[ApiController]
[Route("api/auth")]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")] // giới hạn số lần/phút mỗi IP (Program.cs)
[Tags("1. TaiKhoan")]
public class AuthController : ControllerBase
{
    [HttpPost("dang-nhap")]
    [AllowAnonymous]
    public Task<DangNhapKetQua> DangNhap([FromBody] DangNhapLenh lenh, [FromServices] DangNhapUseCase uc, CancellationToken ct)
        => uc.ThucHienAsync(lenh, ct);

    [HttpPost("dang-ky"), AllowAnonymous]
    public async Task<IActionResult> DangKy(DangKyLenh lenh, [FromServices] DangKyUseCase uc, CancellationToken ct) =>
        StatusCode(201, await uc.ThucHienAsync(lenh, ct));
    [HttpPost("quen-mat-khau"), AllowAnonymous]
    public async Task<IActionResult> QuenMatKhau(QuenMatKhauLenh lenh, [FromServices] QuenMatKhauUseCase uc, CancellationToken ct)
    { await uc.ThucHienAsync(lenh, ct); return Ok(new { thongDiep = QuenMatKhauUseCase.ThongDiep }); }
    [HttpPost("dat-lai-mat-khau"), AllowAnonymous]
    public async Task<IActionResult> DatLai(DatLaiMatKhauLenh lenh, [FromServices] DatLaiMatKhauUseCase uc, CancellationToken ct)
    { await uc.ThucHienAsync(lenh, ct); return NoContent(); }
    [HttpPut("mat-khau"), Authorize]
    public async Task<IActionResult> DoiMatKhau(DoiMatKhauLenh lenh, [FromServices] DoiMatKhauUseCase uc, CancellationToken ct)
    { await uc.ThucHienAsync(User.TaiKhoanId(), lenh, ct); return NoContent(); }
    [HttpPost("dang-xuat"), Authorize]
    public async Task<IActionResult> DangXuat([FromServices] DangXuatUseCase uc, CancellationToken ct)
    { await uc.ThucHienAsync(User.TaiKhoanId(), User.FindFirst(JwtTokenService.ClaimPhien)!.Value, ct); return NoContent(); }
}
