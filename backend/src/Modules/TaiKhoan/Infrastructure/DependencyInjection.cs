using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NhaXe.Modules.TaiKhoan.Application;

namespace NhaXe.Modules.TaiKhoan.Infrastructure;

/// <summary>Điểm lắp ráp của module: nối interface với cài đặt cụ thể (DIP qua DI container).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddTaiKhoanModule(this IServiceCollection s, string connectionString, JwtOptions jwt, SmtpOptions smtp,
        AdminDauTienOptions? adminDauTien = null)
    {
        s.AddDbContext<TaiKhoanDbContext>(o => o.UseSqlServer(connectionString));
        s.AddScoped<ITaiKhoanRepository, TaiKhoanRepository>();
        s.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        s.AddSingleton(smtp);
        s.AddScoped<IGuiMaKhoiPhuc, GuiMaKhoiPhucSmtp>();
        s.AddSingleton(jwt);
        s.AddSingleton<ITokenService, JwtTokenService>();
        s.AddTaiKhoanApplication();
        s.AddSingleton(adminDauTien ?? new AdminDauTienOptions(null, null, null, null));
        s.AddHostedService<TaoAdminKhiKhoiDong>(); // CSDL chưa có admin thì tạo theo cấu hình
        return s;
    }
}
