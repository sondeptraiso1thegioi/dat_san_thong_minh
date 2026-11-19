using Microsoft.Extensions.DependencyInjection;
using NhaXe.Modules.ThongBao.Contracts;

namespace NhaXe.Modules.ThongBao.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddThongBaoApplication(this IServiceCollection s)
    {
        s.AddScoped<LayThongBaoAdminUseCase>();
        s.AddScoped<DanhDauDaDocUseCase>();
        s.AddScoped<LayHopThuUseCase>();
        s.AddScoped<DemChuaDocUseCase>();
        s.AddScoped<DocHetUseCase>();
        s.AddScoped<IThongBaoModule, ThongBaoModule>(); // cổng công khai cho module khác
        return s;
    }
}
