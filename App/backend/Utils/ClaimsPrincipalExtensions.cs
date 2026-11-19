using System.Security.Claims;
using DatSan.Api.Middleware;

namespace DatSan.Api.Utils;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Id người dùng trong claim "sub" của JWT.</summary>
    public static long GetUserId(this ClaimsPrincipal principal) =>
        long.TryParse(principal.FindFirst("sub")?.Value, out var id)
            ? id
            : throw new ApiException(401, "UNAUTHORIZED", "Phiên đăng nhập không hợp lệ.");
}
