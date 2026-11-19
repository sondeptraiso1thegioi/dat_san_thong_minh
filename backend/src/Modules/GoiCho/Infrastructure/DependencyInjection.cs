using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NhaXe.Modules.CongRaVao.Contracts;
using NhaXe.Modules.GoiCho.Application;
using NhaXe.Modules.GoiCho.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.GoiCho.Infrastructure;

public static class DependencyInjection
{
    /// <param name="chuKyNhacHan">Bao lâu quét gói sắp hết hạn một lần. null = không chạy tác vụ nền (dùng trong test).</param>
    public static IServiceCollection AddGoiChoModule(this IServiceCollection s, string connectionString, string thuMucTep,
                                                     TimeSpan? chuKyNhacHan = null)
    {
        s.AddSingleton<ILuuTruTep>(new LuuTruTepTrenDia(thuMucTep)); // đổi sang MinIO: chỉ sửa dòng này
        s.AddDbContext<GoiChoDbContext>(o => o.UseSqlServer(connectionString));
        s.AddScoped<IXeRepository, XeRepository>();
        s.AddScoped<ICauHinh, CauHinhStore>();
        s.AddScoped<IGoiChoRepository, GoiChoRepository>();
        s.AddScoped<IXeQuery, XeQuery>();
        s.AddScoped<IDangKyChoStore, DangKyChoStore>();
        s.AddScoped<IPhiQuaHanStore, PhiQuaHanStore>();
        s.AddScoped<IThanhToanGateway, ThanhToanGiaLap>(); // đổi sang cổng thật: chỉ sửa dòng này
        s.AddScoped<IQuanLyXeStore, QuanLyXeStore>();
        s.AddSingleton<IChinhSachHoanTien, HoanTheoNgayConLai>(); // chính sách khi NGƯỜI DÙNG tự huỷ: đổi chỉ sửa dòng này
        // các chính sách Admin được chọn khi huỷ gói; thêm chính sách mới = thêm một phần tử
        s.AddSingleton(new BangChinhSachHoan(new IChinhSachHoanTien[] { new HoanTheoNgayConLai(), new KhongHoanTien() }));
        s.AddScoped<ISucChuaQuery, SucChuaQuery>();
        s.AddScoped<IDoanhThuQuery, DoanhThuQuery>();
        s.AddScoped<ISucChuaStore, SucChuaStore>();

        // Nghe sự kiện cổng (XeDaVao / XeDaRa) để tự giữ bản sao xe_trong_bai
        s.AddScoped<IXeTrongBaiStore, XeTrongBaiStore>();
        s.AddScoped<NgheXeVaoRa>();
        s.AddScoped<INghe<XeDaVao>>(sp => sp.GetRequiredService<NgheXeVaoRa>());
        s.AddScoped<INghe<XeDaRa>>(sp => sp.GetRequiredService<NgheXeVaoRa>());

        // Nhắc gia hạn trước 72 giờ (tác vụ nền)
        s.AddScoped<INhacHanStore, NhacHanStore>();
        s.AddScoped<NhacSapHetHanUseCase>();
        if (chuKyNhacHan is TimeSpan ck)
            s.AddHostedService(sp => new NhacHanNen(sp.GetRequiredService<IServiceScopeFactory>(),
                                                    sp.GetRequiredService<ILogger<NhacHanNen>>(), ck));

        s.AddGoiChoApplication();
        return s;
    }
}
