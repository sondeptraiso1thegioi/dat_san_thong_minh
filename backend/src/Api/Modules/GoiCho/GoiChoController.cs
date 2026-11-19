using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NhaXe.Modules.GoiCho.Domain;
using NhaXe.Api.Common;
using NhaXe.Modules.GoiCho.Application;

namespace NhaXe.Api.Modules.GoiCho;

public record HuyGoiYeuCau(string? LyDo, string? GopY);

[ApiController]
[Authorize]
[Tags("2b. GoiCho · Khách: mua / huỷ gói")]
public class GoiChoController : ControllerBase
{
    /// <summary>Gói đang bán + số chỗ còn bán theo loại xe (view v_cho_con_ban).</summary>
    [HttpGet("api/goi-cho")]
    public Task<BangGiaDto> BangGia([FromServices] LayBangGiaUseCase uc, CancellationToken ct) => uc.ThucHienAsync(ct);

    /// <summary>Mua mới hoặc gia hạn (sp_mua_goi tự nhận biết gia hạn nối tiếp).</summary>
    [HttpPost("api/dang-ky-cho")]
    public async Task<IActionResult> MuaGoi([FromBody] MuaGoiLenh lenh, [FromServices] MuaGoiUseCase uc, CancellationToken ct)
        => StatusCode(201, await uc.ThucHienAsync(User.TaiKhoanId(), lenh, ct));

    /// <summary>Xem trước số tiền được hoàn nếu huỷ gói.</summary>
    [HttpGet("api/dang-ky-cho/{dangKyChoId:int}/huy")]
    public Task<XemHuyGoiDto> XemHuy(int dangKyChoId, [FromServices] XemHuyGoiUseCase uc, CancellationToken ct)
        => uc.ThucHienAsync(User.TaiKhoanId(), dangKyChoId, ct);

    /// <summary>Các lý do khách được chọn khi huỷ gói.</summary>
    [HttpGet("api/dang-ky-cho/ly-do-huy")]
    public IReadOnlyList<string> LyDoHuy() => LyDoKhachHuy.DanhSach;

    /// <summary>
    /// Huỷ gói trước hạn (chỉ chủ xe, chỉ gói cuối), hoàn tiền theo chính sách.
    /// Body không bắt buộc: { lyDo, gopY }. Admin nhận thông báo ai đã huỷ kèm lý do và góp ý.
    /// </summary>
    [HttpPost("api/dang-ky-cho/{dangKyChoId:int}/huy")]
    public Task<XemHuyGoiDto> Huy(int dangKyChoId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] HuyGoiYeuCau? yc,
                                  [FromServices] HuyGoiUseCase uc, CancellationToken ct)
        => uc.ThucHienAsync(User.TaiKhoanId(), dangKyChoId, yc?.LyDo, yc?.GopY, ct);
}
