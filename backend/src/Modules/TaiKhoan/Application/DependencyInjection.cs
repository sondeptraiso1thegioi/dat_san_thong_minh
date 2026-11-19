using Microsoft.Extensions.DependencyInjection;
using NhaXe.Modules.TaiKhoan.Contracts;

namespace NhaXe.Modules.TaiKhoan.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTaiKhoanApplication(this IServiceCollection s)
    {
        s.AddScoped<DangNhapUseCase>();
        s.AddScoped<DangKyUseCase>();
        s.AddScoped<TaoAdminDauTienUseCase>();
        s.AddScoped<QuenMatKhauUseCase>();
        s.AddScoped<DatLaiMatKhauUseCase>();
        s.AddScoped<DoiMatKhauUseCase>();
        s.AddScoped<DangXuatUseCase>();
        s.AddScoped<DanhSachTaiKhoanUseCase>();
        s.AddScoped<KhoaTaiKhoanUseCase>();
        s.AddScoped<ITaiKhoanModule, TaiKhoanModule>();
        return s;
    }
}
