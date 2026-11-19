using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NhaXe.Api.Common;
using NhaXe.Modules.GoiCho.Application;
using NhaXe.Modules.GoiCho.Contracts;
using NhaXe.Modules.GoiCho.Domain;

namespace NhaXe.Api.Modules.GoiCho;

public record DoiSucChuaYeuCau(int SucChua);
public record DoiGiaYeuCau(decimal Gia);
public record AdminHuyGoiYeuCau(string? LyDo, string? ChinhSach);

/// <summary>Admin của module GoiCho: bảng giá, đổi giá, sức chứa, lịch sử gói, huỷ gói có lý do.</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/admin")]
[Tags("2d. GoiCho · Admin: gói, giá, sức chứa")]
public class AdminGoiChoController : ControllerBase
{
    /// <summary>Tất cả gói (kể cả gói ngừng bán) kèm giá hiện tại và sức chứa từng loại xe.</summary>
    [HttpGet("goi-cho")]
    public Task<BangGiaAdminDto> BangGia([FromServices] LayBangGiaAdminUseCase uc, CancellationToken ct) => uc.ThucHienAsync(ct);

    /// <summary>
    /// Đổi giá một gói. Giá mới chỉ áp cho lần mua sau: gói khách đã mua giữ nguyên số tiền đã trả,
    /// hoàn tiền vẫn tính theo số tiền đã trả. Khách đang mở trang Mua gói với giá cũ sẽ nhận 409 GIA_DA_DOI.
    /// </summary>
    [HttpPut("goi-cho/{goiId:int}/gia")]
    public Task<GoiAdminDto> DoiGia(int goiId, [FromBody] DoiGiaYeuCau yc, [FromServices] DoiGiaGoiUseCase uc, CancellationToken ct) =>
        uc.ThucHienAsync(goiId, yc.Gia, ct);

    [HttpPut("loai-xe/{loaiXeId:int}/suc-chua")]
    public Task<SucChuaLoaiXe> DoiSucChua(int loaiXeId, [FromBody] DoiSucChuaYeuCau yc, [FromServices] DoiSucChuaUseCase uc, CancellationToken ct) =>
        uc.ThucHienAsync(User.TaiKhoanId(), loaiXeId, yc.SucChua, ct);

    [HttpGet("xe/{xeId:int}/dang-ky")]
    public Task<IReadOnlyList<DangKyChoDto>> LichSuGoi(int xeId, [FromServices] LayLichSuGoiAdminUseCase uc, CancellationToken ct) =>
        uc.ThucHienAsync(xeId, ct);

    [HttpGet("chinh-sach-hoan")]
    public IEnumerable<ChinhSachHoanDto> ChinhSachHoan([FromServices] BangChinhSachHoan bang) =>
        bang.TatCa.Select(c => new ChinhSachHoanDto(c.Ma, c.Ten));

    /// <summary>Xem trước số tiền hoàn nếu Admin huỷ gói theo chính sách đã chọn.</summary>
    [HttpGet("dang-ky-cho/{id:int}/huy")]
    public Task<XemHuyGoiDto> XemHuy(int id, [FromQuery] string? chinhSach, [FromServices] AdminXemHuyGoiUseCase uc, CancellationToken ct) =>
        uc.ThucHienAsync(id, chinhSach, ct);

    /// <summary>Admin huỷ gói, bắt buộc lý do. Chủ xe thấy lý do trên thẻ xe và nhận thông báo.</summary>
    [HttpPost("dang-ky-cho/{id:int}/huy")]
    public Task<XemHuyGoiDto> Huy(int id, [FromBody] AdminHuyGoiYeuCau yc, [FromServices] AdminHuyGoiUseCase uc, CancellationToken ct) =>
        uc.ThucHienAsync(User.TaiKhoanId(), id, new AdminHuyGoiLenh(yc.LyDo, yc.ChinhSach), ct);
}
