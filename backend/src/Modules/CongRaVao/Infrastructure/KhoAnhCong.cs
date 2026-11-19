using Microsoft.Extensions.Logging;
using NhaXe.Modules.CongRaVao.Application;

namespace NhaXe.Modules.CongRaVao.Infrastructure;

/// <summary>
/// Lưu ảnh đối chứng ở cổng vào thư mục (Docker: volume "uploads" dùng chung cho cả hai container API).
/// Khoá do server đặt: cong/yyyyMMdd/{guid}.jpg, không dùng tên tệp người gửi.
/// </summary>
public sealed class KhoAnhCongTrenDia(string thuMucGoc) : IKhoAnhCong
{
    private readonly string _goc = Path.GetFullPath(thuMucGoc);

    public async Task<string> LuuAsync(byte[] anh, string loaiAnh, DateTime luc, CancellationToken ct)
    {
        var duoi = loaiAnh == "image/png" ? "png" : "jpg";
        var khoa = $"cong/{luc:yyyyMMdd}/{Guid.NewGuid():N}.{duoi}";
        var p = DuongDan(khoa);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        await File.WriteAllBytesAsync(p, anh, ct);
        return khoa;
    }

    public Task<Stream?> MoAsync(string khoa, CancellationToken ct)
    {
        var p = DuongDan(khoa);
        return Task.FromResult<Stream?>(File.Exists(p) ? File.OpenRead(p) : null);
    }

    /// <summary>Chặn khoá kiểu "../": chỉ nhận đúng dạng do LuuAsync tạo và phải nằm trong thư mục gốc.</summary>
    private string DuongDan(string khoa)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(khoa, "^cong/[0-9]{8}/[a-f0-9]{32}\\.(jpg|png)$"))
            throw new ArgumentException("Khoá ảnh không hợp lệ.", nameof(khoa));
        var p = Path.GetFullPath(Path.Combine(_goc, khoa));
        if (!p.StartsWith(_goc + Path.DirectorySeparatorChar)) throw new ArgumentException("Khoá ảnh không hợp lệ.", nameof(khoa));
        return p;
    }
}

/// <summary>Barie giả lập: chưa có thiết bị thật nên chỉ ghi log. Có thiết bị thì viết lớp mới gọi API / cổng COM của barie.</summary>
public sealed class BarieGiaLap(ILogger<BarieGiaLap> log) : IBarie
{
    public Task MoAsync(string cong, int luotGuiId, CancellationToken ct)
    {
        log.LogInformation("BARIE cổng {Cong} MỞ cho lượt gửi {LuotGuiId}", cong, luotGuiId);
        return Task.CompletedTask;
    }
}
