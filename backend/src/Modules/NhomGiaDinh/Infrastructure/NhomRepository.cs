using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NhaXe.Modules.NhomGiaDinh.Application;
using NhaXe.Modules.NhomGiaDinh.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.NhomGiaDinh.Infrastructure;

public sealed class MaMoiGenerator : IMaMoiGenerator
{
    public string Tao() => Convert.ToHexString(RandomNumberGenerator.GetBytes(5));
}

public class NhomRepository(NhomGiaDinhDbContext db) : INhomRepository
{
    private IQueryable<Nhom> DayDu => db.Nhoms.Include(x => x.ThanhVien).Include(x => x.YeuCau).AsSplitQuery();
    public Task<Nhom?> LayCuaThanhVienAsync(int id, CancellationToken ct) => DayDu.SingleOrDefaultAsync(x => x.ThanhVien.Any(v => v.TaiKhoanId == id), ct);
    public Task<Nhom?> TimTheoMaAsync(string ma, CancellationToken ct) => DayDu.SingleOrDefaultAsync(x => x.MaMoi == ma, ct);
    public Task<bool> DaCoNhomAsync(int id, CancellationToken ct) => db.ThanhViens.AnyAsync(x => x.TaiKhoanId == id, ct);
    public async Task<IReadOnlyList<YeuCauDto>> LayYeuCauCuaAsync(int id, CancellationToken ct) =>
        await db.YeuCaus.AsNoTracking().Where(x => x.TaiKhoanId == id).OrderByDescending(x => x.TaoLuc).Take(50)
            .Select(x => new YeuCauDto(x.Id, x.NhomId, x.TaiKhoanId, x.TrangThai, x.TaoLuc)).ToListAsync(ct);
    public void Them(Nhom nhom) => db.Nhoms.Add(nhom);
    public void Xoa(Nhom nhom) => db.Nhoms.Remove(nhom);
    public Task LuuAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<T> TrongGiaoDichAsync<T>(Func<Task<T>> hanhDong, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var kq = await hanhDong();
            await tx.CommitAsync(ct);
            return kq;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new LoiNghiepVu(LoaiLoi.XungDot, "NHOM_XUNG_DOT", "Thành viên, mã mời hoặc yêu cầu đã tồn tại. Hãy tải lại và thử lại."); }
        catch (Exception ex) when (ex is SqlException { Number: 1205 } || ex.InnerException is SqlException { Number: 1205 })
        { throw new LoiNghiepVu(LoaiLoi.XungDot, "NHOM_XUNG_DOT", "Có thao tác nhóm đồng thời. Hãy tải lại và thử lại."); }
    }
}
