using NhaXe.Modules.CongRaVao.Contracts;
using NhaXe.Modules.CongRaVao.Domain;
using NhaXe.Modules.GoiCho.Contracts;
using NhaXe.Modules.ThongBao.Contracts;
using Microsoft.Extensions.Logging;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.CongRaVao.Application;

// Use case phía cổng (đề cương v10, UC-U06, UC-U07). Mỗi class một việc (SRP), nhận interface qua constructor (DIP).
// Hỏi GoiCho qua IGoiChoModule (xe có đăng ký, đã duyệt, còn gói, còn nợ không). KHÔNG đọc bảng xe / dang_ky_cho.
// Báo "xe đã vào / đã ra" bằng sự kiện (IPhatSuKien): GoiCho giữ bản sao xe trong bãi, TimXe chọn khu và giữ suất.
// CongRaVao KHÔNG gọi TimXe; màn hình cổng hỏi TimXe "mời vào khu nào" bằng API riêng.

/// <summary>Kết quả tra xe ở cổng: xe tìm được, có phải khớp gần đúng không, hoặc lý do dừng lại.</summary>
internal record TraXe(XeTaiCong? Xe, bool GanDung, string? LyDo, IReadOnlyList<string>? UngVien);

/// <summary>Tra biển số: khớp chính xác trước, rồi gần đúng (lệch ≤ 1 ký tự, chỉ nhận khi đúng một ứng viên).</summary>
internal static class TraBienSo
{
    public static async Task<TraXe> TimAsync(IGoiChoModule goiCho, KetQuaNhanDien nd, IReadOnlyCollection<int>? chiTrong, CancellationToken ct)
    {
        XeTaiCong? xe = null;
        try { xe = await goiCho.TimXeTaiCongAsync(nd.BienSo!, ct); }
        catch (LoiNghiepVu e) when (e.Loai == LoaiLoi.DuLieuKhongHopLe) { if (nd.GoTay) return new(null, false, "Biển số sai định dạng", null); }
        if (xe is not null && (chiTrong is null || chiTrong.Contains(xe.XeId))) return new(xe, false, null, null);

        // Admin đã gõ tay thì không đoán thay Admin; AI đọc thì thử gần đúng
        if (nd.GoTay) return new(xe, false, null, null);
        var gan = await goiCho.TimXeGanDungAsync(nd.BienSo!, chiTrong, ct);
        if (gan.Xe is not null) return new(gan.Xe, true, null, gan.UngVien);
        if (gan.UngVien.Count > 1)
            return new(null, false, "Biển gần giống nhiều xe: Admin chọn đúng biển số", gan.UngVien);
        return new(xe, false, null, null);
    }
}

/// <summary>Màn hình camera tự quét (tuDong): khung hình nào nên bỏ qua, không ghi nhật ký.</summary>
internal static class TuQuet
{
    /// <summary>Không thấy biển; hoặc biển đã có nhật ký ở cổng này trong 30 giây (xe còn đứng trước camera).</summary>
    public static async Task<bool> NenBoQuaAsync(ICongRaVaoRepository repo, string cong, KetQuaNhanDien nd, DateTime bayGio, CancellationToken ct)
    {
        if (nd.CheDoThuCong) return false;
        var bien = nd.BienSo ?? nd.UngVien?.FirstOrDefault();
        if (bien is null) return true;
        return await repo.CoSuKienTuAsync(cong, bien, bayGio - XeVaoUseCase.ChongLap, ct);
    }

    /// <summary>
    /// Kiểm tra lần hai bằng biển số CỦA XE sau khi tra (khớp gần đúng): camera tự quét mỗi giây, AI có thể đọc lệch 1 ký tự
    /// ở mỗi khung (36C239396, 36C23939S...) nên khoá ở lần kiểm tra đầu khác nhau, nhưng đều khớp về cùng một xe.
    /// Lỗi thật 19/11/2026 03:42: xe 36C239395 bị từ chối + gửi thông báo 6 lần trong 7 giây.
    /// </summary>
    public static async Task<bool> DaXuLyXeNayAsync(ICongRaVaoRepository repo, AnhCong anh, string cong, KetQuaNhanDien nd, string? bienXe,
                                                   DateTime bayGio, CancellationToken ct) =>
        anh.TuDong && bienXe is not null && bienXe != nd.BienSo
        && await repo.CoSuKienTuAsync(cong, bienXe, bayGio - XeVaoUseCase.ChongLap, ct);
}

