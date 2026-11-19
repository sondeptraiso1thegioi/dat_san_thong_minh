namespace NhaXe.Modules.CongRaVao.Contracts;

/// <summary>
/// Cổng giao tiếp CÔNG KHAI của module Cổng ra vào (số 5). Module này sở hữu bảng luot_gui:
/// "xe nào đang trong bãi, vào lúc nào, đang đỗ ở đâu" chỉ được hỏi qua interface này.
/// BaiXe hỏi để vẽ xe lên sơ đồ. GoiCho KHÔNG gọi interface này (tránh vòng phụ thuộc): GoiCho nghe sự kiện
/// XeDaVao / XeDaRa (SuKienCong.cs) và tự giữ bản sao xe_trong_bai. Xe vào / ra / xác nhận là việc nội bộ của module,
/// gọi qua API (AdminCongController, RaVaoController), không có ở đây.
/// </summary>
public interface ICongRaVaoModule
{
    /// <summary>Mọi lượt gửi đang mở (xe đang trong bãi).</summary>
    Task<IReadOnlyList<LuotGuiDangMo>> LayLuotGuiDangMoAsync(CancellationToken ct = default);

    /// <summary>Lượt gửi đang mở của các xe cho trước (khoá theo xeId). Xe không có trong kết quả = không ở trong bãi.</summary>
    Task<IReadOnlyDictionary<int, LuotGuiDangMo>> LayLuotGuiDangMoCuaXeAsync(IEnumerable<int> xeIds, CancellationToken ct = default);

    /// <summary>
    /// Ghi khu mới nhất vào luot_gui (bản sao nhanh, đề cương v10). CHỈ module TimXe gọi; TimXe vẫn sở hữu lịch sử vi_tri_xe.
    /// khuId = null: xoá khu. Lượt đã đóng thì bỏ qua. nguon: CAMERA_KHU / NGUOI_DUNG / ADMIN.
    /// </summary>
    Task GhiViTriAsync(int luotGuiId, int? khuId, string? nguon, DateTime luc, CancellationToken ct = default);

    /// <summary>Số liệu cổng trong khoảng [tu, den) cho báo cáo (module BaoCao).</summary>
    Task<ThongKeCong> LayThongKeAsync(DateTime tu, DateTime den, CancellationToken ct = default);
}

/// <summary>
/// SoAiTuDoc: lần cho qua mà AI tự đọc (không Admin); SoAdminXuLy: Admin xác nhận / sửa biển / mở tay;
/// DoTinCayTb: trung bình độ tin cậy AI của các lần AI tự đọc (null khi chưa có).
/// </summary>
public record ThongKeCong(int SoLuotVao, int SoLuotRa, int SoTuChoi, int SoAiTuDoc, int SoAdminXuLy, int SoNghiMat, decimal? DoTinCayTb);

/// <summary>Một lượt gửi đang mở, kèm vị trí mới nhất (bản sao nhanh của lần quét camera gần nhất).</summary>
public record LuotGuiDangMo(int LuotGuiId, int XeId, DateTime ThoiGianVao, int? ViTriKhuId, int? ViTriOId, DateTime? ViTriLuc);
