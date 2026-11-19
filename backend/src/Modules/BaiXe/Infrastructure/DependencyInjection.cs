using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NhaXe.Modules.BaiXe.Application;

namespace NhaXe.Modules.BaiXe.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBaiXeModule(this IServiceCollection s, string connectionString)
    {
        s.AddDbContext<BaiXeDbContext>(o => o.UseSqlServer(connectionString));
        s.AddScoped<IBaiXeQuery, BaiXeQuery>();
        s.AddScoped<IBaiXeRepository, BaiXeRepository>();
        s.AddScoped<ICauHinhBaiQuery, CauHinhBaiQuery>();
        s.AddBaiXeApplication();
        return s;
    }
}
