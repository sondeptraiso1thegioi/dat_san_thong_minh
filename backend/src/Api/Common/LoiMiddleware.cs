using NhaXe.SharedKernel;

namespace NhaXe.Api.Common;

/// <summary>
/// Một nơi duy nhất đổi lỗi nghiệp vụ thành phản hồi HTTP (SRP): controller không cần try/catch.
/// Mọi lỗi trả cùng một dạng JSON { ma, thongDiep } để frontend xử lý thống nhất.
/// </summary>
public sealed class LoiMiddleware(RequestDelegate next, ILogger<LoiMiddleware> log)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (LoiNghiepVu ex)
        {
            ctx.Response.StatusCode = MaHttp(ex.Loai);
            await ctx.Response.WriteAsJsonAsync(new { ma = ex.Ma, thongDiep = ex.Message });
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Lỗi hệ thống");
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ctx.Response.WriteAsJsonAsync(new { ma = "LOI_HE_THONG", thongDiep = "Hệ thống gặp lỗi, thử lại sau." });
        }
    }

    public static int MaHttp(LoaiLoi loai) => loai switch
    {
        LoaiLoi.DuLieuKhongHopLe => StatusCodes.Status400BadRequest,
        LoaiLoi.ChuaDangNhap => StatusCodes.Status401Unauthorized,
        LoaiLoi.KhongCoQuyen => StatusCodes.Status403Forbidden,
        LoaiLoi.KhongTimThay => StatusCodes.Status404NotFound,
        LoaiLoi.QuaNhieuLan => StatusCodes.Status429TooManyRequests,
        LoaiLoi.DichVuTamNgung => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status409Conflict
    };
}
