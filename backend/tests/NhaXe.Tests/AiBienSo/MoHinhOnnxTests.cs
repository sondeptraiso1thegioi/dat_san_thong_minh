using NhaXe.Modules.AiBienSo.Application;
using NhaXe.Modules.AiBienSo.Contracts;
using NhaXe.Modules.AiBienSo.Domain;
using NhaXe.Modules.AiBienSo.Infrastructure;
using NhaXe.SharedKernel;

namespace NhaXe.Tests.AiBienSo;

/// <summary>
/// Chạy THẬT hai mô hình đã train (Model A YOLOv8n + PaddleOCR) trên ảnh lấy từ tập TEST của Model A (Roboflow).
/// Tên file = biển số đúng. Đây là kiểm tra tích hợp: tiền xử lý / hậu xử lý C# phải khớp lúc train.
/// </summary>
public class MoHinhOnnxTests : IClassFixture<MoHinhOnnxTests.MoHinhChung>
{
    public class MoHinhChung : IDisposable
    {
        public string ThuMuc { get; } = Path.Combine(AppContext.BaseDirectory, "models");
        public MoHinhDocBienSoOnnx MoHinh { get; }
        public MoHinhChung() => MoHinh = new(new TuyChonMoHinh(ThuMuc));
        public void Dispose() => MoHinh.Dispose();
    }

    private readonly MoHinhChung _m;
    public MoHinhOnnxTests(MoHinhChung m) => _m = m;

    private static AnhBienSo DocFile(string ten) =>
        new(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "AiBienSo", "anh-mau", ten)), "image/jpeg");

    [Theory]
    [InlineData("oto-51B21666.jpg", "51B21666")]       // ô tô, biển dài 1 dòng
    [InlineData("oto-51G10096.jpg", "51G10096")]
    [InlineData("oto-51G01440.jpg", "51G01440")]
    [InlineData("xemay-59L154063.jpg", "59L154063")]   // xe máy, biển 2 dòng
    [InlineData("xemay-59C201909.jpg", "59C201909")]
    [InlineData("xemay-83C106271.jpg", "83C106271")]
    public async Task AnhMau_DocDungBienSo(string file, string dung)
    {
        var kq = await new AiBienSoModule(new DocBienSoUseCase(_m.MoHinh)).DocAsync(DocFile(file));
        Assert.Equal(dung, kq.BienSo);
        Assert.False(kq.CanXacNhan);
        Assert.InRange(kq.DoTinCay, QuyTacDocBienSo.NguongTinCay, 1);
    }

    [Fact]
    public async Task BienNghiengBiCheKhuat_KhongTuNhan()
    {
        // biển xoay ~45° và bị che một phần: OCR ra chuỗi sai định dạng → không đoán, bảo vệ xác nhận tay
        var kq = await new DocBienSoUseCase(_m.MoHinh).ThucHienAsync(DocFile("bien-nghieng-che-khuat.jpg"));
        Assert.Null(kq.BienSo); Assert.True(kq.CanXacNhan);
    }

    [Fact]
    public async Task AnhKhongCoBien_KhongDoan()
    {
        // ảnh xám trơn: Model A không thấy biển nào → không có ứng viên, phải xác nhận tay
        using var bmp = new SkiaSharp.SKBitmap(320, 240);
        bmp.Erase(new SkiaSharp.SKColor(128, 128, 128));
        var png = bmp.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100).ToArray();
        var kq = await new DocBienSoUseCase(_m.MoHinh).ThucHienAsync(new(png, "image/png"));
        Assert.Null(kq.BienSo); Assert.True(kq.CanXacNhan); Assert.Empty(kq.UngVien);
    }

    [Fact]
    public async Task AnhHong_BaoLoiRo()
    {
        byte[] hong = [255, 216, 255, 0, 1, 2, 3];
        var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => _m.MoHinh.DocAsync(new(hong, "image/jpeg"), default));
        Assert.Equal("ANH_KHONG_DOC_DUOC", ex.Ma);
    }

    [Fact]
    public async Task ThieuFileMoHinh_BaoAiChuaSanSang()
    {
        using var m = new MoHinhDocBienSoOnnx(new TuyChonMoHinh(Path.Combine(Path.GetTempPath(), "khong-co-models")));
        var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => m.DocAsync(DocFile("oto-51B21666.jpg"), default));
        Assert.Equal("AI_KHONG_SAN_SANG", ex.Ma);
    }

    /// <summary>File OCR có nhưng hỏng: báo AI chưa sẵn sàng (Model A đã nạp được giải phóng, không rò bộ nhớ).</summary>
    [Fact]
    public async Task FileOcrHong_BaoAiChuaSanSang()
    {
        var thuMuc = Path.Combine(Path.GetTempPath(), "models-hong-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(thuMuc);
        try
        {
            foreach (var f in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "models")))
                File.Copy(f, Path.Combine(thuMuc, Path.GetFileName(f)));
            File.WriteAllBytes(Path.Combine(thuMuc, "paddleocr_rec_bien_so.onnx"), [1, 2, 3, 4]);
            using var m = new MoHinhDocBienSoOnnx(new TuyChonMoHinh(thuMuc));
            for (var i = 0; i < 2; i++)
            {
                var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => m.DocAsync(DocFile("oto-51B21666.jpg"), default));
                Assert.Equal("AI_KHONG_SAN_SANG", ex.Ma);
            }
        }
        finally { Directory.Delete(thuMuc, true); }
    }

    /// <summary>Lần đầu thiếu file mô hình, chép file vào sau: lần gọi tiếp phải chạy được, không cần khởi động lại API.</summary>
    [Fact]
    public async Task ThieuMoHinhRoiChepVao_LanSauDocDuoc()
    {
        var thuMuc = Path.Combine(Path.GetTempPath(), "models-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(thuMuc);
        try
        {
            using var m = new MoHinhDocBienSoOnnx(new TuyChonMoHinh(thuMuc));
            var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => m.DocAsync(DocFile("oto-51B21666.jpg"), default));
            Assert.Equal("AI_KHONG_SAN_SANG", ex.Ma);

            foreach (var f in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "models")))
                File.Copy(f, Path.Combine(thuMuc, Path.GetFileName(f)));
            var kq = await m.DocAsync(DocFile("oto-51B21666.jpg"), default);
            Assert.Contains(kq, u => u.BienSo == "51B21666");
        }
        finally { Directory.Delete(thuMuc, true); }
    }
}

