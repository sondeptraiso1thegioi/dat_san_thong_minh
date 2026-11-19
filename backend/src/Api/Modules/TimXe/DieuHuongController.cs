using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NhaXe.Api.Common;
using NhaXe.Modules.TaiKhoan.Contracts;
using NhaXe.Modules.TimXe.Application;
using NhaXe.SharedKernel;

namespace NhaXe.Api.Modules.TimXe;

/// <summary>
/// Điều hướng trong bãi (người dùng): phiên dẫn đường của tôi, đổi khu đích, xem đường đi từ Cổng vào X.
/// App nghe SignalR /hub/thong-bao (loại KHU) để tải lại ngay, và hỏi lại GET cua-toi mỗi 3 giây khi đang dẫn đường
/// (dự phòng: API chạy 2 bản sau NGINX, chưa có backplane, tín hiệu phát ở bản này không tới app nối bản kia).
/// </summary>
[ApiController]
[Authorize(Roles = "USER")]
[Route("api/dieu-huong")]
[Tags("8. TimXe · Điều hướng trong bãi")]
public class DieuHuongController : ControllerBase
{
    [HttpGet("cua-toi")]
    public Task<DieuHuongCuaToiDto> CuaToi([FromServices] LayDieuHuongCuaToiUseCase uc, CancellationToken ct) =>
        uc.ThucHienAsync(User.TaiKhoanId(), ct);

    /// <summary>Đổi khu đích khi đang dẫn đường. Suất giữ chuyển sang khu mới, giữ nguyên thời hạn còn lại. Trả phiên kèm đường mới.</summary>
    [HttpPut("cua-toi/khu-dich")]
    public Task<PhienDieuHuongDto> DoiKhuDich([FromBody] DoiKhuDichLenh lenh, [FromServices] DoiKhuDichUseCase uc, CancellationToken ct) =>
        uc.ThucHienAsync(User.TaiKhoanId(), lenh.KhuId, ct);

    /// <summary>Chỉ đọc: đường ngắn nhất (Dijkstra trên đồ thị mốc) từ Cổng vào X tới khu. Không tạo phiên, không giữ suất.</summary>
    [HttpGet("duong-di"), Authorize(Roles = "ADMIN,USER")]
    public Task<DuongDiBanDoDto> DuongDi([FromQuery] string? tuKhu, [FromQuery] int denKhu, [FromServices] LayDuongDiUseCase uc, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(tuKhu) && !tuKhu.Equals("CONG_X", StringComparison.OrdinalIgnoreCase))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "DIEM_BAT_DAU_SAI", "Hiện chỉ có đường đi từ Cổng vào X (tuKhu=CONG_X).");
        return uc.ThucHienAsync(denKhu, ct);
    }
}

/// <summary>Admin (trang Quản lý bãi): cảnh báo đi sai khu và ai hay đi sai.</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/admin/tim-xe/di-sai-khu")]
[Tags("8. TimXe · Admin: camera khu")]
public class AdminDiSaiKhuController : ControllerBase
{
    public record DiSaiKhuAdminVaTenDto(IReadOnlyList<DiSaiKhuCoTenDto> GanDay, IReadOnlyList<DemDiSaiCoTenDto> TheoChuXe);
    public record DiSaiKhuCoTenDto(long Id, string BienSo, string TenChuXe, string? KhuChiDinh, string KhuThucTe, bool KhuDay, DateTime Luc);
    public record DemDiSaiCoTenDto(int ChuXeId, string TenChuXe, string Sdt, int SoLan, int SoLanKhuDay, DateTime LanCuoi);

    [HttpGet]
    public async Task<DiSaiKhuAdminVaTenDto> Lay([FromServices] LayDiSaiKhuUseCase uc, [FromServices] ITaiKhoanModule taiKhoan, CancellationToken ct)
    {
        var d = await uc.ThucHienAsync(50, ct);
        var lh = await taiKhoan.LayLienHeAsync(d.GanDay.Select(x => x.ChuXeId).Concat(d.TheoChuXe.Select(x => x.ChuXeId)), ct);
        return new(
            d.GanDay.Select(x => new DiSaiKhuCoTenDto(x.Id, x.BienSo, lh.GetValueOrDefault(x.ChuXeId)?.HoTen ?? "?", x.KhuChiDinh, x.KhuThucTe, x.KhuDay, x.Luc)).ToList(),
            d.TheoChuXe.Select(x => new DemDiSaiCoTenDto(x.ChuXeId, lh.GetValueOrDefault(x.ChuXeId)?.HoTen ?? "?", lh.GetValueOrDefault(x.ChuXeId)?.Sdt ?? "",
                x.SoLan, x.SoLanKhuDay, x.LanCuoi)).ToList());
    }
}
