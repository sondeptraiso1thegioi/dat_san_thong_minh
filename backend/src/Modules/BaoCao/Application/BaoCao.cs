using Microsoft.Extensions.DependencyInjection;
using NhaXe.Modules.CongRaVao.Contracts;
using NhaXe.Modules.GoiCho.Contracts;
using NhaXe.Modules.TimXe.Contracts;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.BaoCao.Application;

// Module 9 BaoCao (đề cương v10, luồng 9): không có bảng riêng, chỉ HỎI các module khác qua Contracts rồi ghép số liệu.
// Đứng đầu chiều phụ thuộc (BaoCao → TimXe → BaiXe → CongRaVao → GoiCho ...), không module nào gọi ngược BaoCao.

public record BaoCaoDto(DateTime Tu, DateTime Den, ThongKeCong Cong, DoanhThu DoanhThu, ThongKeDinhVi DinhVi,
                        IReadOnlyList<SucChuaLoaiXe> SucChua, decimal? TiLeAiTuDoc);

public class LayBaoCaoUseCase(ICongRaVaoModule cong, IGoiChoModule goiCho, ITimXeModule timXe, IClock clock)
{
    /// <summary>Không truyền khoảng thì lấy từ 00:00 hôm nay tới hiện tại. Tối đa 366 ngày.</summary>
    public async Task<BaoCaoDto> ThucHienAsync(DateTime? tu, DateTime? den, CancellationToken ct = default)
    {
        var d = den ?? clock.BayGio;
        var t = tu ?? clock.BayGio.Date;
        if (t >= d) throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "KHOANG_SAI", "Thời điểm bắt đầu phải trước thời điểm kết thúc.");
        if ((d - t).TotalDays > 366) throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "KHOANG_QUA_DAI", "Báo cáo tối đa 366 ngày.");
        var c = await cong.LayThongKeAsync(t, d, ct);
        var tong = c.SoLuotVao + c.SoLuotRa;
        // Tỉ lệ lần cho qua mà AI tự đọc biển (không cần Admin xác nhận / sửa / mở tay)
        decimal? aiTuDoc = tong == 0 ? null : Math.Round(100m * c.SoAiTuDoc / tong, 1);
        return new BaoCaoDto(t, d, c, await goiCho.LayDoanhThuAsync(t, d, ct), await timXe.LayThongKeDinhViAsync(t, d, ct),
                             await goiCho.LaySucChuaAsync(ct), aiTuDoc);
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddBaoCaoModule(this IServiceCollection s) => s.AddScoped<LayBaoCaoUseCase>();
}
