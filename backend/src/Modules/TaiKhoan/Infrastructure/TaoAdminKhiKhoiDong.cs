using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NhaXe.Modules.TaiKhoan.Application;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.TaiKhoan.Infrastructure;

/// <summary>Admin đầu tiên, đọc từ biến môi trường AdminDauTien__HoTen / __Sdt / __Email / __MatKhau (file .env khi chạy Docker).</summary>
public record AdminDauTienOptions(string? HoTen, string? Sdt, string? Email, string? MatKhau);

/// <summary>
/// Tác vụ nền chạy một lần lúc API khởi động: CSDL chưa có admin thì tạo theo cấu hình.
/// Chỉ hẹn giờ và thử lại khi CSDL chưa sẵn sàng; nghiệp vụ nằm ở TaoAdminDauTienUseCase.
/// Mọi lỗi đều được bắt: không để việc tạo admin làm sập API.
/// </summary>
public sealed class TaoAdminKhiKhoiDong(IServiceScopeFactory scopes, AdminDauTienOptions opt, ILogger<TaoAdminKhiKhoiDong> log) : BackgroundService
{
    private const int SoLanThu = 12;

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if (string.IsNullOrWhiteSpace(opt.Sdt) || string.IsNullOrWhiteSpace(opt.MatKhau))
        {
            log.LogWarning("Chưa cấu hình AdminDauTien__Sdt / AdminDauTien__MatKhau. Nếu CSDL chưa có tài khoản ADMIN thì chưa ai vào được trang quản trị.");
            return;
        }
        for (var lan = 1; lan <= SoLanThu; lan++)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var uc = scope.ServiceProvider.GetRequiredService<TaoAdminDauTienUseCase>();
                if (await uc.ThucHienAsync(new(opt.HoTen ?? "Quản trị viên", opt.Sdt, opt.Email ?? "", opt.MatKhau), stop))
                    log.LogInformation("Đã tạo tài khoản ADMIN đầu tiên, đăng nhập bằng SĐT {Sdt}.", opt.Sdt);
                return;
            }
            catch (LoiNghiepVu ex)
            {
                // Cấu hình sai (SĐT, email, mật khẩu yếu, trùng tài khoản): thử lại cũng vô ích
                log.LogError("Không tạo được admin đầu tiên: {ThongDiep} Sửa biến AdminDauTien__* rồi khởi động lại API.", ex.Message);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Chưa kết nối được CSDL để kiểm tra admin (lần {Lan}/{Tong}), thử lại sau 5 giây.", lan, SoLanThu);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stop); }
            catch (OperationCanceledException) { return; }
        }
    }
}
