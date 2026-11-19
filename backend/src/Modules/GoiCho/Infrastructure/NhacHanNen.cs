using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NhaXe.Modules.GoiCho.Application;

namespace NhaXe.Modules.GoiCho.Infrastructure;

/// <summary>
/// Tác vụ nền: cứ mỗi chu kỳ gọi NhacSapHetHanUseCase. Không có logic nghiệp vụ ở đây, chỉ hẹn giờ.
/// Mỗi lần chạy tạo một scope DI mới (DbContext là Scoped, không giữ suốt đời tiến trình).
/// Chạy nhiều instance (docker compose --scale api=2) vẫn không nhắc trùng: store đánh dấu bằng một câu UPDATE.
/// </summary>
public class NhacHanNen : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NhacHanNen> _log;
    private readonly TimeSpan _chuKy;

    public NhacHanNen(IServiceScopeFactory scopes, ILogger<NhacHanNen> log, TimeSpan chuKy) { _scopes = scopes; _log = log; _chuKy = chuKy; }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        using var hen = new PeriodicTimer(_chuKy); // lần đầu chạy SAU một chu kỳ, để API khởi động xong
        while (await hen.WaitForNextTickAsync(stop))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var so = await scope.ServiceProvider.GetRequiredService<NhacSapHetHanUseCase>().ThucHienAsync(stop);
                if (so > 0) _log.LogInformation("Đã nhắc gia hạn {So} gói", so);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { _log.LogError(ex, "Nhắc gia hạn lỗi, thử lại ở chu kỳ sau"); }
        }
    }
}
