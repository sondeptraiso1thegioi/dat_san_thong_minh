using Microsoft.Extensions.Logging;
using NhaXe.Modules.GoiCho.Application;

namespace NhaXe.Modules.GoiCho.Infrastructure;

/// <summary>Cổng thanh toán giả lập cho demo: luôn thành công. Thay bằng cổng thật chỉ cần đổi 1 dòng đăng ký DI.</summary>
public class ThanhToanGiaLap : IThanhToanGateway
{
    private readonly ILogger<ThanhToanGiaLap> _log;
    public ThanhToanGiaLap(ILogger<ThanhToanGiaLap> log) => _log = log;

    public Task<KetQuaThanhToan> ThuTienAsync(int taiKhoanId, decimal soTien, string noiDung, CancellationToken ct)
    {
        var ma = "GL-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        _log.LogInformation("[Thanh toán giả lập] {Ma}: tài khoản {Tk} trả {Tien:N0} đ - {NoiDung}", ma, taiKhoanId, soTien, noiDung);
        return Task.FromResult(new KetQuaThanhToan(true, ma));
    }

    public Task HoanTienAsync(string maGiaoDich, CancellationToken ct)
    {
        _log.LogInformation("[Thanh toán giả lập] Hoàn tiền giao dịch {Ma}", maGiaoDich);
        return Task.CompletedTask;
    }
}
