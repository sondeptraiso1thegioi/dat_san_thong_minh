using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace NhaXe.Modules.AiBienSo.Infrastructure;

/// <summary>Một vùng biển số Model A tìm thấy, toạ độ theo ảnh gốc.</summary>
public record VungBien(float X1, float Y1, float X2, float Y2, float Diem)
{
    public float Rong => X2 - X1;
    public float Cao => Y2 - Y1;
}

/// <summary>
/// Model A: YOLOv8n 1 lớp "bien_so" đã train trên Colab (best.onnx, đầu vào 1x3x640x640, đầu ra 1x5x8400).
/// Tiền xử lý giống Ultralytics: letterbox về 640 (viền xám 114), RGB, chia 255.
/// </summary>
public sealed class ModelATimBien : IDisposable
{
    public const int KichThuoc = 640;
    private readonly InferenceSession _phien;
    private readonly string _tenDauVao;

    public ModelATimBien(string duongDanOnnx, SessionOptions tuyChon)
    {
        _phien = new InferenceSession(duongDanOnnx, tuyChon);
        _tenDauVao = _phien.InputMetadata.Keys.First();
    }

    public IReadOnlyList<VungBien> TimBien(AnhRgb anh, float nguong = 0.4f, float nguongIou = 0.45f)
    {
        // 1. Letterbox: giữ tỉ lệ, đặt giữa khung 640x640
        var tl = Math.Min((float)KichThuoc / anh.Cao, (float)KichThuoc / anh.Rong);
        int rongMoi = (int)Math.Round(anh.Rong * tl), caoMoi = (int)Math.Round(anh.Cao * tl);
        int trai = (KichThuoc - rongMoi) / 2, tren = (KichThuoc - caoMoi) / 2;
        var co = anh.CatVaCo(0, 0, anh.Rong, anh.Cao, rongMoi, caoMoi);
        var t = new DenseTensor<float>([1, 3, KichThuoc, KichThuoc]);
        var buf = t.Buffer.Span;
        buf.Fill(114f / 255f);
        int mp = KichThuoc * KichThuoc;
        for (var y = 0; y < caoMoi; y++)
            for (var x = 0; x < rongMoi; x++)
            {
                int s = (y * rongMoi + x) * 3, d = (y + tren) * KichThuoc + x + trai;
                buf[d] = co.DiemAnh[s] / 255f; buf[mp + d] = co.DiemAnh[s + 1] / 255f; buf[2 * mp + d] = co.DiemAnh[s + 2] / 255f;
            }

        // 2. Chạy mô hình: đầu ra [1, 5, N] = cx, cy, w, h, điểm
        using var kq = _phien.Run([NamedOnnxValue.CreateFromTensor(_tenDauVao, t)]);
        var o = kq[0].AsTensor<float>();
        return GiaiMaDauRa(o.ToArray(), o.Dimensions[2], tl, trai, tren, anh.Rong, anh.Cao, nguong, nguongIou);
    }

    /// <summary>Đổi đầu ra YOLO về toạ độ ảnh gốc rồi lọc trùng (NMS). Tách riêng để unit test không cần mô hình.</summary>
    public static IReadOnlyList<VungBien> GiaiMaDauRa(float[] o, int n, float tl, int trai, int tren, int rong, int cao,
                                                     float nguong, float nguongIou)
    {
        var ds = new List<VungBien>();
        for (var i = 0; i < n; i++)
        {
            var diem = o[4 * n + i];
            if (diem < nguong) continue;
            float cx = o[i], cy = o[n + i], w = o[2 * n + i], h = o[3 * n + i];
            ds.Add(new VungBien(
                Math.Clamp((cx - w / 2 - trai) / tl, 0, rong), Math.Clamp((cy - h / 2 - tren) / tl, 0, cao),
                Math.Clamp((cx + w / 2 - trai) / tl, 0, rong), Math.Clamp((cy + h / 2 - tren) / tl, 0, cao), diem));
        }
        return Nms(ds, nguongIou);
    }

    public static IReadOnlyList<VungBien> Nms(IEnumerable<VungBien> ds, float nguongIou)
    {
        var giu = new List<VungBien>();
        foreach (var v in ds.OrderByDescending(v => v.Diem))
            if (giu.All(g => Iou(g, v) < nguongIou)) giu.Add(v);
        return giu;
    }

    public static float Iou(VungBien a, VungBien b)
    {
        var ix = Math.Max(0, Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1));
        var iy = Math.Max(0, Math.Min(a.Y2, b.Y2) - Math.Max(a.Y1, b.Y1));
        var giao = ix * iy;
        return giao / (a.Rong * a.Cao + b.Rong * b.Cao - giao + 1e-9f);
    }

    public void Dispose() => _phien.Dispose();
}
