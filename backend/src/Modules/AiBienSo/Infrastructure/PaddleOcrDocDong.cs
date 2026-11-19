using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace NhaXe.Modules.AiBienSo.Infrastructure;

/// <summary>
/// PaddleOCR PP-OCRv5 mobile rec đã fine-tune trên biển số VN (Kaggle), chuyển sang ONNX bằng paddle2onnx.
/// Đọc MỘT dòng chữ. Tiền xử lý giống PaddleOCR: cao 48, rộng giữ tỉ lệ (tối thiểu khung 320), BGR, (x/255 - 0.5)/0.5,
/// đệm 0 bên phải. Hậu xử lý CTC: bỏ ký tự trống (chỉ số 0) và ký tự lặp liền nhau.
/// </summary>
public sealed class PaddleOcrDocDong : IDisposable
{
    public const int Cao = 48;
    public const int RongToiThieu = 320;
    private readonly InferenceSession _phien;
    private readonly string _tenDauVao;
    private readonly string[] _tuDien; // tuDien[0] = ký tự trống của CTC

    public PaddleOcrDocDong(string duongDanOnnx, string duongDanTuDien, SessionOptions tuyChon)
    {
        _phien = new InferenceSession(duongDanOnnx, tuyChon);
        _tenDauVao = _phien.InputMetadata.Keys.First();
        _tuDien = ["", .. File.ReadAllLines(duongDanTuDien).Where(l => l.Length > 0)];
    }

    public (string Chu, double Diem) Doc(AnhRgb dong)
    {
        var tiLe = (double)dong.Rong / dong.Cao;
        var rongKhung = (int)(Cao * Math.Max((double)RongToiThieu / Cao, tiLe));
        var rong = Math.Min(rongKhung, (int)Math.Ceiling(Cao * tiLe));
        var co = dong.CatVaCo(0, 0, dong.Rong, dong.Cao, rong, Cao);
        var t = new DenseTensor<float>([1, 3, Cao, rongKhung]); // mặc định 0 = phần đệm
        var buf = t.Buffer.Span;
        int mp = Cao * rongKhung;
        for (var y = 0; y < Cao; y++)
            for (var x = 0; x < rong; x++)
            {
                int s = (y * rong + x) * 3, d = y * rongKhung + x;
                // ảnh lưu RGB, mô hình train với BGR
                buf[d] = co.DiemAnh[s + 2] / 127.5f - 1f; buf[mp + d] = co.DiemAnh[s + 1] / 127.5f - 1f; buf[2 * mp + d] = co.DiemAnh[s] / 127.5f - 1f;
            }
        using var kq = _phien.Run([NamedOnnxValue.CreateFromTensor(_tenDauVao, t)]);
        var o = kq[0].AsTensor<float>(); // [1, T, số lớp]
        return GiaiMaCtc(o.ToArray(), o.Dimensions[1], o.Dimensions[2], _tuDien);
    }

    /// <summary>Giải mã CTC tham lam. Điểm = trung bình xác suất các ký tự giữ lại.</summary>
    public static (string Chu, double Diem) GiaiMaCtc(float[] xs, int soBuoc, int soLop, IReadOnlyList<string> tuDien)
    {
        var chu = new System.Text.StringBuilder(); double tong = 0; int dem = 0, truoc = 0;
        for (var t = 0; t < soBuoc; t++)
        {
            int best = 0; var p = xs[t * soLop];
            for (var c = 1; c < soLop; c++) if (xs[t * soLop + c] > p) { p = xs[t * soLop + c]; best = c; }
            if (best != 0 && best != truoc && best < tuDien.Count) { chu.Append(tuDien[best]); tong += p; dem++; }
            truoc = best;
        }
        return (chu.ToString(), dem == 0 ? 0 : tong / dem);
    }

    public void Dispose() => _phien.Dispose();
}
