using NhaXe.Modules.BaiXe.Contracts;
using NhaXe.Modules.BaiXe.Domain;
using NhaXe.Modules.GoiCho.Contracts;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.BaiXe.Application;

// Use case của module BaiXe. Mỗi class một việc (SRP), nhận interface qua constructor (DIP).
// Quy tắc (tên khu, mã ô, camera cùng khu, trạng thái ô) nằm trong Domain; use case chỉ điều phối:
// lấy dữ liệu → gọi hàm của thực thể → lưu.

internal static class Loi
{
    public static LoiNghiepVu KhongThay(string ma, string tin) => new(LoaiLoi.KhongTimThay, ma, tin);
    public static LoiNghiepVu XungDot(string ma, string tin) => new(LoaiLoi.XungDot, ma, tin);
}

/// <summary>Toàn bộ khu, ô, camera kèm trạng thái ô, cho trang Quản lý bãi.</summary>
public class LayCauHinhBaiUseCase
{
    private readonly ICauHinhBaiQuery _q;
    private readonly IGoiChoModule _goiCho;
    public LayCauHinhBaiUseCase(ICauHinhBaiQuery q, IGoiChoModule goiCho) { _q = q; _goiCho = goiCho; }

    public async Task<CauHinhBaiDto> ThucHienAsync(CancellationToken ct = default)
    {
        var khu = await _q.LayKhuAsync(ct);
        var o = await _q.LayOAsync(ct);
        var cam = await _q.LayCameraAsync(ct);
        var tt = (await _q.LayTrangThaiOAsync(ct)).ToDictionary(t => t.OId);
        var loai = (await _goiCho.LaySucChuaAsync(ct)).Select(s => new LoaiXeDto(s.LoaiXeId, s.LoaiXe)).ToList();
        return new CauHinhBaiDto(
            loai,
            khu.OrderBy(k => k.Ten).Select(k => new KhuQuanLyDto(k.Id, k.Ten, k.LoaiXeId, o.Count(x => x.KhuId == k.Id),
                                                                 cam.Count(c => c.KhuId == k.Id), k.SucChua, k.MaSoDo, k.DangDung)).ToList(),
            o.OrderBy(x => x.MaO).Select(x =>
            {
                var t = tt.GetValueOrDefault(x.Id);
                return new OQuanLyDto(x.Id, x.KhuId, x.MaO, x.MaQr, x.CameraId, t?.TrangThai ?? TrangThaiOMa.KhongRo, t?.Nguon, t?.CapNhatLuc);
            }).ToList(),
            cam.OrderBy(c => c.Ten).Select(c => new CameraDto(c.Id, c.KhuId, c.Ten, c.NguonAnh, c.DangBat, c.VaiTro)).ToList());
    }
}

// ---------------- Khu ----------------

public class ThemKhuUseCase
{
    private readonly IBaiXeRepository _repo;
    private readonly IGoiChoModule _goiCho;
    public ThemKhuUseCase(IBaiXeRepository repo, IGoiChoModule goiCho) { _repo = repo; _goiCho = goiCho; }

    public async Task<int> ThucHienAsync(KhuLenh lenh, CancellationToken ct = default)
    {
        var loai = await _goiCho.LaySucChuaAsync(ct);
        var khu = KhuVuc.Tao(lenh.Ten, lenh.LoaiXeId, LoaiXe.Ids(loai));
        if (lenh.SucChua is int sc) khu.DatSucChua(sc, LoaiXe.LaXeMay(loai, khu.LoaiXeId));
        if (lenh.DangDung is bool dd) khu.DatDangDung(dd);
        if (await _repo.TrungTenKhuAsync(khu.Ten, null, ct)) throw Loi.XungDot("TRUNG_TEN_KHU", $"Đã có khu tên \"{khu.Ten}\".");
        _repo.Them(khu);
        await _repo.LuuAsync(ct);
        return khu.Id;
    }
}

