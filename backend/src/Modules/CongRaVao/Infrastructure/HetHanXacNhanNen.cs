using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NhaXe.Modules.CongRaVao.Application;

namespace NhaXe.Modules.CongRaVao.Infrastructure;

/// <summary>
/// Tác vụ nền: vài giây một lần kết thúc các yêu cầu xác nhận đã quá 60 giây (cổng không mở, lượt gửi quay lại "đang gửi").
/// Chỉ hẹn giờ; quy tắc nằm trong HetHanXacNhanUseCase / YeuCauXacNhan.
/// Hai instance API cùng chạy: instance nào lưu sau sẽ gặp ROWVERSION khác và bỏ qua yêu cầu đó, nên không xử lý trùng.
/// </summary>
public class HetHanXacNhanNen : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<HetHanXacNhanNen> _log;
    private readonly TimeSpan _chuKy;

    public HetHanXacNhanNen(IServiceScopeFactory scopes, ILogger<HetHanXacNhanNen> log, TimeSpan chuKy)
    { _scopes = scopes; _log = log; _chuKy = chuKy; }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        using var hen = new PeriodicTimer(_chuKy);
        while (await hen.WaitForNextTickAsync(stop))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var so = await scope.ServiceProvider.GetRequiredService<HetHanXacNhanUseCase>().ThucHienAsync(stop);
                if (so > 0) _log.LogInformation("Đã kết thúc {So} yêu cầu xác nhận quá hạn", so);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { _log.LogError(ex, "Xử lý hết hạn xác nhận lỗi, thử lại ở chu kỳ sau"); }
        }
    }
}
