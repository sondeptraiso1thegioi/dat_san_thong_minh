using NhaXe.Modules.NhomGiaDinh.Contracts;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.GoiCho.Application;

// Các chỗ GoiCho cần hỏi module khác, gom vào đây để use case không lặp lại.
// Mỗi câu hỏi đi qua Contracts của module sở hữu dữ liệu, GoiCho không đọc bảng của họ.

/// <summary>Kiểm tra quyền dùng xe: chủ xe, hoặc người cùng nhóm (hỏi NhomGiaDinh) khi xe bật chia sẻ.</summary>
public class QuyenDungXe
{
    private readonly IXeRepository _xe;
    private readonly INhomGiaDinhModule _nhom;
    public QuyenDungXe(IXeRepository xe, INhomGiaDinhModule nhom) { _xe = xe; _nhom = nhom; }

    /// <summary>Không có quyền thì báo "không tìm thấy xe" (không để lộ là xe có tồn tại).</summary>
    public async Task KiemTraAsync(int taiKhoanId, int xeId, CancellationToken ct)
    {
        var xe = await _xe.TimAsync(xeId, ct);
        if (xe is null || !xe.DuocDungBoi(taiKhoanId, await _nhom.LayNguoiCungNhomAsync(taiKhoanId, ct)))
            throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_TIM_THAY_XE", "Không tìm thấy xe.");
    }
}

/// <summary>Xe đang trong bãi (bản sao xe_trong_bai của GoiCho) và nợ phí quá hạn của lượt gửi đó (GoiCho tự tính).</summary>
public record TinhTrangTrongBai(int LuotGuiId, int SoNgayNo, decimal SoTienNo, PhiQuaHanDto? No);

/// <summary>
/// "Xe nào đang trong bãi" đọc từ bản sao xe_trong_bai mà GoiCho tự giữ bằng cách nghe sự kiện XeDaVao / XeDaRa
/// của CongRaVao (NgheCongVaNhacHan.cs). Nhờ vậy GoiCho KHÔNG gọi ICongRaVaoModule: chiều gọi chỉ còn CongRaVao → GoiCho.
/// </summary>
public class TraCuuTrongBai
{
    private readonly IXeTrongBaiStore _trongBai;
    private readonly IPhiQuaHanStore _phi;
    public TraCuuTrongBai(IXeTrongBaiStore trongBai, IPhiQuaHanStore phi) { _trongBai = trongBai; _phi = phi; }

    /// <summary>Chỉ có mặt trong kết quả những xe đang ở trong bãi.</summary>
    public async Task<IReadOnlyDictionary<int, TinhTrangTrongBai>> LayAsync(IEnumerable<int> xeIds, CancellationToken ct)
    {
        var ids = xeIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, TinhTrangTrongBai>();
        var luot = await _trongBai.LayAsync(ids, ct); // xeId → luotGuiId
        if (luot.Count == 0) return new Dictionary<int, TinhTrangTrongBai>();
        var no = (await _phi.LayNoAsync(luot.Values.ToList(), ct)).ToDictionary(n => n.LuotGuiId);
        return luot.ToDictionary(l => l.Key, l =>
        {
            var n = no.GetValueOrDefault(l.Value);
            return new TinhTrangTrongBai(l.Value, n?.SoNgayConNo ?? 0, n?.SoTienConNo ?? 0, n);
        });
    }

    public async Task<bool> DangTrongBaiAsync(int xeId, CancellationToken ct) =>
        (await _trongBai.LayAsync(new[] { xeId }, ct)).ContainsKey(xeId);
}
