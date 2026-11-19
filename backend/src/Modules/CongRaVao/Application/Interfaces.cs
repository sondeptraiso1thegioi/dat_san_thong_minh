using Microsoft.Extensions.Logging;
using NhaXe.Modules.CongRaVao.Domain;

using NhaXe.Modules.AiBienSo.Contracts;

namespace NhaXe.Modules.CongRaVao.Application;

// Các "cổng ra ngoài" (port) mà use case của CongRaVao cần. Application chỉ khai báo interface;
// Infrastructure cài đặt bằng EF Core / SQL Server; test cài đặt bằng bản giả trong bộ nhớ (DIP).

/// <summary>Phía GHI: lượt gửi, yêu cầu xác nhận, nhật ký cổng. Một lần LuuAsync = một transaction.</summary>
public interface ICongRaVaoRepository
{
    Task<LuotGui?> TimLuotAsync(int id, CancellationToken ct);
    Task<LuotGui?> TimLuotMoCuaXeAsync(int xeId, CancellationToken ct);
    Task<YeuCauXacNhan?> TimYeuCauAsync(int id, CancellationToken ct);
    Task<YeuCauXacNhan?> TimYeuCauDangChoAsync(int luotGuiId, CancellationToken ct);
    /// <summary>Id các yêu cầu còn CHO mà đã quá hạn (tác vụ nền xử lý).</summary>
    Task<IReadOnlyList<int>> LayIdYeuCauQuaHanAsync(DateTime bayGio, CancellationToken ct);

    void Them(LuotGui luot);
    void Them(YeuCauXacNhan yeuCau);
    void Them(SuKienCong suKien);

    /// <summary>Đã có nhật ký cổng (loai) của biển này từ thời điểm "tu"? Dùng chống đọc lặp ở chế độ tự quét.</summary>
    Task<bool> CoSuKienTuAsync(string loai, string bienSo, DateTime tu, CancellationToken ct) => Task.FromResult(false);

    /// <summary>
    /// Lưu mọi thay đổi trong một transaction. Lỗi có thể ném:
    /// YEU_CAU_DA_THAY_DOI (409): người khác vừa sửa cùng yêu cầu (ROWVERSION khác) → use case đọc lại và thử lại.
    /// XE_DA_TRONG_BAI (409): hai lần quét vào cùng lúc cho một xe (chỉ mục UX_lg_mot_luot_mo chặn).
    /// </summary>
    Task LuuAsync(CancellationToken ct);

    /// <summary>Bỏ mọi thực thể đang theo dõi, để lần thử lại đọc dữ liệu mới từ DB.</summary>
    void BoTheoDoi();
}

/// <summary>Phía ĐỌC lượt gửi đang mở (dùng cho cổng công khai ICongRaVaoModule).</summary>
public interface ILuotGuiQuery
{
    Task<IReadOnlyList<Contracts.LuotGuiDangMo>> LayDangMoAsync(IReadOnlyCollection<int>? xeIds, CancellationToken ct);
}

