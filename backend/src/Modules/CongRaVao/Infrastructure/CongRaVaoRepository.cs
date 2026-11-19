using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NhaXe.Modules.CongRaVao.Application;
using NhaXe.Modules.CongRaVao.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.CongRaVao.Infrastructure;

public class CongRaVaoRepository : ICongRaVaoRepository
{
    private readonly CongRaVaoDbContext _db;
    public CongRaVaoRepository(CongRaVaoDbContext db) => _db = db;

    public Task<LuotGui?> TimLuotAsync(int id, CancellationToken ct) => _db.LuotGui.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<LuotGui?> TimLuotMoCuaXeAsync(int xeId, CancellationToken ct) =>
        _db.LuotGui.FirstOrDefaultAsync(x => x.XeId == xeId && x.ThoiGianRa == null, ct);
    public Task<bool> CoSuKienTuAsync(string loai, string bienSo, DateTime tu, CancellationToken ct) =>
        _db.SuKienCong.AsNoTracking().AnyAsync(x => x.Loai == loai && x.BienSo == bienSo && x.ThoiGian >= tu, ct);
    public Task<YeuCauXacNhan?> TimYeuCauAsync(int id, CancellationToken ct) => _db.YeuCau.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<YeuCauXacNhan?> TimYeuCauDangChoAsync(int luotGuiId, CancellationToken ct) =>
        _db.YeuCau.FirstOrDefaultAsync(x => x.LuotGuiId == luotGuiId && x.KetQua == KetQuaXacNhan.Cho, ct);

    public async Task<IReadOnlyList<int>> LayIdYeuCauQuaHanAsync(DateTime bayGio, CancellationToken ct) =>
        await _db.YeuCau.Where(x => x.KetQua == KetQuaXacNhan.Cho && x.HetHanLuc <= bayGio).Select(x => x.Id).ToListAsync(ct);

    public void Them(LuotGui luot) => _db.LuotGui.Add(luot);
    public void Them(YeuCauXacNhan yeuCau) => _db.YeuCau.Add(yeuCau);
    public void Them(SuKienCong suKien) => _db.SuKienCong.Add(suKien);
    public void BoTheoDoi() => _db.ChangeTracker.Clear();

    public async Task LuuAsync(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new LoiNghiepVu(LoaiLoi.XungDot, "YEU_CAU_DA_THAY_DOI", "Yêu cầu vừa được người khác cập nhật. Tải lại rồi thử lại.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql)
        {
            throw sql.Message.Contains("UX_lg_mot_luot_mo")
                ? new LoiNghiepVu(LoaiLoi.XungDot, "XE_DA_TRONG_BAI", "Xe này đang ở trong bãi.")
                : sql.Message.Contains("UX_ycxn_dang_cho")
                    ? new LoiNghiepVu(LoaiLoi.XungDot, MaDangCho, "Xe đang có yêu cầu chờ xác nhận.")
                    : new LoiNghiepVu(LoaiLoi.XungDot, "TRUNG_DU_LIEU", "Dữ liệu bị trùng.");
        }
    }

    private const string MaDangCho = "DANG_CHO_XAC_NHAN";
}

/// <summary>Phía đọc: AsNoTracking, chỉ bảng của CongRaVao.</summary>
public class CongQuery : ICongQuery
{
    private readonly CongRaVaoDbContext _db;
    public CongQuery(CongRaVaoDbContext db) => _db = db;

    public async Task<IReadOnlyList<(YeuCauXacNhan YeuCau, int XeId)>> LayYeuCauDangChoAsync(IReadOnlyCollection<int>? xeIds, CancellationToken ct)
    {
        var q = from y in _db.YeuCau.AsNoTracking()
                join l in _db.LuotGui.AsNoTracking() on y.LuotGuiId equals l.Id
                where y.KetQua == KetQuaXacNhan.Cho
                select new { y, l.XeId };
        if (xeIds is not null) q = q.Where(r => xeIds.Contains(r.XeId));
        return (await q.ToListAsync(ct)).Select(r => (r.y, r.XeId)).ToList();
    }

    public async Task<IReadOnlyList<SuKienCong>> LaySuKienGanDayAsync(int soDong, CancellationToken ct) =>
        await _db.SuKienCong.AsNoTracking().OrderByDescending(s => s.ThoiGian).ThenByDescending(s => s.Id).Take(soDong).ToListAsync(ct);

    public async Task<IReadOnlyList<LuotGui>> LayLuotCuaXeAsync(IReadOnlyCollection<int> xeIds, int soDong, CancellationToken ct) =>
        await _db.LuotGui.AsNoTracking().Where(l => xeIds.Contains(l.XeId))
                 .OrderByDescending(l => l.ThoiGianVao).Take(soDong).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<int, YeuCauXacNhan>> LayYeuCauGanNhatAsync(IReadOnlyCollection<int> luotIds, CancellationToken ct)
    {
        if (luotIds.Count == 0) return new Dictionary<int, YeuCauXacNhan>();
        return (await _db.YeuCau.AsNoTracking().Where(y => luotIds.Contains(y.LuotGuiId)).ToListAsync(ct))
            .GroupBy(y => y.LuotGuiId).ToDictionary(g => g.Key, g => g.MaxBy(y => y.TaoLuc)!);
    }

