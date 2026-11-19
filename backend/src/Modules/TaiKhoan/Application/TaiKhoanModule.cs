using NhaXe.Modules.TaiKhoan.Contracts;
using NhaXe.Modules.TaiKhoan.Domain;

namespace NhaXe.Modules.TaiKhoan.Application;

/// <summary>Cài đặt cổng công khai của module Tài khoản cho các module khác dùng.</summary>
public class TaiKhoanModule : ITaiKhoanModule
{
    private readonly ITaiKhoanRepository _repo;
    public TaiKhoanModule(ITaiKhoanRepository repo) => _repo = repo;

    public async Task<IReadOnlyDictionary<int, string>> LayHoTenAsync(IEnumerable<int> taiKhoanIds, CancellationToken ct = default)
        => (await _repo.LayNhieuAsync(taiKhoanIds.Distinct(), ct)).ToDictionary(t => t.Id, t => t.HoTen);

    public async Task<IReadOnlyDictionary<int, ThongTinLienHe>> LayLienHeAsync(IEnumerable<int> taiKhoanIds, CancellationToken ct = default)
        => (await _repo.LayNhieuAsync(taiKhoanIds.Distinct(), ct)).ToDictionary(t => t.Id, t => new ThongTinLienHe(t.Id, t.HoTen, t.Sdt, t.Email, t.TaoLuc));

    public async Task<IReadOnlyList<int>> LayAdminIdsAsync(CancellationToken ct = default)
        => (await _repo.LayTheoVaiTroAsync(VaiTro.Admin, ct)).Select(t => t.Id).OrderBy(id => id).ToList();

    public async Task<IReadOnlyDictionary<int, string>> LayMaDayAsync(IEnumerable<int> taiKhoanIds, CancellationToken ct = default)
        => (await _repo.LayNhieuAsync(taiKhoanIds.Distinct(), ct)).Where(t => t.MaDay is not null && !t.BiKhoa)
            .ToDictionary(t => t.Id, t => t.MaDay!);

    public async Task<bool> PhienConHieuLucAsync(int taiKhoanId, string phien, CancellationToken ct = default)
    {
        var tk = await _repo.TimTheoIdAsync(taiKhoanId, ct);
        return tk is not null && !tk.BiKhoa && tk.PhienHienTai == phien;
    }
}
