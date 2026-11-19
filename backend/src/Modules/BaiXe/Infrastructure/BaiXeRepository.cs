using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NhaXe.Modules.BaiXe.Application;
using NhaXe.Modules.BaiXe.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.BaiXe.Infrastructure;

/// <summary>Phía ghi: EF Core theo dõi thực thể, LuuAsync = SaveChanges. Lỗi ràng buộc của SQL được đổi sang lỗi nghiệp vụ.</summary>
public class BaiXeRepository : IBaiXeRepository
{
    private readonly BaiXeDbContext _db;
    public BaiXeRepository(BaiXeDbContext db) => _db = db;

    public Task<KhuVuc?> TimKhuAsync(int id, CancellationToken ct) => _db.Khu.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> TrungTenKhuAsync(string ten, int? truId, CancellationToken ct) =>
        _db.Khu.AnyAsync(x => x.Ten == ten && x.Id != (truId ?? 0), ct);
    public Task<int> DemOCuaKhuAsync(int khuId, CancellationToken ct) => _db.O.CountAsync(x => x.KhuId == khuId, ct);
    public Task<int> DemCameraCuaKhuAsync(int khuId, CancellationToken ct) => _db.Camera.CountAsync(x => x.KhuId == khuId, ct);
    public void Them(KhuVuc khu) => _db.Khu.Add(khu);
    public void Xoa(KhuVuc khu) => _db.Khu.Remove(khu);

    public Task<ODo?> TimOAsync(int id, CancellationToken ct) => _db.O.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> TrungMaOAsync(string maO, int? truId, CancellationToken ct) =>
        _db.O.AnyAsync(x => x.MaO == maO && x.Id != (truId ?? 0), ct);
    public void Them(ODo o) => _db.O.Add(o);
    public void Xoa(ODo o) => _db.O.Remove(o);

    public Task<Camera?> TimCameraAsync(int id, CancellationToken ct) => _db.Camera.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> TrungTenCameraAsync(string ten, int? truId, CancellationToken ct) =>
        _db.Camera.AnyAsync(x => x.Ten == ten && x.Id != (truId ?? 0), ct);
    public Task<int> DemOCuaCameraAsync(int cameraId, CancellationToken ct) => _db.O.CountAsync(x => x.CameraId == cameraId, ct);
    public void Them(Camera c) => _db.Camera.Add(c);
    public void Xoa(Camera c) => _db.Camera.Remove(c);

    public Task<TrangThaiO?> TimTrangThaiOAsync(int oId, CancellationToken ct) => _db.TrangThaiO.FirstOrDefaultAsync(x => x.OId == oId, ct);
    public void Them(TrangThaiO t) => _db.TrangThaiO.Add(t);

    public async Task LuuAsync(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql)
        {
            // Đổi đúng 2 loại lỗi mà người dùng có thể gây ra; lỗi khác để LoiMiddleware trả 500.
            throw sql.Number switch
            {
                // 547: vi phạm khoá ngoại, ví dụ xoá ô đã có lượt gửi / vị trí xe trỏ tới (bảng của module khác)
                547 => new LoiNghiepVu(LoaiLoi.XungDot, "DANG_DUOC_DUNG",
                                       "Dữ liệu này đang được nơi khác dùng (lượt gửi, vị trí xe hoặc lối đi), không xoá được."),
                // 2601/2627: trùng UNIQUE (hai Admin thêm cùng lúc, lọt qua bước kiểm tra trùng)
                2601 or 2627 => new LoiNghiepVu(LoaiLoi.XungDot, "TRUNG_DU_LIEU", "Dữ liệu bị trùng (tên khu, tên camera hoặc mã ô)."),
                _ => ex
            };
        }
    }
}

/// <summary>Phía đọc cấu hình bãi: AsNoTracking vì chỉ để hiển thị.</summary>
public class CauHinhBaiQuery : ICauHinhBaiQuery
{
    private readonly BaiXeDbContext _db;
    public CauHinhBaiQuery(BaiXeDbContext db) => _db = db;

    public async Task<IReadOnlyList<KhuVuc>> LayKhuAsync(CancellationToken ct) => await _db.Khu.AsNoTracking().ToListAsync(ct);
    public async Task<IReadOnlyList<ODo>> LayOAsync(CancellationToken ct) => await _db.O.AsNoTracking().ToListAsync(ct);
    public async Task<IReadOnlyList<Camera>> LayCameraAsync(CancellationToken ct) => await _db.Camera.AsNoTracking().ToListAsync(ct);
    public async Task<IReadOnlyList<TrangThaiO>> LayTrangThaiOAsync(CancellationToken ct) => await _db.TrangThaiO.AsNoTracking().ToListAsync(ct);

    // Đồ thị lối đi lấy từ do-thi-ban-do.json (toạ độ theo ảnh sơ đồ thật), không còn đọc nut_duong_di / canh_duong_di
    // (hai bảng đó giữ nguyên trong DB, toạ độ của sơ đồ mẫu cũ). Nút điểm đến gắn với khu qua khu_vuc.ma_so_do.
    private async Task<(IReadOnlyList<NutRow> Nut, IReadOnlyList<DuongNganNhat.Canh> Canh)> DoThiAsync(CancellationToken ct)
    {
        var khu = await _db.Khu.AsNoTracking().Where(k => k.MaSoDo != null && k.DangDung)
            .Select(k => new { k.Id, k.MaSoDo }).ToListAsync(ct);
        return DoThiBanDo.MacDinh.ChoBaiXe(khu.ToDictionary(k => k.MaSoDo!, k => k.Id));
    }

    public async Task<IReadOnlyList<NutRow>> LayNutAsync(CancellationToken ct) => (await DoThiAsync(ct)).Nut;
    public async Task<IReadOnlyList<DuongNganNhat.Canh>> LayCanhAsync(CancellationToken ct) => (await DoThiAsync(ct)).Canh;
}
