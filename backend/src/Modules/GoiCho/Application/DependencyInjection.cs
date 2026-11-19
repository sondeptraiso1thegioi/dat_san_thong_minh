using Microsoft.Extensions.DependencyInjection;
using NhaXe.Modules.GoiCho.Contracts;

namespace NhaXe.Modules.GoiCho.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddGoiChoApplication(this IServiceCollection s)
    {
        s.AddScoped<QuyenDungXe>();     // hỏi NhomGiaDinh
        s.AddScoped<TraCuuTrongBai>();  // bản sao xe_trong_bai (nghe sự kiện cổng) + phí quá hạn
        s.AddScoped<LayXeCuaToiUseCase>();
        s.AddScoped<ThemXeUseCase>();
        s.AddScoped<DuyetHangLoatUseCase>();
        s.AddScoped<TaiAnhGiayDangKyUseCase>();
        s.AddScoped<XemAnhGiayDangKyUseCase>();
        s.AddScoped<LayBangGiaUseCase>();
        s.AddScoped<LayLichSuGoiUseCase>();
        s.AddScoped<MuaGoiUseCase>();
        s.AddScoped<XemPhiQuaHanUseCase>();
        s.AddScoped<TraPhiQuaHanUseCase>();
        s.AddScoped<LayXeChoDuyetUseCase>();
        s.AddScoped<DuyetXeUseCase>();
        s.AddScoped<XemHuyGoiUseCase>();
        s.AddScoped<HuyGoiUseCase>();
        s.AddScoped<TimXeDaDuyetUseCase>();
        s.AddScoped<DemXeChoDuyetUseCase>();
        s.AddScoped<LayChiTietXeAdminUseCase>();
        s.AddScoped<GoXeUseCase>();
        s.AddScoped<LayLichSuGoiAdminUseCase>();
        s.AddScoped<AdminXemHuyGoiUseCase>();
        s.AddScoped<AdminHuyGoiUseCase>();
        s.AddScoped<DoiSucChuaUseCase>();
        s.AddScoped<LayBangGiaAdminUseCase>();
        s.AddScoped<DoiGiaGoiUseCase>();
        s.AddScoped<IGoiChoModule, GoiChoModule>(); // cổng công khai cho module khác
        return s;
    }
}