/// <summary>Các bước thuần (không cần mô hình): NMS, giải mã đầu ra YOLO, giải mã CTC, chọn 1 dòng / 2 dòng, co ảnh.</summary>
public class XuLyMoHinhTests
{
    [Fact]
    public void Nms_BoHopTrung_GiuHopDiemCao()
    {
        var ds = ModelATimBien.Nms([new(0, 0, 10, 10, .5f), new(1, 1, 11, 11, .9f), new(50, 50, 60, 60, .6f)], .45f);
        Assert.Equal([.9f, .6f], ds.Select(v => v.Diem));
    }

    [Fact]
    public void GiaiMaDauRa_DoiVeToaDoAnhGoc_VaLocNguong()
    {
        // 2 hộp (n = 2): hộp 0 điểm 0.9 tâm (320, 320) rộng 100 cao 50; hộp 1 điểm 0.1 bị loại
        float[] o = [320, 0, 320, 0, 100, 0, 50, 0, .9f, .1f];
        // ảnh gốc 1280x640 → tỉ lệ 0.5, viền trên 160
        var v = Assert.Single(ModelATimBien.GiaiMaDauRa(o, 2, .5f, 0, 160, 1280, 640, .4f, .45f));
        Assert.Equal(new VungBien(540, 270, 740, 370, .9f), v);
    }

    [Fact]
    public void GiaiMaCtc_BoKyTuTrongVaLapLienNhau()
    {
        string[] tu = ["", "3", "0", "A"];
        // các bước: 3 3 _ 0 _ 0 A  → "300A"
        int[] chiSo = [1, 1, 0, 2, 0, 2, 3];
        var xs = new float[chiSo.Length * 4];
        for (var t = 0; t < chiSo.Length; t++) xs[t * 4 + chiSo[t]] = 1f;
        var (chu, diem) = PaddleOcrDocDong.GiaiMaCtc(xs, chiSo.Length, 4, tu);
        Assert.Equal("300A", chu); Assert.Equal(1, diem, 3);
    }

    [Theory]
    [InlineData("51G31691", .99, "51C31631", .82, "51G31691")]   // cả hai hợp lệ → lấy điểm cao
    [InlineData("6092", .81, "51L30352", .98, "51L30352")]       // 1 dòng sai định dạng → lấy 2 dòng
    [InlineData("51G42861", .99, "H42060", .72, "51G42861")]
    public void ChonCachDoc_UuTienChuoiDungDinhDang(string mot, double d1, string hai, double d2, string mong)
        => Assert.Equal(mong, QuyTacDocBienSo.ChonCachDoc((mot, d1), (hai, d2))?.BienSo);

    [Fact]
    public void ChonCachDoc_CaHaiSai_KhongDoan() => Assert.Null(QuyTacDocBienSo.ChonCachDoc(("ABC", .9), ("12", .9)));

    [Fact]
    public void XoayTheoExif_Quay90()
    {
        // 2x1: [A][B] → quay 90° theo chiều kim đồng hồ (RightTop) thành 1x2: A ở trên, B ở dưới
        var anh = new AnhRgb(2, 1, [1, 1, 1, 2, 2, 2]).XoayTheoExif(SkiaSharp.SKEncodedOrigin.RightTop);
        Assert.Equal((1, 2), (anh.Rong, anh.Cao));
        Assert.Equal([1, 1, 1, 2, 2, 2], anh.DiemAnh);
    }

    [Fact]
    public void CatVaCo_GiuNguyenKhiCungKichThuoc()
    {
        var anh = new AnhRgb(2, 2, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);
        Assert.Equal(anh.DiemAnh, anh.CatVaCo(0, 0, 2, 2, 2, 2).DiemAnh);
        Assert.Equal([10, 11, 12], anh.Cat(1, 1, 1, 1).DiemAnh);
    }
}