public class SuaKhuUseCase
{
    private readonly IBaiXeRepository _repo;
    private readonly IGoiChoModule _goiCho;
    public SuaKhuUseCase(IBaiXeRepository repo, IGoiChoModule goiCho) { _repo = repo; _goiCho = goiCho; }

    public async Task ThucHienAsync(int id, KhuLenh lenh, CancellationToken ct = default)
    {
        var khu = await _repo.TimKhuAsync(id, ct) ?? throw Loi.KhongThay("KHONG_TIM_THAY_KHU", "Không tìm thấy khu.");
        // đổi loại xe của khu đang có ô sẽ làm sai sức chứa thực tế: chỉ cho đổi khi khu chưa có ô
        if (lenh.LoaiXeId != khu.LoaiXeId && await _repo.DemOCuaKhuAsync(id, ct) > 0)
            throw Loi.XungDot("KHU_CON_O", "Khu đã có ô, không đổi loại xe được. Xoá hết ô trước.");
        var loai = await _goiCho.LaySucChuaAsync(ct);
        khu.Sua(lenh.Ten, lenh.LoaiXeId, LoaiXe.Ids(loai));
        // không gửi sức chứa mà đổi sang loại xe máy: vẫn kiểm tra số đang có không vượt 80
        khu.DatSucChua(lenh.SucChua ?? khu.SucChua, LoaiXe.LaXeMay(loai, khu.LoaiXeId));
        if (lenh.DangDung is bool dd) khu.DatDangDung(dd);
        if (await _repo.TrungTenKhuAsync(khu.Ten, id, ct)) throw Loi.XungDot("TRUNG_TEN_KHU", $"Đã có khu tên \"{khu.Ten}\".");
        await _repo.LuuAsync(ct);
    }
}

public class XoaKhuUseCase
{
    private readonly IBaiXeRepository _repo;
    public XoaKhuUseCase(IBaiXeRepository repo) => _repo = repo;

    public async Task ThucHienAsync(int id, CancellationToken ct = default)
    {
        var khu = await _repo.TimKhuAsync(id, ct) ?? throw Loi.KhongThay("KHONG_TIM_THAY_KHU", "Không tìm thấy khu.");
        if (await _repo.DemOCuaKhuAsync(id, ct) > 0) throw Loi.XungDot("KHU_CON_O", "Khu còn ô. Xoá hết ô trước khi xoá khu.");
        if (await _repo.DemCameraCuaKhuAsync(id, ct) > 0) throw Loi.XungDot("KHU_CON_CAMERA", "Khu còn camera. Xoá camera trước.");
        _repo.Xoa(khu);
        await _repo.LuuAsync(ct);
    }
}

// ---------------- Ô ----------------

public class ThemOUseCase
{
    private readonly IBaiXeRepository _repo;
    private readonly IClock _clock;
    public ThemOUseCase(IBaiXeRepository repo, IClock clock) { _repo = repo; _clock = clock; }

    public async Task<int> ThucHienAsync(OLenh lenh, CancellationToken ct = default)
    {
        var khu = await _repo.TimKhuAsync(lenh.KhuId, ct) ?? throw Loi.KhongThay("KHONG_TIM_THAY_KHU", "Không tìm thấy khu.");
        var cam = lenh.CameraId is int c ? await _repo.TimCameraAsync(c, ct) ?? throw Loi.KhongThay("KHONG_TIM_THAY_CAMERA", "Không tìm thấy camera.") : null;
        var o = ODo.Tao(khu, lenh.MaO, cam);
        if (await _repo.TrungMaOAsync(o.MaO, null, ct)) throw Loi.XungDot("TRUNG_MA_O", $"Đã có ô {o.MaO}.");
        _repo.Them(o);
        await _repo.LuuAsync(ct);
        // ô mới: chưa ai báo nên "không rõ" cho tới khi Admin hoặc AI cập nhật
        _repo.Them(TrangThaiO.Tao(o.Id, TrangThaiOMa.KhongRo, NguonTrangThai.Admin, _clock.BayGio));
        await _repo.LuuAsync(ct);
        return o.Id;
    }
}