/// <summary>Xe tới cổng VÀO: đọc biển → tra xe → kiểm gói, loại biển, trùng lượt → mở lượt gửi hoặc từ chối. Lần nào cũng ghi nhật ký.</summary>
public class XeVaoUseCase
{
    private readonly INhanDienBienSo _ai;
    private readonly IGoiChoModule _goiCho;
    private readonly ICongRaVaoRepository _repo;
    private readonly IPhatSuKien _phat;
    private readonly IThongBaoModule _thongBao;
    private readonly IClock _clock;
    private readonly ILogger? _log;
    private readonly IKhoAnhCong? _kho;

    public XeVaoUseCase(INhanDienBienSo ai, IGoiChoModule goiCho, ICongRaVaoRepository repo, IPhatSuKien phat,
                        IThongBaoModule thongBao, IClock clock, ILogger<XeVaoUseCase>? log = null, IKhoAnhCong? kho = null)
    { _ai = ai; _goiCho = goiCho; _repo = repo; _phat = phat; _thongBao = thongBao; _clock = clock; _log = log; _kho = kho; }

    /// <summary>Chống đọc lặp của màn hình camera (đề cương v10): 30 giây cho cùng một biển.</summary>
    public static readonly TimeSpan ChongLap = TimeSpan.FromSeconds(30);

    public async Task<KetQuaCongDto> ThucHienAsync(AnhCong anh, CancellationToken ct = default)
    {
        var nd = await _ai.DocAsync(anh, ct);
        var kq = await XuLyAsync(anh, nd, ct);
        return nd.Khung is null || kq.KhongThayBien ? kq : kq with { KhungBien = nd.Khung };
    }

    private Task<bool> NenBoQuaAsync(KetQuaNhanDien nd, DateTime bayGio, CancellationToken ct) =>
        TuQuet.NenBoQuaAsync(_repo, SuKienCong.Vao, nd, bayGio, ct);

