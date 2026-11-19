using Microsoft.Extensions.DependencyInjection;
using NhaXe.Modules.CongRaVao.Contracts;

namespace NhaXe.Modules.CongRaVao.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCongRaVaoApplication(this IServiceCollection s)
    {
        s.AddScoped<ICongRaVaoModule, CongRaVaoModule>(); // cổng công khai cho module khác (BaiXe hỏi xe nào trong bãi)
        // Đọc biển bằng AI (module AiBienSo qua Contracts). Đổi từ bản giả lập sang AI chỉ sửa dòng này (OCP: use case không đổi)
        s.AddScoped<INhanDienBienSo, NhanDienBangAi>();
        s.AddScoped<KetThucYeuCau>();
        s.AddScoped<GhepYeuCau>();
        s.AddScoped<XeVaoUseCase>();
        s.AddScoped<XeRaUseCase>();
        s.AddScoped<NguoiDungXeXacNhanUseCase>();
        s.AddScoped<AdminMoTayUseCase>();
        // GoiCho báo đã trả phí quá hạn → mở barrier cho lượt đang chờ thanh toán
        s.AddScoped<NhaXe.SharedKernel.INghe<NhaXe.Modules.GoiCho.Contracts.DaTraPhiQuaHan>, NgheDaTraPhi>();
        s.AddScoped<HetHanXacNhanUseCase>();
        s.AddScoped<LayManHinhCongUseCase>();
        s.AddScoped<LayRaVaoCuaToiUseCase>();
        s.AddScoped<LayLuotCongUseCase>();
        s.AddScoped<LayAnhDoiChungUseCase>();
        // Barie mở theo sự kiện lượt gửi (TimXe cũng nghe 2 sự kiện này: một sự kiện, nhiều người nghe)
        s.AddScoped<NhaXe.SharedKernel.INghe<Contracts.XeDaVao>, MoBarieKhiXeVao>();
        s.AddScoped<NhaXe.SharedKernel.INghe<Contracts.XeDaRa>, MoBarieKhiXeRa>();
        return s;
    }
}