public class SuaOUseCase
{
    private readonly IBaiXeRepository _repo;
    public SuaOUseCase(IBaiXeRepository repo) => _repo = repo;

    public async Task ThucHienAsync(int id, OLenh lenh, CancellationToken ct = default)
    {
        var o = await _repo.TimOAsync(id, ct) ?? throw Loi.KhongThay("KHONG_TIM_THAY_O", "Không tìm thấy ô.");
        if (lenh.KhuId != o.KhuId) throw Loi.XungDot("KHONG_DOI_KHU", "Không chuyển ô sang khu khác được. Xoá ô rồi tạo ở khu mới.");
        var cam = lenh.CameraId is int c ? await _repo.TimCameraAsync(c, ct) ?? throw Loi.KhongThay("KHONG_TIM_THAY_CAMERA", "Không tìm thấy camera.") : null;
        o.Sua(lenh.MaO, cam);
        if (await _repo.TrungMaOAsync(o.MaO, id, ct)) throw Loi.XungDot("TRUNG_MA_O", $"Đã có ô {o.MaO}.");
        await _repo.LuuAsync(ct);
    }
}

public class XoaOUseCase
{
    private readonly IBaiXeRepository _repo;
    public XoaOUseCase(IBaiXeRepository repo) => _repo = repo;

    public async Task ThucHienAsync(int id, CancellationToken ct = default)
    {
        var o = await _repo.TimOAsync(id, ct) ?? throw Loi.KhongThay("KHONG_TIM_THAY_O", "Không tìm thấy ô.");
        _repo.Xoa(o); // trang_thai_o xoá theo (ON DELETE CASCADE); ô đã có lịch sử vị trí xe thì DB chặn → DANG_DUOC_DUNG
        await _repo.LuuAsync(ct);
    }
}

/// <summary>Admin đặt trạng thái ô bằng tay (thay cho AI ô trống khi chưa có).</summary>
public class DatTrangThaiOUseCase
{
    private readonly IBaiXeRepository _repo;
    private readonly IClock _clock;
    public DatTrangThaiOUseCase(IBaiXeRepository repo, IClock clock) { _repo = repo; _clock = clock; }

    public Task ThucHienAsync(int oId, string? trangThai, CancellationToken ct = default) =>
        CapNhat(_repo, oId, trangThai, NguonTrangThai.Admin, _clock.BayGio, ct);

    /// <summary>Dùng chung cho Admin (nguồn ADMIN) và AI (nguồn AI, qua IBaiXeModule).</summary>
    internal static async Task CapNhat(IBaiXeRepository repo, int oId, string? trangThai, string nguon, DateTime bayGio, CancellationToken ct)
    {
        _ = await repo.TimOAsync(oId, ct) ?? throw Loi.KhongThay("KHONG_TIM_THAY_O", "Không tìm thấy ô.");
        var t = await repo.TimTrangThaiOAsync(oId, ct);
        if (t is null) repo.Them(TrangThaiO.Tao(oId, trangThai!, nguon, bayGio));
        else t.CapNhat(trangThai, nguon, bayGio);
        await repo.LuuAsync(ct);
    }
}

// ---------------- Camera ----------------

public class ThemCameraUseCase
{
    private readonly IBaiXeRepository _repo;
    public ThemCameraUseCase(IBaiXeRepository repo) => _repo = repo;

    public async Task<int> ThucHienAsync(CameraLenh lenh, CancellationToken ct = default)
    {
        _ = await _repo.TimKhuAsync(lenh.KhuId, ct) ?? throw Loi.KhongThay("KHONG_TIM_THAY_KHU", "Không tìm thấy khu.");
        var c = Camera.Tao(lenh.KhuId, lenh.Ten, lenh.NguonAnh, lenh.DangBat);
        c.DatVaiTro(lenh.VaiTro);
        if (await _repo.TrungTenCameraAsync(c.Ten, null, ct)) throw Loi.XungDot("TRUNG_TEN_CAMERA", $"Đã có camera tên \"{c.Ten}\".");
        _repo.Them(c);
        await _repo.LuuAsync(ct);
        return c.Id;
    }
}

