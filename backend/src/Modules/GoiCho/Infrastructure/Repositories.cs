using Microsoft.EntityFrameworkCore;
using NhaXe.Modules.GoiCho.Application;
using NhaXe.Modules.GoiCho.Domain;
using GoiChoEntity = NhaXe.Modules.GoiCho.Domain.GoiCho;

namespace NhaXe.Modules.GoiCho.Infrastructure;

public class XeRepository : IXeRepository
{
    private readonly GoiChoDbContext _db;
    public XeRepository(GoiChoDbContext db) => _db = db;

    public Task<Xe?> TimAsync(int xeId, CancellationToken ct) => _db.Xes.FirstOrDefaultAsync(x => x.Id == xeId, ct);
    public Task<bool> TonTaiBienSoAsync(string bienSo, CancellationToken ct) => _db.Xes.AnyAsync(x => x.BienSo == bienSo, ct);

    public async Task<IReadOnlyList<Xe>> LayChoDuyetAsync(CancellationToken ct) =>
        await _db.Xes.AsNoTracking().Where(x => x.TrangThaiDuyet == TrangThaiDuyet.ChoDuyet).OrderBy(x => x.TaoLuc).ToListAsync(ct);

    public Task<int> DemChoDuyetCuaAsync(int taiKhoanId, CancellationToken ct) =>
        _db.Xes.CountAsync(x => x.ChuXeId == taiKhoanId && x.TrangThaiDuyet == TrangThaiDuyet.ChoDuyet, ct);

    public void Them(Xe xe) => _db.Xes.Add(xe);
    public Task LuuAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}

public class GoiChoRepository : IGoiChoRepository
{
    private readonly GoiChoDbContext _db;
    public GoiChoRepository(GoiChoDbContext db) => _db = db;

    public Task<GoiChoEntity?> TimAsync(int goiId, CancellationToken ct) => _db.GoiChos.AsNoTracking().FirstOrDefaultAsync(g => g.Id == goiId, ct);

    public async Task<IReadOnlyList<GoiChoEntity>> LayDangBanAsync(CancellationToken ct) =>
        await _db.GoiChos.AsNoTracking().Where(g => g.DangBan).OrderBy(g => g.LoaiXeId).ThenBy(g => g.SoNgay).ToListAsync(ct);

    public async Task<IReadOnlyList<GoiChoEntity>> LayTatCaAsync(CancellationToken ct) =>
        await _db.GoiChos.AsNoTracking().OrderBy(g => g.LoaiXeId).ThenBy(g => g.SoNgay).ToListAsync(ct);

    public Task<GoiChoEntity?> TimDeSuaAsync(int goiId, CancellationToken ct) => _db.GoiChos.FirstOrDefaultAsync(g => g.Id == goiId, ct);

    public Task LuuAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    public Task<bool> TonTaiLoaiXeAsync(int loaiXeId, CancellationToken ct) => _db.LoaiXes.AnyAsync(l => l.Id == loaiXeId, ct);
    public Task<string?> LayMaLoaiXeAsync(int loaiXeId, CancellationToken ct) =>
        _db.LoaiXes.Where(l => l.Id == loaiXeId).Select(l => (string?)l.Ten).FirstOrDefaultAsync(ct);
}

/// <summary>Đọc bảng cau_hinh; thiếu dòng hoặc sai định dạng thì dùng giá trị mặc định.</summary>
public class CauHinhStore : ICauHinh
{
    private readonly GoiChoDbContext _db;
    public CauHinhStore(GoiChoDbContext db) => _db = db;

    public async Task<int> LaySoNguyenAsync(string khoa, int macDinh, CancellationToken ct)
    {
        var giaTri = await _db.Database
            .SqlQuery<string>($"SELECT gia_tri AS Value FROM dbo.cau_hinh WHERE khoa = {khoa}")
            .FirstOrDefaultAsync(ct);
        return int.TryParse(giaTri, out var n) && n > 0 ? n : macDinh;
    }
}
