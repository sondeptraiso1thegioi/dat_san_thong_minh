using Microsoft.EntityFrameworkCore;
using NhaXe.Modules.TaiKhoan.Application;
using Microsoft.Data.SqlClient;
using NhaXe.SharedKernel;
using TK = NhaXe.Modules.TaiKhoan.Domain.TaiKhoan;

namespace NhaXe.Modules.TaiKhoan.Infrastructure;

/// <summary>Repository (Buổi 4): giấu EF Core sau interface ITaiKhoanRepository.</summary>
public class TaiKhoanRepository : ITaiKhoanRepository
{
    private readonly TaiKhoanDbContext _db;
    public TaiKhoanRepository(TaiKhoanDbContext db) => _db = db;

    public Task<TK?> TimTheoSdtAsync(string sdt, CancellationToken ct) => _db.TaiKhoans.FirstOrDefaultAsync(x => x.Sdt == sdt, ct);
    public Task<TK?> TimTheoIdAsync(int id, CancellationToken ct) => _db.TaiKhoans.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> TonTaiEmailAsync(string email, CancellationToken ct) => _db.TaiKhoans.AnyAsync(x => x.Email == email, ct);
    public void Them(TK taiKhoan) => _db.TaiKhoans.Add(taiKhoan);

    public async Task<IReadOnlyList<TK>> TimKiemAsync(string? tuKhoa, string? trangThai, CancellationToken ct)
    {
        var q = _db.TaiKhoans.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var k = tuKhoa.Trim();
            q = q.Where(x => x.HoTen.Contains(k) || x.Sdt.Contains(k) || (x.Email != null && x.Email.Contains(k)));
        }
        if (!string.IsNullOrWhiteSpace(trangThai)) q = q.Where(x => x.TrangThai == trangThai);
        return await q.OrderBy(x => x.VaiTro).ThenByDescending(x => x.Id).Take(500).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TK>> LayNhieuAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var ds = ids.ToList();
        if (ds.Count == 0) return [];
        return await _db.TaiKhoans.AsNoTracking().Where(x => ds.Contains(x.Id)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TK>> LayTheoVaiTroAsync(string vaiTro, CancellationToken ct) =>
        await _db.TaiKhoans.AsNoTracking().Where(x => x.VaiTro == vaiTro).ToListAsync(ct);

    public async Task LuuAsync(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { throw new LoiNghiepVu(LoaiLoi.XungDot, "TAI_KHOAN_DA_DOI", "Tài khoản vừa thay đổi. Vui lòng thử lại."); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new LoiNghiepVu(LoaiLoi.XungDot, "TAI_KHOAN_TRUNG", "Số điện thoại hoặc email đã được đăng ký."); }
    }
}
