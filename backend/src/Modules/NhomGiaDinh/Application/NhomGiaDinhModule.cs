using NhaXe.Modules.NhomGiaDinh.Contracts;

namespace NhaXe.Modules.NhomGiaDinh.Application;

/// <summary>Phía ĐỌC thành viên nhóm. Chỉ module NhomGiaDinh được đọc bảng thanh_vien_nhom.</summary>
public interface IThanhVienNhomQuery
{
    Task<IReadOnlyList<int>> LayNguoiCungNhomAsync(int taiKhoanId, CancellationToken ct);
}

/// <summary>Cài đặt cổng công khai INhomGiaDinhModule.</summary>
public class NhomGiaDinhModule : INhomGiaDinhModule
{
    private readonly IThanhVienNhomQuery _query;
    public NhomGiaDinhModule(IThanhVienNhomQuery query) => _query = query;

    public async Task<IReadOnlyList<int>> LayNguoiCungNhomAsync(int taiKhoanId, CancellationToken ct = default) =>
        (await _query.LayNguoiCungNhomAsync(taiKhoanId, ct)).Where(id => id != taiKhoanId).Distinct().ToList();
}
