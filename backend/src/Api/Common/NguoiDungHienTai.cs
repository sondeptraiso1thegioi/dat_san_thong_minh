using System.Security.Claims;

namespace NhaXe.Api.Common;

public static class NguoiDungHienTai
{
    /// <summary>Id tài khoản lấy từ JWT. Không bao giờ lấy từ body/query, để không ai giả danh người khác.</summary>
    public static int TaiKhoanId(this ClaimsPrincipal u) => int.Parse(u.FindFirstValue("sub")!);
}
