using Microsoft.Extensions.DependencyInjection;
using NhaXe.Modules.NhomGiaDinh.Contracts;

namespace NhaXe.Modules.NhomGiaDinh.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNhomGiaDinhApplication(this IServiceCollection s)
    {
        s.AddScoped<INhomGiaDinhModule, NhomGiaDinhModule>(); // cổng công khai cho module khác
        s.AddScoped<XemNhomUseCase>();
        s.AddScoped<TaoNhomUseCase>();
        s.AddScoped<XinThamGiaUseCase>();
        s.AddScoped<XuLyYeuCauUseCase>();
        s.AddScoped<DoiMaMoiUseCase>();
        s.AddScoped<RoiNhomUseCase>();
        s.AddScoped<GiaiTanNhomUseCase>();
        return s;
    }
}