public class SuaCameraUseCase
{
    private readonly IBaiXeRepository _repo;
    public SuaCameraUseCase(IBaiXeRepository repo) => _repo = repo;

    public async Task ThucHienAsync(int id, CameraLenh lenh, CancellationToken ct = default)
    {
        var c = await _repo.TimCameraAsync(id, ct) ?? throw Loi.KhongThay("KHONG_TIM_THAY_CAMERA", "Không tìm thấy camera.");
        if (lenh.KhuId != c.KhuId) throw Loi.XungDot("KHONG_DOI_KHU", "Không chuyển camera sang khu khác được.");
        c.Sua(lenh.Ten, lenh.NguonAnh, lenh.DangBat);
        if (lenh.VaiTro is not null) c.DatVaiTro(lenh.VaiTro);
        if (await _repo.TrungTenCameraAsync(c.Ten, id, ct)) throw Loi.XungDot("TRUNG_TEN_CAMERA", $"Đã có camera tên \"{c.Ten}\".");
        await _repo.LuuAsync(ct);
    }
}

public class XoaCameraUseCase
{
    private readonly IBaiXeRepository _repo;
    public XoaCameraUseCase(IBaiXeRepository repo) => _repo = repo;

    public async Task ThucHienAsync(int id, CancellationToken ct = default)
    {
        var c = await _repo.TimCameraAsync(id, ct) ?? throw Loi.KhongThay("KHONG_TIM_THAY_CAMERA", "Không tìm thấy camera.");
        if (await _repo.DemOCuaCameraAsync(id, ct) > 0)
            throw Loi.XungDot("CAMERA_CON_O", "Còn ô gắn với camera này. Bỏ gắn camera khỏi các ô trước.");
        _repo.Xoa(c);
        await _repo.LuuAsync(ct);
    }
}

// ---------------- Khách xem chỗ trống ----------------

/// <summary>Số ô trống theo khu, cho khách xem trước khi vào bãi. Không lộ biển số hay chủ xe nào.</summary>
public class LayChoTrongUseCase
{
    private readonly ICauHinhBaiQuery _q;
    private readonly IClock _clock;
    public LayChoTrongUseCase(ICauHinhBaiQuery q, IClock clock) { _q = q; _clock = clock; }

    public async Task<ChoTrongDto> ThucHienAsync(CancellationToken ct = default)
    {
        var bayGio = _clock.BayGio;
        var khu = await _q.LayKhuAsync(ct);
        var o = await _q.LayOAsync(ct);
        var tt = (await _q.LayTrangThaiOAsync(ct)).ToDictionary(t => t.OId);
        // Khu tạm tắt (VD Khu A, B ô tô chưa làm) không hiện cho khách
        var ds = khu.Where(k => k.DangDung).OrderBy(k => k.Ten).Select(k =>
        {
            var dsO = o.Where(x => x.KhuId == k.Id).OrderBy(x => x.MaO)
                       .Select(x => new OChoTrongDto(x.Id, x.MaO, TrangThaiO.HienThi(tt.GetValueOrDefault(x.Id), bayGio))).ToList();
            return new KhuChoTrongDto(k.Id, k.Ten, k.LoaiXeId, dsO.Count,
                dsO.Count(x => x.TrangThai == TrangThaiOMa.Trong), dsO.Count(x => x.TrangThai == TrangThaiOMa.CoXe),
                dsO.Count(x => x.TrangThai == TrangThaiOMa.KhongRo), dsO);
        }).ToList();
        return new ChoTrongDto(bayGio, ds);
    }
}

/// <summary>Cài đặt cổng công khai IBaiXeModule: TimXe hỏi khu, camera, đường đi; AI ô trống (sau này) báo trạng thái ô.</summary>
public class BaiXeModule : IBaiXeModule
{
    private readonly IBaiXeRepository _repo;
    private readonly ICauHinhBaiQuery _q;
    private readonly IClock _clock;
    public BaiXeModule(IBaiXeRepository repo, ICauHinhBaiQuery q, IClock clock) { _repo = repo; _q = q; _clock = clock; }

