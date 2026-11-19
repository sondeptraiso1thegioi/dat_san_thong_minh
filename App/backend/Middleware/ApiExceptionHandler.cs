using DatSan.Api.DTOs;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;

namespace DatSan.Api.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, message) = exception switch
        {
            ApiException error => (error.StatusCode, error.Code, error.Message),
            SqlException => (503, "DATABASE_UNAVAILABLE", "Chưa kết nối được dữ liệu. Vui lòng thử lại sau."),
            _ => (500, "INTERNAL_ERROR", "Có lỗi xảy ra. Vui lòng thử lại sau.")
        };
        if (status >= 500)
            logger.LogError(exception, "API request failed with {Code}", code);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ApiError(message, code), cancellationToken);
        return true;
    }
}
