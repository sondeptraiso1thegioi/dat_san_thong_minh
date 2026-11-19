using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NhaXe.Modules.ThongBao.Application;

namespace NhaXe.Modules.ThongBao.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddThongBaoModule(this IServiceCollection s, string connectionString)
    {
        s.AddDbContext<ThongBaoDbContext>(o => o.UseSqlServer(connectionString));
        s.AddScoped<IThongBaoStore, ThongBaoStore>();
        s.AddThongBaoApplication();
        return s;
    }
}
