using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NhaXe.Api.Common;
using NhaXe.Modules.BaiXe.Contracts;
using NhaXe.Modules.CongRaVao.Contracts;
using NhaXe.Modules.GoiCho.Application;

namespace NhaXe.Api.Modules.GoiCho;

/// <summary>DongY = false thì bắt buộc LyDo (≥ 5 ký tự), chủ xe sẽ thấy lý do này.</summary>
public record DuyetXeYeuCau(bool DongY, string? LyDo);
public record GoXeYeuCau(string? LyDo);
public record SoLuongDto(int SoLuong);

[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/admin/xe")]
[Tags("2c. GoiCho · Admin: duyệt xe")]
public class AdminXeController : ControllerBase
{
    [HttpGet("cho-duyet")]
    public Task<IReadOnlyList<XeChoDuyetDto>> ChoDuyet([FromServices] LayXeChoDuyetUseCase uc, CancellationToken ct) => uc.ThucHienAsync(ct);

    /// <summary>Số xe đang chờ duyệt (badge trên menu "Quản lý xe").</summary>
    [HttpGet("cho-duyet/so-luong")]
    public async Task<SoLuongDto> SoLuongChoDuyet([FromServices] DemXeChoDuyetUseCase uc, CancellationToken ct) => new(await uc.ThucHienAsync(ct));

    [HttpPut("{xeId:int}/duyet")]
    public async Task<IActionResult> Duyet(int xeId, [FromBody] DuyetXeYeuCau yc, [FromServices] DuyetXeUseCase uc, CancellationToken ct)
    {
        await uc.ThucHienAsync(User.TaiKhoanId(), xeId, yc.DongY, yc.LyDo, ct);
        return NoContent();
    }

    /// <summary>Duyệt / từ chối nhiều xe một lần (tối đa 50). Xe lỗi được liệt kê riêng, xe khác vẫn xử lý.</summary>
    [HttpPost("duyet-hang-loat")]
    public Task<KetQuaHangLoatDto> DuyetHangLoat([FromBody] DuyetHangLoatLenh lenh, [FromServices] DuyetHangLoatUseCase uc, CancellationToken ct)
        => uc.ThucHienAsync(User.TaiKhoanId(), lenh, ct);

    /// <summary>
    /// Xe đã duyệt: q = biển số (không phân biệt chấm, gạch, khoảng trắng, hoa thường; tìm "chứa"),
    /// loc = TAT_CA | CON_HAN | SAP_HET (≤ 72 giờ) | CHUA_CO (hết hạn / chưa mua) | TRONG_BAI, phân trang (mặc định 20 xe).
    /// </summary>
    [HttpGet("da-duyet")]
    public Task<TrangXeDaDuyetDto> DaDuyet([FromQuery] string? q, [FromQuery] string? loc, [FromQuery] int trang, [FromQuery] int soDong,
                                          [FromServices] TimXeDaDuyetUseCase uc, CancellationToken ct)
        => uc.ThucHienAsync(q, loc, trang < 1 ? 1 : trang, soDong < 1 ? TimXeDaDuyetUseCase.SoDongMacDinh : soDong, ct);

    /// <summary>
    /// Chi tiết một xe cho khung bên phải: xe, chủ xe, gói hiện tại, lịch sử gói, phí quá hạn (GoiCho),
    /// giờ vào bãi (CongRaVao) và khu đang đỗ (BaiXe), mỗi phần hỏi qua Contracts của module sở hữu.
    /// </summary>
    [HttpGet("{xeId:int}")]
    public async Task<ChiTietXeAdminDto> ChiTiet(int xeId, [FromServices] LayChiTietXeAdminUseCase uc, [FromServices] ICongRaVaoModule cong,
                                                 [FromServices] IBaiXeModule baiXe, CancellationToken ct)
    {
        var ct0 = await uc.ThucHienAsync(xeId, ct);
        var luot = (await cong.LayLuotGuiDangMoCuaXeAsync(new[] { xeId }, ct)).GetValueOrDefault(xeId);
        if (luot is null) return ct0;
        var khu = luot.ViTriKhuId is int k ? (await baiXe.LayKhuAsync(ct)).FirstOrDefault(x => x.Id == k)?.Ten : null;
        return ct0 with { TrongBai = new TrongBaiAdminDto(true, luot.ThoiGianVao, khu) };
    }

    /// <summary>Gỡ xe phạm quy (gỡ mềm). Gói còn hạn bị huỷ, không hoàn tiền.</summary>
    [HttpPut("{xeId:int}/go")]
    public async Task<IActionResult> Go(int xeId, [FromBody] GoXeYeuCau yc, [FromServices] GoXeUseCase uc, CancellationToken ct)
    {
        await uc.ThucHienAsync(User.TaiKhoanId(), xeId, yc.LyDo, ct);
        return NoContent();
    }
}
