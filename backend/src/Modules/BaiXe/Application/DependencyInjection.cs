using Microsoft.Extensions.DependencyInjection;
using NhaXe.Modules.BaiXe.Contracts;

namespace NhaXe.Modules.BaiXe.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddBaiXeApplication(this IServiceCollection s)
    {
        s.AddScoped<LaySoDoBaiUseCase>();
        s.AddScoped<LayCauHinhBaiUseCase>();
        s.AddScoped<ThemKhuUseCase>();
        s.AddScoped<SuaKhuUseCase>();
        s.AddScoped<XoaKhuUseCase>();
        s.AddScoped<ThemOUseCase>();
        s.AddScoped<SuaOUseCase>();
        s.AddScoped<XoaOUseCase>();
        s.AddScoped<DatTrangThaiOUseCase>();
        s.AddScoped<ThemCameraUseCase>();
        s.AddScoped<SuaCameraUseCase>();
        s.AddScoped<XoaCameraUseCase>();
        s.AddScoped<LayChoTrongUseCase>();
        s.AddScoped<IBaiXeModule, BaiXeModule>(); // cổng công khai (AI ô trống sẽ gọi)
        return s;
    }
}