    private async Task<KetQuaCongDto> XuLyAsync(AnhCong anh, KetQuaNhanDien nd, CancellationToken ct)
    {
        var bayGio = _clock.BayGio;
        if (anh.TuDong && await NenBoQuaAsync(nd, bayGio, ct))
            return new KetQuaCongDto(SuKienCong.Vao, false, false, nd.BienSo, "Bỏ qua khung hình", KhongThayBien: true);
        if (nd.CheDoThuCong)
            return await TuChoi(null, null, nd.LyDo!, bayGio, ct, thuCong: true);
        if (nd.BienSo is null) return await TuChoi(null, nd.DoTinCay, nd.LyDo ?? "Không đọc được biển số", bayGio, ct, nd.UngVien);

        var tra = await TraBienSo.TimAsync(_goiCho, nd, null, ct);
        if (tra.LyDo is not null) return await TuChoi(nd.BienSo, nd.DoTinCay, tra.LyDo, bayGio, ct, tra.UngVien);
        var xe = tra.Xe;
        if (await TuQuet.DaXuLyXeNayAsync(_repo, anh, SuKienCong.Vao, nd, xe?.BienSo, bayGio, ct))
            return new KetQuaCongDto(SuKienCong.Vao, false, false, xe!.BienSo, "Bỏ qua khung hình", KhongThayBien: true);

        // Thứ tự kiểm tra = thứ tự lý do hiện trên màn hình cổng (đề cương v10, UC-U06 bước 3)
        var lyDo = xe switch
        {
            null => "Xe chưa đăng ký",
            { DaDuyet: false } => "Xe chưa được duyệt",
            { CoGoiHieuLuc: false } => NoiDungCong.LyDoKhongCoGoi(xe.HetHanLuc, bayGio),
            _ => null
        };
        if (lyDo is not null)
        {
            var kq = await TuChoi(xe?.BienSo ?? nd.BienSo, nd.DoTinCay, lyDo, bayGio, ct);
            if (xe is { DaDuyet: true, CoGoiHieuLuc: false }) // lời mời gia hạn cho người dùng xe
                await BaoSauKhiLuu.ChayAsync(async () => await _thongBao.GuiNhieuAsync(await _goiCho.LayNguoiDungXeAsync(xe.XeId, ct),
                    LoaiThongBao.HetHan, NoiDungCong.MoiMuaGoi(xe.BienSo, xe.HetHanLuc, bayGio),
                    new DichThongBao(HanhDongThongBao.MuaGoi, xe.XeId), ct), _log);
            return kq;
        }

        // Kiểm chéo loại biển AI đọc với loại xe đã đăng ký (gõ tay hoặc loại biển không rõ thì bỏ qua)
        if (!nd.GoTay && nd.LoaiBien != "KHONG_RO" && nd.LoaiBien != xe!.LoaiXe)
            return await TuChoi(xe.BienSo, nd.DoTinCay,
                $"Loại biển ({NoiDungCong.TenLoai(nd.LoaiBien)}) không trùng loại xe đã đăng ký ({NoiDungCong.TenLoai(xe.LoaiXe)}): Admin xác nhận",
                bayGio, ct, new[] { xe.BienSo });
        if (await _repo.TimLuotMoCuaXeAsync(xe!.XeId, ct) is not null)
        {
            var kq = await TuChoi(xe.BienSo, nd.DoTinCay, "Xe này đang có lượt gửi chưa kết thúc (có thể biển bị trùng / làm giả)", bayGio, ct);
            await BaoSauKhiLuu.ChayAsync(() => _thongBao.GuiChoMoiAdminAsync(LoaiThongBao.DoiSoat, NoiDungCong.TrungLuot(xe.BienSo),
                new DichThongBao(HanhDongThongBao.XemCongVao), ct), _log);
            return kq;
        }

        // Ảnh đối chứng: chỉ lưu khi xe được cho vào (khung hình bị từ chối không giữ lại)
        var anhVao = anh.Anh is not null && anh.LoaiAnh is not null && _kho is not null
            ? await _kho.LuuAsync(anh.Anh, anh.LoaiAnh, bayGio, ct) : null;
        var luot = LuotGui.Mo(xe.XeId, bayGio, nd.DoTinCay, anhVao);
        _repo.Them(luot);
        _repo.Them(nd.GoTay && anh.AdminId is int admin
            ? SuKienCong.MoTayVao(xe.BienSo, luot, "nhập / xác nhận biển số tại cổng", admin, bayGio)
            : SuKienCong.ChoVao(xe.BienSo, nd.DoTinCay, luot, bayGio));
        await _repo.LuuAsync(ct); // một transaction: lượt gửi + nhật ký

        // SAU khi lưu xong mới báo ra ngoài. TimXe nghe XeDaVao để chọn khu và giữ suất 5 phút.
        await BaoSauKhiLuu.ChayAsync(() => _phat.PhatAsync(new XeDaVao(luot.Id, xe.XeId, bayGio), ct), _log);
        await BaoSauKhiLuu.ChayAsync(async () =>
            await _thongBao.GuiNhieuAsync(await _goiCho.LayNguoiDungXeAsync(xe.XeId, ct), LoaiThongBao.Vao, NoiDungCong.DaVao(xe.BienSo, bayGio),
                new DichThongBao(HanhDongThongBao.XemLuot, luot.Id), ct), _log);
        return new KetQuaCongDto(SuKienCong.Vao, true, false, xe.BienSo, "Mời xe vào", luot.Id, DoTinCay: nd.DoTinCay,
                                 LoaiBien: nd.LoaiBien, KhopGanDung: tra.GanDung, XeId: xe.XeId,
                                 ThoiGianVao: luot.ThoiGianVao, CoAnhVao: anhVao is not null);
    }

    private async Task<KetQuaCongDto> TuChoi(string? bienSo, decimal? tinCay, string lyDo, DateTime bayGio, CancellationToken ct,
                                             IReadOnlyList<string>? ungVien = null, bool thuCong = false)
    {
        _repo.Them(SuKienCong.TuChoiXe(SuKienCong.Vao, bienSo, tinCay, null, lyDo, null, bayGio));
        await _repo.LuuAsync(ct);
        return new KetQuaCongDto(SuKienCong.Vao, false, false, bienSo, lyDo, UngVien: ungVien, DoTinCay: tinCay,
                                 CheDoThuCong: thuCong, CanAdmin: ungVien is { Count: > 0 } || thuCong);
    }
}