/// <summary>Phía ĐỌC cho màn hình cổng và trang Ra vào của khách (không theo dõi thay đổi).</summary>
public interface ICongQuery
{
    /// <summary>Yêu cầu đang chờ, kèm xe của lượt gửi. xeIds = null: mọi xe.</summary>
    Task<IReadOnlyList<(YeuCauXacNhan YeuCau, int XeId)>> LayYeuCauDangChoAsync(IReadOnlyCollection<int>? xeIds, CancellationToken ct);
    Task<IReadOnlyList<SuKienCong>> LaySuKienGanDayAsync(int soDong, CancellationToken ct);
    Task<IReadOnlyList<LuotGui>> LayLuotCuaXeAsync(IReadOnlyCollection<int> xeIds, int soDong, CancellationToken ct);
    /// <summary>Yêu cầu xác nhận ra gần nhất của từng lượt (trang Ra vào báo "đã hết hạn lúc …" khi mở từ thông báo cũ).</summary>
    Task<IReadOnlyDictionary<int, YeuCauXacNhan>> LayYeuCauGanNhatAsync(IReadOnlyCollection<int> luotIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<int, YeuCauXacNhan>>(new Dictionary<int, YeuCauXacNhan>());
    /// <summary>Lượt gửi đang mở cần Admin xử lý: nghi mất, chờ thanh toán, hoặc yêu cầu ra gần nhất đã hết hạn.</summary>
    Task<IReadOnlyList<(LuotGui Luot, string LyDo)>> LayLuotCanXuLyAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<(LuotGui, string)>>(Array.Empty<(LuotGui, string)>());
    /// <summary>Đếm nhật ký cổng trong khoảng thời gian (module BaoCao hỏi qua ICongRaVaoModule).</summary>
    Task<Contracts.ThongKeCong> LayThongKeAsync(DateTime tu, DateTime den, CancellationToken ct) =>
        Task.FromResult(new Contracts.ThongKeCong(0, 0, 0, 0, 0, 0, null));
}

/// <summary>Đọc số giây chờ xác nhận từ cau_hinh.thoi_gian_cho_xac_nhan_giay (mặc định 60).</summary>
public interface ICauHinhCong
{
    Task<int> LaySoGiayChoXacNhanAsync(CancellationToken ct);
    /// <summary>cau_hinh.giay_gioi_han_ai (mặc định 3): AI quá thời gian thì cổng chuyển chế độ thủ công.</summary>
    Task<int> LayGiayGioiHanAiAsync(CancellationToken ct) => Task.FromResult(3);
}

/// <summary>Kho ảnh đối chứng chụp ở cổng (trong Docker: volume uploads dùng chung cho 2 API). Sau này đổi sang MinIO chỉ thay cài đặt.</summary>
public interface IKhoAnhCong
{
    /// <summary>Lưu ảnh, trả về khoá (vd cong/20261118/3f2a....jpg) để ghi vào luot_gui.</summary>
    Task<string> LuuAsync(byte[] anh, string loaiAnh, DateTime luc, CancellationToken ct);
    Task<Stream?> MoAsync(string khoa, CancellationToken ct);
}

/// <summary>
/// Lệnh mở barie. Chưa có thiết bị thật: bản giả lập ghi log. Barie mở khi lượt gửi được mở (xe vào) hoặc đóng (xe ra),
/// nghe qua sự kiện XeDaVao / XeDaRa nên không use case nào phải biết tới barie.
/// </summary>
public interface IBarie
{
    Task MoAsync(string cong, int luotGuiId, CancellationToken ct);
}

// ------------------------- Nhận diện biển số -------------------------

/// <summary>
/// Xe tới cổng: ảnh camera (JPEG/PNG) và/hoặc biển số Admin gõ.
/// Có biển gõ tay thì dùng biển gõ tay (Admin đã nhìn tận mắt, UC-A08); không có thì để AI đọc ảnh.
/// AdminId: người đang đứng màn hình cổng (ghi vào nhật ký khi Admin xác nhận / sửa biển số).
/// </summary>
/// TuDong: khung hình do màn hình camera cổng tự chụp mỗi giây. Không thấy biển, hoặc biển vừa xử lý trong 30 giây,
/// thì bỏ qua: không ghi nhật ký, không báo (tránh mỗi giây một dòng "từ chối").
public record AnhCong(string? BienSoGoTay, byte[]? Anh = null, string? LoaiAnh = null, int? AdminId = null, bool TuDong = false);

/// <summary>
/// Kết quả đọc biển. BienSo = null khi không đọc được hoặc AI không chắc (dưới ngưỡng 0,85, nhiều biển);
/// khi đó UngVien là các biển AI đoán để Admin chọn. LoaiBien: O_TO / XE_MAY / KHONG_RO.
/// CheDoThuCong = true: mô hình lỗi hoặc quá thời gian (3 giây), màn hình cổng yêu cầu Admin nhập biển số.
/// </summary>
public record KetQuaNhanDien(string? BienSo, decimal? DoTinCay, string? LyDo = null, IReadOnlyList<string>? UngVien = null,
                             string LoaiBien = "KHONG_RO", bool GoTay = false, bool CheDoThuCong = false, KhungBien? Khung = null);

/// <summary>
/// Cổng tới việc đọc biển số. Use case chỉ biết interface này (DIP), không biết có AI hay không.
/// Cài đặt: NhanDienBangAi gọi module AiBienSo (Model A YOLOv8n + PaddleOCR) qua Contracts.
/// </summary>
public interface INhanDienBienSo
{
    Task<KetQuaNhanDien> DocAsync(AnhCong anh, CancellationToken ct);
}

/// <summary>
/// Đọc biển ở cổng: biển gõ tay (nếu có) được ưu tiên, độ tin cậy 1; nếu không thì gửi ảnh cho AI.
/// AI chỉ được tự nhận khi đọc ra đúng MỘT biển đủ tin cậy; còn lại trả null kèm ứng viên để Admin chọn (không đoán).
/// AI lỗi hoặc chạy quá giay_gioi_han_ai giây: chế độ thủ công (đề cương v10), không làm hỏng cổng.
/// </summary>
public class NhanDienBangAi(IAiBienSoModule ai, ICauHinhCong? cauHinh = null, Microsoft.Extensions.Logging.ILogger<NhanDienBangAi>? log = null)
    : INhanDienBienSo
{
    public const string LyDoThuCong = "AI không phản hồi kịp hoặc bị lỗi: chế độ thủ công, Admin nhập biển số";

    public async Task<KetQuaNhanDien> DocAsync(AnhCong anh, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(anh.BienSoGoTay)) return new(anh.BienSoGoTay.Trim(), 1m, GoTay: true);
        if (anh.Anh is null || anh.LoaiAnh is null) return new(null, null); // không ảnh, không gõ: "Không đọc được biển số"

        var giay = cauHinh is null ? 3 : await cauHinh.LayGiayGioiHanAiAsync(ct);
        KetQuaDocBienSo kq;
        try
        {
            var viec = ai.DocAsync(new AnhBienSo(anh.Anh, anh.LoaiAnh), ct);
            // ONNX chạy đồng bộ, không huỷ giữa chừng được: chờ tối đa "giay" giây rồi bỏ kết quả
            var xong = await Task.WhenAny(viec, Task.Delay(TimeSpan.FromSeconds(giay), ct));
            if (xong != viec)
            {
                log?.LogWarning("AI đọc biển quá {Giay} giây, chuyển chế độ thủ công", giay);
                return new(null, null, LyDoThuCong, CheDoThuCong: true);
            }
            kq = await viec;
        }
        catch (NhaXe.SharedKernel.LoiNghiepVu) { throw; } // ảnh sai định dạng: lỗi của người gửi, báo thẳng
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log?.LogError(e, "AI đọc biển lỗi, chuyển chế độ thủ công");
            return new(null, null, LyDoThuCong, CheDoThuCong: true);
        }

        var diem = Math.Round((decimal)kq.DoTinCay, 4);
        var khung = kq.UngVien.FirstOrDefault()?.Khung;
        if (kq.BienSo is not null) return new(kq.BienSo, diem, LoaiBien: kq.LoaiBien, Khung: khung);
        var ungVien = kq.UngVien.Select(u => u.BienSo).ToList();
        return new(null, ungVien.Count == 0 ? null : diem,
            ungVien.Count == 0 ? "AI không thấy biển số trong ảnh" : "AI chưa chắc chắn (dưới ngưỡng 0,85): Admin chọn hoặc sửa biển số",
            ungVien, kq.LoaiBien, Khung: khung);
    }
}
