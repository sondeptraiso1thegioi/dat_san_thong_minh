using Microsoft.Extensions.DependencyInjection;
using NhaXe.Modules.CongRaVao.Contracts;
using NhaXe.Modules.TimXe.Contracts;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.TimXe.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTimXeApplication(this IServiceCollection s)
    {
        s.AddScoped<TinhChoKhu>();
        s.AddScoped<NgheCongTimXe>();
        // Nghe sự kiện cổng: CongRaVao không gọi TimXe (luật không vòng phụ thuộc)
        s.AddScoped<INghe<XeDaVao>>(sp => sp.GetRequiredService<NgheCongTimXe>());
        s.AddScoped<INghe<XeDaRa>>(sp => sp.GetRequiredService<NgheCongTimXe>());
        s.AddScoped<LayChiDanUseCase>();
        s.AddScoped<XuLyCameraKhuUseCase>();
        s.AddScoped<DaDoXeUseCase>();
        s.AddScoped<LayMucChoUseCase>();
        s.AddScoped<TimXeCuaToiUseCase>();
        s.AddScoped<LaySoXeKhuUseCase>();
        s.AddScoped<AdminGanKhuUseCase>();
        s.AddScoped<HetHanGiuChoUseCase>();
        // Điều hướng trong bãi (mở rộng chọn khu + giữ suất)
        s.AddScoped<LayDuongDiUseCase>();
        s.AddScoped<LayDieuHuongCuaToiUseCase>();
        s.AddScoped<DoiKhuDichUseCase>();
        s.AddScoped<LayDiSaiKhuUseCase>();
        s.AddScoped<ITimXeModule, TimXeModule>();
        return s;
    }
}