/// <summary>
/// Xe tới cổng RA (đề cương v10, UC-U07): đọc biển → tìm lượt gửi đang mở (chính xác, rồi gần đúng với xe đang trong bãi)
/// → tạo yêu cầu xác nhận 60 giây gửi tới mọi người có quyền dùng xe. Cổng CHƯA mở ở bước này.
/// Phí quá hạn xét SAU khi người dùng xác nhận (KetThucYeuCau): còn nợ thì chờ thanh toán.
/// </summary>
public class XeRaUseCase
{
    private readonly INhanDienBienSo _ai;
    private readonly IGoiChoModule _goiCho;
    private readonly ICongRaVaoRepository _repo;
    private readonly ILuotGuiQuery _dangMo;
    private readonly ICauHinhCong _cauHinh;
    private readonly KetThucYeuCau _ketThuc;
    private readonly IThongBaoModule _thongBao;
    private readonly IClock _clock;
    private readonly ILogger? _log;
    private readonly IKhoAnhCong? _kho;

    public XeRaUseCase(INhanDienBienSo ai, IGoiChoModule goiCho, ICongRaVaoRepository repo, ILuotGuiQuery dangMo, ICauHinhCong cauHinh,
                       KetThucYeuCau ketThuc, IThongBaoModule thongBao, IClock clock, ILogger<XeRaUseCase>? log = null, IKhoAnhCong? kho = null)
    { _ai = ai; _goiCho = goiCho; _repo = repo; _dangMo = dangMo; _cauHinh = cauHinh; _ketThuc = ketThuc; _thongBao = thongBao; _clock = clock; _log = log; _kho = kho; }

    public async Task<KetQuaCongDto> ThucHienAsync(AnhCong anh, CancellationToken ct = default)
    {
        var nd = await _ai.DocAsync(anh, ct);
        var kq = await XuLyAsync(anh, nd, ct);
        if (kq.KhongThayBien) return kq;
        return kq with { KhungBien = nd.Khung ?? kq.KhungBien, DoTinCay = kq.DoTinCay ?? nd.DoTinCay };
    }

    private async Task<KetQuaCongDto> XuLyAsync(AnhCong anh, KetQuaNhanDien nd, CancellationToken ct)
    {
        var bayGio = _clock.BayGio;
        if (anh.TuDong && await TuQuet.NenBoQuaAsync(_repo, SuKienCong.Ra, nd, bayGio, ct))
            return new KetQuaCongDto(SuKienCong.Ra, false, false, nd.BienSo, "Bỏ qua khung hình", KhongThayBien: true);
        if (nd.CheDoThuCong) return await TuChoi(null, null, null, nd.LyDo!, bayGio, ct, thuCong: true);
        if (nd.BienSo is null) return await TuChoi(null, nd.DoTinCay, null, nd.LyDo ?? "Không đọc được biển số", bayGio, ct, nd.UngVien);

        var trongBai = (await _dangMo.LayDangMoAsync(null, ct)).Select(l => l.XeId).ToHashSet();
        var tra = await TraBienSo.TimAsync(_goiCho, nd, trongBai, ct);
        if (tra.LyDo is not null) return await TuChoi(nd.BienSo, nd.DoTinCay, null, tra.LyDo, bayGio, ct, tra.UngVien);
        var xe = tra.Xe;
        if (await TuQuet.DaXuLyXeNayAsync(_repo, anh, SuKienCong.Ra, nd, xe?.BienSo, bayGio, ct))
            return new KetQuaCongDto(SuKienCong.Ra, false, false, xe!.BienSo, "Bỏ qua khung hình", KhongThayBien: true);
        var luot = xe is null ? null : await _repo.TimLuotMoCuaXeAsync(xe.XeId, ct);
        if (xe is null || luot is null)
        {
            var kq = await TuChoi(xe?.BienSo ?? nd.BienSo, nd.DoTinCay, null, "Không thấy lượt gửi đang mở của xe này: đã báo Admin", bayGio, ct);
            await BaoSauKhiLuu.ChayAsync(() => _thongBao.GuiChoMoiAdminAsync(LoaiThongBao.DoiSoat,
                NoiDungCong.KhongThayLuot(xe?.BienSo ?? nd.BienSo), new DichThongBao(HanhDongThongBao.XuLyCongRa), ct), _log);
            return kq;
        }

        // Đã xác nhận "Đúng là tôi" nhưng còn nợ: quét lại thì mở luôn nếu vừa trả xong
        if (luot.TrangThai == TrangThaiLuot.ChoThanhToan)
            return await _ketThuc.MoSauThanhToanAsync(luot, xe.BienSo, ct)
                ? new KetQuaCongDto(SuKienCong.Ra, true, false, xe.BienSo, "Đã trả phí quá hạn, mời xe ra", luot.Id, XeId: xe.XeId,
                                    ThoiGianVao: luot.ThoiGianVao, CoAnhVao: luot.AnhVao is not null)
                : new KetQuaCongDto(SuKienCong.Ra, false, false, xe.BienSo,
                    $"Chờ thanh toán phí quá hạn {NoiDungCong.Tien(await _goiCho.LaySoTienNoAsync(xe.XeId, ct))} đ trên ứng dụng",
                    luot.Id, XeId: xe.XeId, ChoThanhToan: true, ThoiGianVao: luot.ThoiGianVao, CoAnhVao: luot.AnhVao is not null);

        // Xe đứng lâu trước camera: trả về đúng yêu cầu đang chờ, không tạo thêm, không gửi thêm thông báo (idempotent)
        var dangCho = await _repo.TimYeuCauDangChoAsync(luot.Id, ct);
        if (dangCho is not null)
        {
            if (!dangCho.HetHan(bayGio))
                return DangChoXacNhan(xe.BienSo, luot, dangCho, bayGio, "Đang chờ xác nhận");
            await _ketThuc.LuuVaBaoAsync(dangCho, luot, ct); // quá hạn mà tác vụ nền chưa kịp xử lý: kết thúc luôn
        }

        var soGiay = await _cauHinh.LaySoGiayChoXacNhanAsync(ct);
        var yc = YeuCauXacNhan.Tao(luot.Id, bayGio, soGiay);
        var anhRa = anh.Anh is not null && anh.LoaiAnh is not null && _kho is not null
            ? await _kho.LuuAsync(anh.Anh, anh.LoaiAnh, bayGio, ct) : null;
        luot.BatDauChoXacNhan(nd.DoTinCay, anhRa);
        _repo.Them(yc);
        await _repo.LuuAsync(ct);

        await BaoSauKhiLuu.ChayAsync(async () =>
            await _thongBao.GuiNhieuAsync(await _goiCho.LayNguoiDungXeAsync(xe.XeId, ct), LoaiThongBao.Ra,
                                          NoiDungCong.ChoXacNhan(xe.BienSo, soGiay), new DichThongBao(HanhDongThongBao.XacNhanRa, luot.Id), ct), _log);
        return DangChoXacNhan(xe.BienSo, luot, yc, bayGio, "Đang chờ xác nhận") with { KhopGanDung = tra.GanDung };
    }

