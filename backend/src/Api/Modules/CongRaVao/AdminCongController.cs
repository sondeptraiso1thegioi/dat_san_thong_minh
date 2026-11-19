using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NhaXe.Api.Common;
using NhaXe.Modules.CongRaVao.Application;

namespace NhaXe.Api.Modules.CongRaVao;

/// <summary>
/// Màn hình cổng của quản lý bãi. Hai cách đưa xe tới cổng:
/// - vao / ra: gõ (hoặc chọn) biển số, dùng khi chưa có camera hoặc AI không chắc;
/// - vao/anh / ra/anh: gửi ảnh camera, Model A + PaddleOCR đọc biển (module AiBienSo). Sau bước đọc biển, luồng giống hệt.
/// </summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/admin/cong")]
[Tags("5. CongRaVao · Admin: cổng ra vào")]
public class AdminCongController : ControllerBase
{
    /// <summary>Yêu cầu đang chờ xác nhận, nhật ký cổng gần đây, danh sách xe để chọn nhanh.</summary>
    [HttpGet]
    public Task<ManHinhCongDto> ManHinh([FromServices] LayManHinhCongUseCase uc, CancellationToken ct) => uc.ThucHienAsync(ct);

    /// <summary>Xe tới cổng vào.</summary>
    [HttpPost("vao")]
    public Task<KetQuaCongDto> Vao([FromBody] XeTaiCongLenh lenh, [FromServices] XeVaoUseCase uc, CancellationToken ct) =>
        uc.ThucHienAsync(new AnhCong(lenh.BienSo, AdminId: User.TaiKhoanId()), ct);

    /// <summary>Xe tới cổng ra: tạo yêu cầu xác nhận 60 giây gửi người có quyền dùng xe (chưa mở cổng).</summary>
    [HttpPost("ra")]
    public Task<KetQuaCongDto> Ra([FromBody] XeTaiCongLenh lenh, [FromServices] XeRaUseCase uc, CancellationToken ct) =>
        uc.ThucHienAsync(new AnhCong(lenh.BienSo, AdminId: User.TaiKhoanId()), ct);

    /// <summary>Xe tới cổng vào, kèm ảnh camera: AI đọc biển. Gõ thêm bienSo thì dùng biển gõ tay.</summary>
    [HttpPost("vao/anh"), Consumes("multipart/form-data"), RequestSizeLimit(6 * 1024 * 1024)]
    /// <remarks>tuDong = true: khung hình màn hình camera tự chụp; không thấy biển / biển vừa xử lý trong 30 giây thì bỏ qua, không ghi nhật ký.</remarks>
    public async Task<KetQuaCongDto> VaoBangAnh(IFormFile anh, [FromForm] string? bienSo, [FromForm] bool tuDong, [FromServices] XeVaoUseCase uc, CancellationToken ct) =>
        await uc.ThucHienAsync(await AnhTuForm(anh, bienSo, User.TaiKhoanId(), ct) with { TuDong = tuDong }, ct);

    /// <summary>Xe tới cổng ra, kèm ảnh camera.</summary>
    [HttpPost("ra/anh"), Consumes("multipart/form-data"), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<KetQuaCongDto> RaBangAnh(IFormFile anh, [FromForm] string? bienSo, [FromForm] bool tuDong, [FromServices] XeRaUseCase uc, CancellationToken ct) =>
        await uc.ThucHienAsync(await AnhTuForm(anh, bienSo, User.TaiKhoanId(), ct) with { TuDong = tuDong }, ct);

    /// <summary>Trạng thái lượt gửi: màn hình cổng ra hỏi lại trong lúc chờ người dùng xác nhận trên app.</summary>
    [HttpGet("luot/{id:int}")]
    public Task<LuotCongDto> Luot(int id, [FromServices] LayLuotCongUseCase uc, CancellationToken ct) => uc.ThucHienAsync(id, ct);

    /// <summary>Ảnh đối chứng chụp ở cổng vào ("vao") / cổng ra ("ra") của lượt gửi.</summary>
    [HttpGet("luot/{id:int}/anh/{loai:regex(^(vao|ra)$)}")]
    public async Task<IActionResult> AnhDoiChung(int id, string loai, [FromServices] LayAnhDoiChungUseCase uc, CancellationToken ct)
    {
        var s = await uc.ThucHienAsync(id, loai, ct);
        return s is null ? NotFound() : File(s, "image/jpeg");
    }

    private static async Task<AnhCong> AnhTuForm(IFormFile anh, string? bienSo, int adminId, CancellationToken ct)
    {
        if (anh.Length is <= 0 or > 5 * 1024 * 1024)
            throw new NhaXe.SharedKernel.LoiNghiepVu(NhaXe.SharedKernel.LoaiLoi.DuLieuKhongHopLe, "ANH_SAI_KICH_THUOC", "Ảnh phải có dữ liệu và không quá 5 MB.");
        using var ms = new MemoryStream();
        await anh.CopyToAsync(ms, ct);
        return new AnhCong(bienSo, ms.ToArray(), anh.ContentType, adminId);
    }

    /// <summary>Mở barrier thủ công (UC-A10): hết 60 giây không ai trả lời, nghi mất đã kiểm tra, app / AI lỗi. Bắt buộc lý do.</summary>
    [HttpPost("mo-tay")]
    public Task<KetQuaCongDto> MoTay([FromBody] MoTayLenh lenh, [FromServices] AdminMoTayUseCase uc, CancellationToken ct) =>
        uc.ThucHienAsync(User.TaiKhoanId(), lenh.LuotGuiId, lenh.LyDo, ct);
}
