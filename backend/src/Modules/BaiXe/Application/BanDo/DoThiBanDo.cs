using System.Text.Json;
using NhaXe.Modules.BaiXe.Domain;

namespace NhaXe.Modules.BaiXe.Application;

/// <summary>
/// Đồ thị điểm mốc của bãi đọc từ do-thi-ban-do.json (nhúng trong DLL). Toạ độ theo tỉ lệ ảnh (0..1) nên frontend vẽ đè lên ảnh
/// ở mọi cỡ màn hình. Độ dài cạnh tự tính từ toạ độ: dời một điểm không phải sửa số mét. Thay hai bảng nut_duong_di / canh_duong_di
/// (toạ độ của sơ đồ mẫu cũ). Dijkstra vẫn là DuongNganNhat: một bản thuật toán, dùng cho cả chọn khu gần cổng và vẽ đường.
/// </summary>
public sealed class DoThiBanDo
{
    public record Nut(int Id, string Ma, string? Ten, double X, double Y, string? MaKhu);

    public IReadOnlyList<Nut> CacNut { get; }
    public IReadOnlyList<(string A, string B)> CacCanh { get; }
    public double RongAnh { get; }
    public double CaoAnh { get; }
    public double MetMoiPx { get; }

    private DoThiBanDo(IReadOnlyList<Nut> nut, IReadOnlyList<(string, string)> canh, double rong, double cao, double met)
    { CacNut = nut; CacCanh = canh; RongAnh = rong; CaoAnh = cao; MetMoiPx = met; }

    private record Json(JsonAnh Anh, List<JsonNut> Nut, List<List<string>> Canh);
    private record JsonAnh(double Rong, double Cao, double MetMoiPx);
    private record JsonNut(string Ma, string? Ten, double X, double Y, string? Khu);

    /// <summary>Đọc và kiểm tra JSON: mã nút không trùng, cạnh trỏ tới nút có thật, toạ độ trong 0..1.</summary>
    public static DoThiBanDo Doc(string json)
    {
        var j = JsonSerializer.Deserialize<Json>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Đồ thị bản đồ rỗng.");
        if (j.Nut is not { Count: > 0 }) throw new InvalidOperationException("Đồ thị bản đồ chưa có nút nào.");
        var nut = j.Nut.Select((n, i) =>
        {
            if (n.X is < 0 or > 1 || n.Y is < 0 or > 1) throw new InvalidOperationException($"Nút {n.Ma}: x, y phải trong 0..1.");
            return new Nut(i + 1, n.Ma, string.IsNullOrWhiteSpace(n.Ten) ? null : n.Ten, n.X, n.Y,
                           string.IsNullOrWhiteSpace(n.Khu) ? null : n.Khu.Trim().ToUpperInvariant());
        }).ToList();
        var ma = nut.Select(n => n.Ma).ToList();
        if (ma.Distinct().Count() != ma.Count) throw new InvalidOperationException("Đồ thị bản đồ có mã nút trùng.");
        var canh = (j.Canh ?? new()).Select(c =>
        {
            if (c.Count != 2 || !ma.Contains(c[0]) || !ma.Contains(c[1]))
                throw new InvalidOperationException($"Cạnh [{string.Join(", ", c)}] trỏ tới nút không có.");
            return (c[0], c[1]);
        }).ToList();
        return new DoThiBanDo(nut, canh, j.Anh.Rong, j.Anh.Cao, j.Anh.MetMoiPx);
    }

    private static readonly Lazy<DoThiBanDo> MacDinhLazy = new(() =>
    {
        var asm = typeof(DoThiBanDo).Assembly;
        var ten = asm.GetManifestResourceNames().Single(n => n.EndsWith("do-thi-ban-do.json", StringComparison.Ordinal));
        using var s = asm.GetManifestResourceStream(ten)!;
        using var r = new StreamReader(s);
        return Doc(r.ReadToEnd());
    });

    /// <summary>Đồ thị của bãi (file do-thi-ban-do.json nhúng trong DLL), đọc một lần.</summary>
    public static DoThiBanDo MacDinh => MacDinhLazy.Value;

    /// <summary>Độ dài (mét) giữa hai nút: khoảng cách theo pixel ảnh nhân MetMoiPx.</summary>
    public double DoDai(Nut a, Nut b) => Math.Round(Math.Sqrt(Math.Pow((a.X - b.X) * RongAnh, 2) + Math.Pow((a.Y - b.Y) * CaoAnh, 2)) * MetMoiPx, 2);

    /// <summary>Nút và cạnh dạng BaiXe dùng (NutRow, DuongNganNhat.Canh). khuTheoMa: mã sơ đồ (R, S, HAM) → id khu trong DB.</summary>
    public (IReadOnlyList<NutRow> Nut, IReadOnlyList<DuongNganNhat.Canh> Canh) ChoBaiXe(IReadOnlyDictionary<string, int> khuTheoMa)
    {
        var theoMa = CacNut.ToDictionary(n => n.Ma);
        var nut = CacNut.Select(n => new NutRow(n.Id, n.Ten ?? "", n.X, n.Y,
            n.MaKhu is not null && khuTheoMa.TryGetValue(n.MaKhu, out var k) ? k : null)).ToList();
        var canh = CacCanh.Select(c => new DuongNganNhat.Canh(theoMa[c.A].Id, theoMa[c.B].Id, DoDai(theoMa[c.A], theoMa[c.B]))).ToList();
        return (nut, canh);
    }
}
