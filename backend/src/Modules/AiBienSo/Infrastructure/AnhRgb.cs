using NhaXe.SharedKernel;
using SkiaSharp;

namespace NhaXe.Modules.AiBienSo.Infrastructure;

/// <summary>Ảnh RGB 8 bit, điểm ảnh xếp liền theo hàng (R,G,B,R,G,B...). Kiểu dữ liệu chung giữa giải mã ảnh và hai mô hình.</summary>
public sealed class AnhRgb
{
    public int Rong { get; }
    public int Cao { get; }
    public byte[] DiemAnh { get; }

    public AnhRgb(int rong, int cao, byte[] diemAnh)
    {
        if (rong <= 0 || cao <= 0 || diemAnh.Length != rong * cao * 3) throw new ArgumentException("Kích thước ảnh không khớp dữ liệu.");
        Rong = rong; Cao = cao; DiemAnh = diemAnh;
    }

    public const int SoDiemToiDa = 12_000_000; // chặn ảnh "bom giải nén"

    /// <summary>Giải mã JPEG/PNG, xoay theo EXIF (ảnh chụp điện thoại).</summary>
    public static AnhRgb GiaiMa(byte[] noiDung)
    {
        using var codec = SKCodec.Create(new MemoryStream(noiDung, writable: false));
        if (codec is null) throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "ANH_KHONG_DOC_DUOC", "Ảnh hỏng hoặc không đọc được.");
        // Đọc kích thước từ phần đầu file TRƯỚC khi giải mã toàn bộ
        if ((long)codec.Info.Width * codec.Info.Height > SoDiemToiDa)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "ANH_QUA_LON", "Ảnh quá 12 megapixel.");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        var kq = codec.GetPixels(info, bmp.GetPixels());
        if (kq is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "ANH_KHONG_DOC_DUOC", "Ảnh hỏng hoặc không đọc được.");
        var rgba = bmp.GetPixelSpan();
        var rgb = new byte[info.Width * info.Height * 3];
        for (int i = 0, j = 0; j < rgb.Length; i += 4, j += 3) { rgb[j] = rgba[i]; rgb[j + 1] = rgba[i + 1]; rgb[j + 2] = rgba[i + 2]; }
        return new AnhRgb(info.Width, info.Height, rgb).XoayTheoExif(codec.EncodedOrigin);
    }

    /// <summary>Đưa ảnh về chiều đứng thật theo thẻ EXIF Orientation.</summary>
    public AnhRgb XoayTheoExif(SKEncodedOrigin goc)
    {
        if (goc == SKEncodedOrigin.TopLeft) return this;
        var doiChieu = goc is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int rong = doiChieu ? Cao : Rong, cao = doiChieu ? Rong : Cao;
        var dst = new byte[DiemAnh.Length];
        for (var y = 0; y < cao; y++)
            for (var x = 0; x < rong; x++)
            {
                // (x, y) trên ảnh mới lấy từ (sx, sy) trên ảnh cũ
                var (sx, sy) = goc switch
                {
                    SKEncodedOrigin.TopRight => (Rong - 1 - x, y),
                    SKEncodedOrigin.BottomRight => (Rong - 1 - x, Cao - 1 - y),
                    SKEncodedOrigin.BottomLeft => (x, Cao - 1 - y),
                    SKEncodedOrigin.LeftTop => (y, x),
                    SKEncodedOrigin.RightTop => (y, Cao - 1 - x),
                    SKEncodedOrigin.RightBottom => (Rong - 1 - y, Cao - 1 - x),
                    SKEncodedOrigin.LeftBottom => (Rong - 1 - y, x),
                    _ => (x, y)
                };
                Buffer.BlockCopy(DiemAnh, (sy * Rong + sx) * 3, dst, (y * rong + x) * 3, 3);
            }
        return new AnhRgb(rong, cao, dst);
    }

    /// <summary>
    /// Cắt vùng (x, y, w, h) rồi co/giãn về (rongMoi, caoMoi) bằng nội suy song tuyến,
    /// cùng cách tính tâm điểm ảnh với cv2.INTER_LINEAR mà lúc train dùng.
    /// </summary>
    public AnhRgb CatVaCo(int x, int y, int w, int h, int rongMoi, int caoMoi)
    {
        x = Math.Clamp(x, 0, Rong - 1); y = Math.Clamp(y, 0, Cao - 1);
        w = Math.Clamp(w, 1, Rong - x); h = Math.Clamp(h, 1, Cao - y);
        var dst = new byte[rongMoi * caoMoi * 3];
        double tlX = (double)w / rongMoi, tlY = (double)h / caoMoi;
        for (var j = 0; j < caoMoi; j++)
        {
            var sy = Math.Clamp((j + 0.5) * tlY - 0.5, 0, h - 1);
            int y0 = (int)sy, y1 = Math.Min(y0 + 1, h - 1); var fy = sy - y0;
            for (var i = 0; i < rongMoi; i++)
            {
                var sx = Math.Clamp((i + 0.5) * tlX - 0.5, 0, w - 1);
                int x0 = (int)sx, x1 = Math.Min(x0 + 1, w - 1); var fx = sx - x0;
                int p00 = ((y + y0) * Rong + x + x0) * 3, p01 = ((y + y0) * Rong + x + x1) * 3;
                int p10 = ((y + y1) * Rong + x + x0) * 3, p11 = ((y + y1) * Rong + x + x1) * 3;
                var d = (j * rongMoi + i) * 3;
                for (var c = 0; c < 3; c++)
                {
                    var v = (DiemAnh[p00 + c] * (1 - fx) + DiemAnh[p01 + c] * fx) * (1 - fy)
                          + (DiemAnh[p10 + c] * (1 - fx) + DiemAnh[p11 + c] * fx) * fy;
                    dst[d + c] = (byte)Math.Clamp((int)Math.Round(v), 0, 255);
                }
            }
        }
        return new AnhRgb(rongMoi, caoMoi, dst);
    }

    public AnhRgb Cat(int x, int y, int w, int h)
    {
        x = Math.Clamp(x, 0, Rong - 1); y = Math.Clamp(y, 0, Cao - 1);
        w = Math.Clamp(w, 1, Rong - x); h = Math.Clamp(h, 1, Cao - y);
        var dst = new byte[w * h * 3];
        for (var j = 0; j < h; j++) Buffer.BlockCopy(DiemAnh, ((y + j) * Rong + x) * 3, dst, j * w * 3, w * 3);
        return new AnhRgb(w, h, dst);
    }
}