    private static KetQuaCongDto DangChoXacNhan(string bienSo, LuotGui luot, YeuCauXacNhan yc, DateTime bayGio, string tin) =>
        new(SuKienCong.Ra, false, true, bienSo, tin, luot.Id, yc.Id, yc.HetHanLuc, yc.SoGiayConLai(bayGio), XeId: luot.XeId,
            ThoiGianVao: luot.ThoiGianVao, CoAnhVao: luot.AnhVao is not null);

    private async Task<KetQuaCongDto> TuChoi(string? bienSo, decimal? tinCay, int? luotId, string lyDo, DateTime bayGio, CancellationToken ct,
                                             IReadOnlyList<string>? ungVien = null, bool thuCong = false)
    {
        _repo.Them(SuKienCong.TuChoiXe(SuKienCong.Ra, bienSo, tinCay, luotId, lyDo, null, bayGio));
        await _repo.LuuAsync(ct);
        return new KetQuaCongDto(SuKienCong.Ra, false, false, bienSo, lyDo, luotId, UngVien: ungVien, DoTinCay: tinCay,
                                 CheDoThuCong: thuCong, CanAdmin: true);
    }
}

/// <summary>
/// Việc làm SAU khi dữ liệu đã lưu (phát sự kiện, gửi thông báo): lỗi thì ghi log rồi bỏ qua, không làm hỏng kết quả đã lưu.
/// Huỷ yêu cầu (OperationCanceledException) vẫn ném ra như thường.
/// </summary>
internal static class BaoSauKhiLuu
{
    public static async Task ChayAsync(Func<Task> viec, ILogger? log)
    {
        try { await viec(); }
        catch (Exception e) when (e is not OperationCanceledException)
        { log?.LogWarning(e, "Việc sau khi lưu bị lỗi; dữ liệu cổng vẫn giữ nguyên"); }
    }
}
