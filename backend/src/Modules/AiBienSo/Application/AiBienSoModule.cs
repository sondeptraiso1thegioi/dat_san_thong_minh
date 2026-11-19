using NhaXe.Modules.AiBienSo.Contracts;
using NhaXe.Modules.AiBienSo.Domain;
namespace NhaXe.Modules.AiBienSo.Application;

/// <summary>
/// Cổng tới các mô hình AI (DIP). Bản cài đặt thật: Model A YOLOv8n tìm biển + PaddleOCR (PP-OCRv5 rec) đọc chữ,
/// chạy bằng ONNX Runtime ngay trong tiến trình .NET (Infrastructure/MoHinhDocBienSoOnnx).
/// Trả về các chuỗi đọc được (chưa lọc) kèm điểm; use case lo phần luật nghiệp vụ.
/// </summary>
public interface IMoHinhDocBienSo
{
    Task<IReadOnlyList<BienSoUngVien>> DocAsync(AnhBienSo anh, CancellationToken ct);
}

public class DocBienSoUseCase(IMoHinhDocBienSo moHinh)
{
    public async Task<KetQuaDocBienSo> ThucHienAsync(AnhBienSo anh, CancellationToken ct = default)
    {
        QuyTacDocBienSo.KiemTraAnh(anh.NoiDung, anh.LoaiNoiDung);
        var ds = (await moHinh.DocAsync(anh, ct))
            .Select(x => new BienSoUngVien(QuyTacDocBienSo.ChuanHoa(x.BienSo) ?? "", x.DoTinCay, x.Khung))
            .Where(x => x.BienSo.Length > 0 && double.IsFinite(x.DoTinCay) && x.DoTinCay is >= 0 and <= 1)
            .GroupBy(x => x.BienSo).Select(g => g.MaxBy(x => x.DoTinCay)!)
            .OrderByDescending(x => x.DoTinCay).Take(10).ToArray();
        // Chỉ tự nhận khi đúng MỘT biển và đủ tin cậy; còn lại để người xác nhận (không đoán)
        var canXacNhan = ds.Length != 1 || ds[0].DoTinCay < QuyTacDocBienSo.NguongTinCay;
        return new(canXacNhan ? null : ds[0].BienSo, ds.FirstOrDefault()?.DoTinCay ?? 0, canXacNhan, ds,
                   QuyTacDocBienSo.LoaiBien(ds.FirstOrDefault()?.BienSo));
    }
}

public class AiBienSoModule(DocBienSoUseCase useCase) : IAiBienSoModule
{
    public Task<KetQuaDocBienSo> DocAsync(AnhBienSo anh, CancellationToken ct = default) => useCase.ThucHienAsync(anh, ct);
}
