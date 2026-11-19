namespace NhaXe.Modules.TimXe.Contracts;

/// <summary>
/// Cổng giao tiếp CÔNG KHAI của module TimXe (số 8). Module BaoCao hỏi tỉ lệ định vị qua interface này.
/// TimXe sở hữu giu_cho_khu và lịch sử vi_tri_xe; khu mới nhất của lượt gửi nhờ CongRaVao ghi (GhiViTriAsync).
/// </summary>
public interface ITimXeModule
{
    /// <summary>Lượt gửi XE MÁY vào trong [tu, den): bao nhiêu lượt có khu, do nguồn nào ghi lần đầu.</summary>
    Task<ThongKeDinhVi> LayThongKeDinhViAsync(DateTime tu, DateTime den, CancellationToken ct = default);
}

/// <param name="SoLuot">Lượt gửi (đã có ít nhất một lần ghi khu hoặc vào trong khoảng thời gian).</param>
/// <param name="SoCoKhu">Lượt có ít nhất một lần ghi khu.</param>
public record ThongKeDinhVi(int SoLuot, int SoCoKhu, int SoCameraGhi, int SoNguoiDungBao, int SoAdminGan)
{
    public decimal? TiLeDinhVi => SoLuot == 0 ? null : Math.Round(100m * SoCoKhu / SoLuot, 1);
}