    public async Task<IReadOnlyList<(LuotGui Luot, string LyDo)>> LayLuotCanXuLyAsync(CancellationToken ct)
    {
        var mo = await _db.LuotGui.AsNoTracking().Where(l => l.ThoiGianRa == null).ToListAsync(ct);
        if (mo.Count == 0) return Array.Empty<(LuotGui, string)>();
        var ids = mo.Select(l => l.Id).ToList();
        // yêu cầu gần nhất của từng lượt: HET_HAN mà lượt đã về DANG_GUI = "hết 60 giây, chuyển Admin"
        var ganNhat = (await _db.YeuCau.AsNoTracking().Where(y => ids.Contains(y.LuotGuiId)).ToListAsync(ct))
            .GroupBy(y => y.LuotGuiId).ToDictionary(g => g.Key, g => g.MaxBy(y => y.TaoLuc)!);
        var kq = new List<(LuotGui, string)>();
        foreach (var l in mo)
        {
            var y = ganNhat.GetValueOrDefault(l.Id);
            if (l.TrangThai == TrangThaiLuot.NghiMat) kq.Add((l, "Người dùng báo không phải mình lấy xe (nghi mất)"));
            else if (l.TrangThai == TrangThaiLuot.ChoThanhToan) kq.Add((l, "Đã xác nhận ra, chờ trả phí quá hạn"));
            else if (l.TrangThai == TrangThaiLuot.DangGui && y?.KetQua == KetQuaXacNhan.HetHan)
                kq.Add((l, "Hết 60 giây không ai xác nhận ở cổng ra"));
        }
        return kq;
    }

    public async Task<Contracts.ThongKeCong> LayThongKeAsync(DateTime tu, DateTime den, CancellationToken ct)
    {
        var ds = await _db.SuKienCong.AsNoTracking().Where(s => s.ThoiGian >= tu && s.ThoiGian < den)
            .Select(s => new { s.Loai, s.KetQua, s.AdminId, s.DoTinCay }).ToListAsync(ct);
        var aiTuDoc = ds.Where(s => s.KetQua == SuKienCong.ChoQua && s.AdminId == null && s.DoTinCay is not null and < 1m).ToList();
        var nghiMat = await _db.YeuCau.AsNoTracking()
            .CountAsync(y => y.KetQua == KetQuaXacNhan.TuChoi && y.ChuXeDongY == false && y.TaoLuc >= tu && y.TaoLuc < den, ct);
        return new Contracts.ThongKeCong(
            ds.Count(s => s.Loai == SuKienCong.Vao && s.KetQua == SuKienCong.ChoQua),
            ds.Count(s => s.Loai == SuKienCong.Ra && s.KetQua == SuKienCong.ChoQua),
            ds.Count(s => s.KetQua == SuKienCong.TuChoi),
            aiTuDoc.Count,
            ds.Count(s => s.AdminId != null),
            nghiMat,
            aiTuDoc.Count == 0 ? null : Math.Round(aiTuDoc.Average(s => s.DoTinCay!.Value), 4));
    }
}

/// <summary>Đọc cau_hinh (bảng cấu hình chung của hệ thống, chỉ đọc).</summary>
public class CauHinhCong : ICauHinhCong
{
    public const string Khoa = "thoi_gian_cho_xac_nhan_giay";
    private readonly CongRaVaoDbContext _db;
    private readonly ILogger<CauHinhCong>? _log;
    public CauHinhCong(CongRaVaoDbContext db, ILogger<CauHinhCong>? log = null) { _db = db; _log = log; }

    public async Task<int> LaySoGiayChoXacNhanAsync(CancellationToken ct)
    {
        var s = await _db.Database.SqlQuery<string>($"SELECT gia_tri AS Value FROM dbo.cau_hinh WHERE khoa = {Khoa}").FirstOrDefaultAsync(ct);
        // Giá trị cấu hình hỏng (không phải số, ngoài 10–600 giây) là lỗi CẤU HÌNH, không phải lỗi của bảo vệ ở cổng:
        // dùng mặc định 60 giây và ghi log để Admin sửa, cổng vẫn chạy.
        if (int.TryParse(s, out var v) && v is >= YeuCauXacNhan.SoGiayToiThieu and <= YeuCauXacNhan.SoGiayToiDa) return v;
        if (s is not null)
            _log?.LogWarning("cau_hinh.{Khoa} = '{GiaTri}' không hợp lệ (cần {Min}–{Max}), dùng mặc định {MacDinh} giây",
                             Khoa, s, YeuCauXacNhan.SoGiayToiThieu, YeuCauXacNhan.SoGiayToiDa, YeuCauXacNhan.SoGiayMacDinh);
        return YeuCauXacNhan.SoGiayMacDinh;
    }

    public async Task<int> LayGiayGioiHanAiAsync(CancellationToken ct)
    {
        var s = await _db.Database.SqlQuery<string>($"SELECT gia_tri AS Value FROM dbo.cau_hinh WHERE khoa = {"giay_gioi_han_ai"}").FirstOrDefaultAsync(ct);
        return int.TryParse(s, out var v) && v is >= 1 and <= 30 ? v : 3;
    }
}
