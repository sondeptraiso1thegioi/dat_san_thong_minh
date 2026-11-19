using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NhaXe.Api.Common;
using NhaXe.Modules.TaiKhoan.Application;

namespace NhaXe.Api.Modules.TaiKhoan;

/// <summary>Admin quản lý tài khoản người dùng: xem, tìm, khoá, mở khoá (v10, module 1).</summary>
[ApiController]
[Route("api/admin/tai-khoan")]
[Authorize(Roles = "ADMIN")]
[Tags("1. TaiKhoan (Admin)")]
public class AdminTaiKhoanController : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<TaiKhoanAdminDto>> DanhSach([FromQuery] string? tuKhoa, [FromQuery] string? trangThai,
        [FromServices] DanhSachTaiKhoanUseCase uc, CancellationToken ct) => uc.ThucHienAsync(tuKhoa, trangThai, ct);

    [HttpPut("{id:int}/khoa")]
    public async Task<IActionResult> Khoa(int id, [FromServices] KhoaTaiKhoanUseCase uc, CancellationToken ct)
    { await uc.ThucHienAsync(User.TaiKhoanId(), id, true, ct); return NoContent(); }

    [HttpPut("{id:int}/mo-khoa")]
    public async Task<IActionResult> MoKhoa(int id, [FromServices] KhoaTaiKhoanUseCase uc, CancellationToken ct)
    { await uc.ThucHienAsync(User.TaiKhoanId(), id, false, ct); return NoContent(); }
}
