using Microsoft.Extensions.DependencyInjection;
using NhaXe.Modules.AiBienSo.Contracts;

namespace NhaXe.Modules.AiBienSo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAiBienSoApplication(this IServiceCollection s)
    {
        s.AddScoped<DocBienSoUseCase>();
        s.AddScoped<DocAnhTaiLenUseCase>();
        s.AddScoped<IAiBienSoModule, AiBienSoModule>();
        return s;
    }
}
