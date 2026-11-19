using NhaXe.Modules.NhomGiaDinh.Domain;

namespace NhaXe.Modules.NhomGiaDinh.Application;

public interface INhomRepository
{
    Task<Nhom?> LayCuaThanhVienAsync(int taiKhoanId, CancellationToken ct);
    Task<Nhom?> TimTheoMaAsync(string ma, CancellationToken ct);
    Task<bool> DaCoNhomAsync(int taiKhoanId, CancellationToken ct);
    Task<IReadOnlyList<YeuCauDto>> LayYeuCauCuaAsync(int taiKhoanId, CancellationToken ct);
    void Them(Nhom nhom);
    void Xoa(Nhom nhom);
    Task LuuAsync(CancellationToken ct);
    // Các bước kiểm tra + thay đổi cùng một giao dịch, ngăn duyệt vào 2 nhóm đồng thời.
    Task<T> TrongGiaoDichAsync<T>(Func<Task<T>> hanhDong, CancellationToken ct);
}

public interface IMaMoiGenerator { string Tao(); }
