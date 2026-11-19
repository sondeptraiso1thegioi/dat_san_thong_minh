using NhaXe.Modules.TimXe.Contracts;
using NhaXe.Modules.TimXe.Domain;

namespace NhaXe.Modules.TimXe.Application;

/// <summary>Phía ghi / đọc bảng của TimXe (giu_cho_khu, vi_tri_xe). Một lần LuuAsync = một transaction.</summary>
public interface ITimXeRepository
{
    Task<GiuChoKhu?> TimGiuDangGiuAsync(int luotGuiId, CancellationToken ct);
    /// <summary>Số suất DANG_GIU còn hạn theo khu.</summary>
    Task<IReadOnlyDictionary<int, int>> DemGiuTheoKhuAsync(DateTime bayGio, CancellationToken ct);
    /// <summary>
    /// Đổi mọi suất DANG_GIU đã quá giờ thành HET_HAN bằng MỘT câu UPDATE ... OUTPUT và trả các suất vừa đổi.
    /// Hai bản API chạy cùng lúc thì mỗi suất chỉ một bản nhận được (không báo người dùng hai lần).
    /// </summary>
    Task<IReadOnlyList<GiuChoKhu>> KetThucQuaHanAsync(DateTime bayGio, CancellationToken ct);
    /// <summary>Lần ghi gần nhất của lượt gửi (mọi nguồn) và lần gần nhất tại một camera (chống đọc lặp).</summary>
    Task<ViTriXe?> LayGhiCuoiAsync(int luotGuiId, CancellationToken ct);
    Task<DateTime?> LayLanCuoiTaiCameraAsync(int luotGuiId, int cameraId, CancellationToken ct);
    /// <summary>Suất được mời gần nhất của các lượt gửi (để hiện "Mời vào khu X" trên app).</summary>
    Task<IReadOnlyDictionary<int, GiuChoKhu>> LayGiuGanNhatAsync(IReadOnlyCollection<int> luotGuiIds, CancellationToken ct);
    Task<ThongKeDinhVi> LayThongKeAsync(DateTime tu, DateTime den, CancellationToken ct);

    /// <summary>Lần đi sai gần nhất của lượt gửi tại khu (chống báo lặp khi camera tự quét mỗi giây).</summary>
    Task<DateTime?> LayLanSaiCuoiAsync(int luotGuiId, int khuId, CancellationToken ct);
    Task<IReadOnlyList<DiSaiKhu>> LayDiSaiGanDayAsync(int soDong, CancellationToken ct);
    /// <summary>Số lần đi sai theo chủ xe (Admin xem ai hay đi sai). Nhiều lần nhất trước.</summary>
    Task<IReadOnlyList<DemDiSai>> DemDiSaiTheoChuXeAsync(CancellationToken ct);

    void Them(GiuChoKhu g);
    void Them(ViTriXe v);
    void Them(DiSaiKhu d);
    Task LuuAsync(CancellationToken ct);
}

public record DemDiSai(int ChuXeId, int SoLan, int SoLanKhuDay, DateTime LanCuoi);

/// <summary>Tham số cau_hinh của TimXe (V004).</summary>
public interface ICauHinhTimXe
{
    Task<int> LayAsync(string khoa, int macDinh, CancellationToken ct);
}

public static class KhoaCauHinh
{
    public const string PhutGiu = "phut_giu_cho_khu";
    public const string GiayChongDocLap = "giay_chong_doc_lap";
    public const string PhanTramSapDay = "phan_tram_sap_day";
    public const string PhutChuaDinhVi = "phut_chua_dinh_vi";
}
