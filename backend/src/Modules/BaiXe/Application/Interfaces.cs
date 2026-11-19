using NhaXe.Modules.BaiXe.Domain;

namespace NhaXe.Modules.BaiXe.Application;

/// <summary>
/// Phía ĐỌC của module BaiXe: chỉ đọc bảng của module mình (khu_vuc, o_do; sau này camera, nut_duong_di).
/// Xe nào đang trong bãi, đỗ ở đâu: hỏi CongRaVao (ICongRaVaoModule). Biển số, loại xe, hạn gói: hỏi GoiCho (IGoiChoModule).
/// </summary>
public interface IBaiXeQuery
{
    Task<IReadOnlyList<KhuRow>> LayKhuAsync(CancellationToken ct);
    Task<IReadOnlyList<ODoRow>> LayODoAsync(CancellationToken ct);
}

/// <summary>
/// Phía GHI của module BaiXe (khu, ô, camera). Lấy thực thể "để sửa" (EF theo dõi thay đổi), sửa bằng hàm của thực thể, rồi LuuAsync.
/// Xoá thứ đang được bảng khác trỏ tới (vị trí xe, lối đi) thì LuuAsync báo lỗi DANG_DUOC_DUNG.
/// </summary>
public interface IBaiXeRepository
{
    Task<KhuVuc?> TimKhuAsync(int id, CancellationToken ct);
    Task<bool> TrungTenKhuAsync(string ten, int? truId, CancellationToken ct);
    Task<int> DemOCuaKhuAsync(int khuId, CancellationToken ct);
    Task<int> DemCameraCuaKhuAsync(int khuId, CancellationToken ct);
    void Them(KhuVuc khu);
    void Xoa(KhuVuc khu);

    Task<ODo?> TimOAsync(int id, CancellationToken ct);
    Task<bool> TrungMaOAsync(string maO, int? truId, CancellationToken ct);
    void Them(ODo o);
    void Xoa(ODo o);

    Task<Camera?> TimCameraAsync(int id, CancellationToken ct);
    Task<bool> TrungTenCameraAsync(string ten, int? truId, CancellationToken ct);
    Task<int> DemOCuaCameraAsync(int cameraId, CancellationToken ct);
    void Them(Camera c);
    void Xoa(Camera c);

    Task<TrangThaiO?> TimTrangThaiOAsync(int oId, CancellationToken ct);
    void Them(TrangThaiO t);

    Task LuuAsync(CancellationToken ct);
}

/// <summary>Đọc toàn bộ cấu hình bãi để hiển thị (không theo dõi thay đổi).</summary>
public interface ICauHinhBaiQuery
{
    Task<IReadOnlyList<KhuVuc>> LayKhuAsync(CancellationToken ct);
    Task<IReadOnlyList<ODo>> LayOAsync(CancellationToken ct);
    Task<IReadOnlyList<Camera>> LayCameraAsync(CancellationToken ct);
    Task<IReadOnlyList<TrangThaiO>> LayTrangThaiOAsync(CancellationToken ct);
    /// <summary>Nút và cạnh lối đi (bảng nut_duong_di, canh_duong_di).</summary>
    Task<IReadOnlyList<NutRow>> LayNutAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<NutRow>>(Array.Empty<NutRow>());
    Task<IReadOnlyList<DuongNganNhat.Canh>> LayCanhAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DuongNganNhat.Canh>>(Array.Empty<DuongNganNhat.Canh>());
}

/// <summary>Nút lối đi. KhuId khác null: nút đầu khu. Nút "Lối vào" (không gắn khu, id nhỏ nhất) là cổng.</summary>
public record NutRow(int Id, string Ten, double X, double Y, int? KhuId);