    public Task CapNhatTrangThaiOAsync(int oId, string trangThai, CancellationToken ct = default) =>
        DatTrangThaiOUseCase.CapNhat(_repo, oId, trangThai, NguonTrangThai.Ai, _clock.BayGio, ct);

    public async Task<IReadOnlyList<KhuThongTin>> LayKhuAsync(CancellationToken ct = default)
    {
        var khu = await _q.LayKhuAsync(ct);
        var (nut, kc, _) = await DoThiAsync(ct);
        return khu.OrderBy(k => k.Ten).Select(k =>
        {
            var dau = nut.FirstOrDefault(n => n.KhuId == k.Id);
            double? d = dau is not null && kc.TryGetValue(dau.Id, out var x) ? Math.Round(x, 2) : null;
            return new KhuThongTin(k.Id, k.Ten, k.LoaiXeId, k.SucChua, d, k.MaSoDo, k.DangDung);
        }).ToList();
    }

    public async Task<CameraThongTin?> LayCameraAsync(int cameraId, CancellationToken ct = default)
    {
        var c = (await _q.LayCameraAsync(ct)).FirstOrDefault(c => c.Id == cameraId);
        return c is null ? null : new CameraThongTin(c.Id, c.KhuId, c.Ten, c.VaiTro, c.DangBat);
    }

    public async Task<DuongDiDto?> TimDuongAsync(int khuId, CancellationToken ct = default)
    {
        var (nut, kc, truoc) = await DoThiAsync(ct);
        var cong = Cong(nut);
        var dau = nut.FirstOrDefault(n => n.KhuId == khuId);
        if (cong is null || dau is null) return null;
        var ds = DuongNganNhat.Duong(cong.Id, dau.Id, truoc);
        if (ds is null) return null;
        var theoId = nut.ToDictionary(n => n.Id);
        return new DuongDiDto(khuId, Math.Round(kc[dau.Id], 2),
            ds.Select(id => theoId[id]).Select(n => new DiemDuong(n.Id, n.Ten, n.X, n.Y)).ToList());
    }

    /// <summary>Cổng vào = nút tên "Lối vào"; không có thì nút không gắn khu có id nhỏ nhất.</summary>
    internal static NutRow? Cong(IReadOnlyList<NutRow> nut) =>
        nut.FirstOrDefault(n => n.KhuId is null && n.Ten.Trim().Equals("Lối vào", StringComparison.OrdinalIgnoreCase))
        ?? nut.Where(n => n.KhuId is null).OrderBy(n => n.Id).FirstOrDefault();

    private async Task<(IReadOnlyList<NutRow> Nut, IReadOnlyDictionary<int, double> Kc, IReadOnlyDictionary<int, int> Truoc)> DoThiAsync(CancellationToken ct)
    {
        var nut = await _q.LayNutAsync(ct);
        var cong = Cong(nut);
        if (cong is null) return (nut, new Dictionary<int, double>(), new Dictionary<int, int>());
        var (kc, truoc) = DuongNganNhat.Tinh(cong.Id, await _q.LayCanhAsync(ct));
        return (nut, kc, truoc);
    }
}

internal static class LoaiXe
{
    /// <summary>Loại xe là dữ liệu của GoiCho (bảng loai_xe): hỏi qua IGoiChoModule, không đọc bảng.</summary>
    public static IReadOnlyCollection<int> Ids(IReadOnlyList<SucChuaLoaiXe> loai) => loai.Select(s => s.LoaiXeId).ToList();

    /// <summary>Mã loại xe máy là "XE_MAY" (bảng loai_xe của GoiCho).</summary>
    public static bool LaXeMay(IReadOnlyList<SucChuaLoaiXe> loai, int loaiXeId) =>
        loai.Any(l => l.LoaiXeId == loaiXeId && l.LoaiXe == "XE_MAY");
}
