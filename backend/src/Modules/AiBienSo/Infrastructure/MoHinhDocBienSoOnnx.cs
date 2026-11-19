using Microsoft.ML.OnnxRuntime;
using NhaXe.Modules.AiBienSo.Application;
using NhaXe.Modules.AiBienSo.Contracts;
using NhaXe.Modules.AiBienSo.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.AiBienSo.Infrastructure;

/// <param name="ThuMuc">Thư mục chứa 3 file mô hình (xem models/README.md).</param>
/// <param name="SoLuongDongThoi">Số ảnh được xử lý cùng lúc trên một instance API (giới hạn CPU).</param>
public record TuyChonMoHinh(string ThuMuc, float NguongModelA = 0.4f, int SoBienToiDa = 3, int SoLuongDongThoi = 2)
{
    public string ModelA => Path.Combine(ThuMuc, "model_a_yolov8n.onnx");
    public string Ocr => Path.Combine(ThuMuc, "paddleocr_rec_bien_so.onnx");
    public string TuDien => Path.Combine(ThuMuc, "dict_bien_so.txt");
}

/// <summary>
/// Pipeline hai giai đoạn của phương án 3 (phần cổng):
/// 1) Model A YOLOv8n tìm vùng biển trên ảnh camera;
/// 2) PaddleOCR đọc chữ trên vùng đã cắt. Mỗi vùng đọc thử 1 dòng và 2 dòng (tách đôi theo chiều cao),
/// giữ cách cho chuỗi đúng định dạng biển VN.
/// Hai mô hình nạp một lần (singleton, lười: chỉ nạp khi có ảnh đầu tiên), InferenceSession an toàn khi gọi song song.
/// </summary>
public sealed class MoHinhDocBienSoOnnx(TuyChonMoHinh tuyChon) : IMoHinhDocBienSo, IDisposable
{
    // Không dùng Lazy<T>: Lazy nhớ cả LỖI lần đầu, chép file mô hình vào sau vẫn báo lỗi tới khi khởi động lại.
    // Ở đây chỉ nhớ khi nạp THÀNH CÔNG; lỗi thì lần sau thử nạp lại.
    private (ModelATimBien A, PaddleOcrDocDong Ocr)? _moHinh;
    private readonly object _khoaNap = new();
    private (ModelATimBien A, PaddleOcrDocDong Ocr) LayMoHinh()
    {
        lock (_khoaNap) return _moHinh ??= Nap(tuyChon); // khoá rất ngắn khi đã nạp; tránh đọc struct dở dang giữa 2 luồng
    }
    private readonly SemaphoreSlim _gioiHan = new(tuyChon.SoLuongDongThoi);

    private static (ModelATimBien, PaddleOcrDocDong) Nap(TuyChonMoHinh t)
    {
        foreach (var f in new[] { t.ModelA, t.Ocr, t.TuDien })
            if (!File.Exists(f)) throw new FileNotFoundException($"Thiếu file mô hình: {f}");
        using var so = new SessionOptions { IntraOpNumThreads = 2, GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
        var a = new ModelATimBien(t.ModelA, so);
        try { return (a, new PaddleOcrDocDong(t.Ocr, t.TuDien, so)); }
        catch { a.Dispose(); throw; } // file OCR hỏng: giải phóng Model A vừa nạp, lần sau nạp lại không bị rò bộ nhớ
    }

    public async Task<IReadOnlyList<BienSoUngVien>> DocAsync(AnhBienSo anh, CancellationToken ct)
    {
        var img = AnhRgb.GiaiMa(anh.NoiDung);
        await _gioiHan.WaitAsync(ct);
        try
        {
            (ModelATimBien A, PaddleOcrDocDong Ocr) m;
            try { m = LayMoHinh(); }
            catch (Exception ex) when (ex is FileNotFoundException or OnnxRuntimeException)
            { throw new LoiNghiepVu(LoaiLoi.XungDot, "AI_KHONG_SAN_SANG", "Chưa nạp được mô hình nhận diện biển số. Kiểm tra thư mục models."); }
            return await Task.Run(() => Doc(m.A, m.Ocr, img, ct), ct);
        }
        finally { _gioiHan.Release(); }
    }

    private List<BienSoUngVien> Doc(ModelATimBien a, PaddleOcrDocDong ocr, AnhRgb img, CancellationToken ct)
    {
        var kq = new List<BienSoUngVien>();
        foreach (var v in a.TimBien(img, tuyChon.NguongModelA).Take(tuyChon.SoBienToiDa))
        {
            ct.ThrowIfCancellationRequested();
            // Cắt sát hộp Model A (giống ảnh biển đã cắt dùng để train OCR); nới thêm làm OCR đọc sai rõ rệt
            int x = (int)v.X1, y = (int)v.Y1, w = (int)Math.Ceiling(v.Rong), h = (int)Math.Ceiling(v.Cao);
            if (w < 8 || h < 8) continue;
            var vung = img.Cat(x, y, w, h);
            var motDong = ocr.Doc(vung);
            var tach = vung.Cao / 2; // tách đôi đúng giữa, giống cách cắt dòng lúc train OCR
            var tren = ocr.Doc(vung.Cat(0, 0, vung.Rong, tach));
            var duoi = ocr.Doc(vung.Cat(0, tach, vung.Rong, vung.Cao - tach));
            var haiDong = (tren.Chu + duoi.Chu, Math.Min(tren.Diem, duoi.Diem));
            if (QuyTacDocBienSo.ChonCachDoc(motDong, haiDong) is { } chon)
                kq.Add(new(chon.BienSo, chon.DoTinCay,
                    new KhungBien(Math.Round((double)x / img.Rong, 4), Math.Round((double)y / img.Cao, 4),
                                  Math.Round((double)w / img.Rong, 4), Math.Round((double)h / img.Cao, 4))));
        }
        return kq;
    }

    public void Dispose()
    {
        lock (_khoaNap) if (_moHinh is { } m) { m.A.Dispose(); m.Ocr.Dispose(); _moHinh = null; }
        _gioiHan.Dispose();
    }
}
