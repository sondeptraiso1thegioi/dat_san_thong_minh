using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NhaXe.Api.Common;
using NhaXe.Modules.GoiCho.Application;

namespace NhaXe.Api.Modules.GoiCho;

/// <summary>Form tải ảnh (multipart/form-data): trường "anh" là tệp JPG/PNG ≤ 5 MB.</summary>
public class TaiAnhYeuCau
{
    public IFormFile? Anh { get; set; }
}

[ApiController]
[Authorize]
[Route("api/xe")]
[Tags("2a. GoiCho · Khách: xe của tôi")]
public class XeController : ControllerBase
{
    /// <summary>Xe tài khoản được dùng: xe của mình + xe người cùng nhóm bật chia sẻ.</summary>
    [HttpGet]
    public Task<IReadOnlyList<XeDuocDungDto>> LayDanhSach([FromServices] LayXeCuaToiUseCase uc, CancellationToken ct)
        => uc.ThucHienAsync(User.TaiKhoanId(), ct);

    [HttpPost]
    public async Task<IActionResult> Them([FromBody] ThemXeLenh lenh, [FromServices] ThemXeUseCase uc, CancellationToken ct)
        => StatusCode(201, new { xeId = await uc.ThucHienAsync(User.TaiKhoanId(), lenh, ct) });

    /// <summary>Tải (lại) ảnh giấy đăng ký. Xe bị từ chối thì tải ảnh mới = gửi lại để Admin duyệt.</summary>
    [HttpPut("{xeId:int}/giay-dang-ky")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> TaiAnh(int xeId, [FromForm] TaiAnhYeuCau yc, [FromServices] TaiAnhGiayDangKyUseCase uc, CancellationToken ct)
    {
        if (yc.Anh is null) return BadRequest(new { ma = "THIEU_ANH", thongDiep = "Chưa chọn ảnh giấy đăng ký." });
        await using var s = yc.Anh.OpenReadStream();
        await uc.ThucHienAsync(User.TaiKhoanId(), xeId, s, yc.Anh.Length, ct);
        return NoContent();
    }

    /// <summary>Xem ảnh giấy đăng ký: chỉ chủ xe và Admin.</summary>
    [HttpGet("{xeId:int}/giay-dang-ky")]
    public async Task<IActionResult> XemAnh(int xeId, [FromServices] XemAnhGiayDangKyUseCase uc, CancellationToken ct)
    {
        var anh = await uc.ThucHienAsync(User.TaiKhoanId(), User.IsInRole("ADMIN"), xeId, ct);
        Response.Headers.CacheControl = "private, no-store"; // giấy tờ cá nhân: không cho proxy/trình duyệt lưu đệm
        return File(anh.NoiDung, anh.ContentType);
    }

    [HttpGet("{xeId:int}/dang-ky")]
    public Task<IReadOnlyList<DangKyChoDto>> LichSuGoi(int xeId, [FromServices] LayLichSuGoiUseCase uc, CancellationToken ct)
        => uc.ThucHienAsync(User.TaiKhoanId(), xeId, ct);

    [HttpGet("{xeId:int}/phi-qua-han")]
    public async Task<IActionResult> XemPhiQuaHan(int xeId, [FromServices] XemPhiQuaHanUseCase uc, CancellationToken ct)
    {
        var no = await uc.ThucHienAsync(User.TaiKhoanId(), xeId, ct);
        return no is null ? NoContent() : Ok(no);
    }

    [HttpPost("{xeId:int}/phi-qua-han/thanh-toan")]
    public Task<PhiQuaHanDto> TraPhiQuaHan(int xeId, [FromServices] TraPhiQuaHanUseCase uc, CancellationToken ct)
        => uc.ThucHienAsync(User.TaiKhoanId(), xeId, ct);
}
