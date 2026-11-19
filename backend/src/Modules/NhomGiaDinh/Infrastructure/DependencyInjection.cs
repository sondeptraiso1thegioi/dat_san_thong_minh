using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NhaXe.Modules.NhomGiaDinh.Application;

namespace NhaXe.Modules.NhomGiaDinh.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNhomGiaDinhModule(this IServiceCollection s, string connectionString)
    {
        s.AddDbContext<NhomGiaDinhDbContext>(o => o.UseSqlServer(connectionString));
        s.AddScoped<IThanhVienNhomQuery, ThanhVienNhomQuery>();
        s.AddScoped<INhomRepository, NhomRepository>();
        s.AddSingleton<IMaMoiGenerator, MaMoiGenerator>();
        s.AddNhomGiaDinhApplication();
        return s;
    }
}
