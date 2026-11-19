namespace NhaXe.Modules.ThongBao.Application;

public class ThongBaoDto
{
    public int Id { get; set; }
    public string Loai { get; set; } = "";
    public string NoiDung { get; set; } = "";
    public DateTime GuiLuc { get; set; }
    public bool DaDoc { get; set; }
    /// <summary>Việc cần làm / chỗ cần xem (HanhDongThongBao). null: thông báo cũ, frontend suy theo loai.</summary>
    public string? HanhDong { get; set; }
    public int? ThamChieuId { get; set; }
}

public record HopThuDto(int SoChuaDoc, IReadOnlyList<ThongBaoDto> ThongBao);
