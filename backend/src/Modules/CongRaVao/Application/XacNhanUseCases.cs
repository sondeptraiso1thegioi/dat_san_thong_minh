using NhaXe.Modules.CongRaVao.Contracts;
using NhaXe.Modules.CongRaVao.Domain;
using NhaXe.Modules.GoiCho.Contracts;
using NhaXe.Modules.TaiKhoan.Contracts;
using NhaXe.Modules.ThongBao.Contracts;
using Microsoft.Extensions.Logging;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.CongRaVao.Application;

/// <summary>
/// Bước chung sau khi một yêu cầu xác nhận đổi trạng thái (đề cương v10, UC-U08):
/// - "Đúng là tôi", hết nợ: mở barrier, đóng lượt, xoá khu.
/// - "Đúng là tôi", còn phí quá hạn: lượt sang CHO_THANH_TOAN, trả phí xong mới mở (NgheDaTraPhi).
/// - "Không phải tôi": NGHI_MAT, báo Admin.
/// - Hết 60 giây: không mở, lượt về DANG_GUI, chuyển Admin xử lý (Admin mở tay có lý do).
/// Cập nhật lượt + ghi nhật ký + lưu trong MỘT transaction, rồi mới phát sự kiện và gửi thông báo.
/// </summary>
public class KetThucYeuCau
{
    private readonly ICongRaVaoRepository _repo;
    private readonly IGoiChoModule _goiCho;
    private readonly ITaiKhoanModule _taiKhoan;
    private readonly IThongBaoModule _thongBao;
    private readonly IPhatSuKien _phat;
    private readonly IClock _clock;
    private readonly ILogger? _log;

    public KetThucYeuCau(ICongRaVaoRepository repo, IGoiChoModule goiCho, ITaiKhoanModule taiKhoan, IThongBaoModule thongBao,
                         IPhatSuKien phat, IClock clock, ILogger<KetThucYeuCau>? log = null)
    { _repo = repo; _goiCho = goiCho; _taiKhoan = taiKhoan; _thongBao = thongBao; _phat = phat; _clock = clock; _log = log; }

    private async Task<string> BienSoAsync(int xeId, CancellationToken ct) =>
        (await _goiCho.LayTinhTrangXeAsync(new[] { xeId }, ct)).GetValueOrDefault(xeId)?.BienSo ?? $"#{xeId}";

    private async Task<string> TenAsync(int? taiKhoanId, CancellationToken ct) =>
        taiKhoanId is int id ? (await _taiKhoan.LayHoTenAsync(new[] { id }, ct)).GetValueOrDefault(id, $"#{id}") : "?";

    public async Task LuuVaBaoAsync(YeuCauXacNhan yc, LuotGui luot, CancellationToken ct)
    {
        var bayGio = _clock.BayGio;
        var bienSo = await BienSoAsync(luot.XeId, ct);
        var ten = await TenAsync(yc.ChuXeTraLoiId, ct);
        decimal no = 0;

        if (yc.KetQua == KetQuaXacNhan.DongY)
        {
            no = await _goiCho.LaySoTienNoAsync(luot.XeId, ct);
            if (no > 0) luot.ChoThanhToan();
            else
            {
                luot.Ra(bayGio);
                _repo.Them(SuKienCong.ChoRa(bienSo, luot.DoTinCayRa, luot.Id, NoiDungCong.DaDuocXacNhan(ten), bayGio));
            }
        }
        else if (yc.KetQua is KetQuaXacNhan.TuChoi or KetQuaXacNhan.HetHan)
        {
            luot.HuyChoRa(nghiMat: yc.NguoiDungXeBaoKhongPhai);
            _repo.Them(SuKienCong.TuChoiXe(SuKienCong.Ra, bienSo, luot.DoTinCayRa, luot.Id, yc.LyDo!, null, bayGio));
        }
        await _repo.LuuAsync(ct); // yêu cầu + lượt gửi + nhật ký: cùng một transaction

        if (yc.DangCho) return;
        // Từ đây dữ liệu đã lưu (barrier có thể đã mở): lỗi phát sự kiện / gửi thông báo chỉ ghi log, không trả 500.
        await BaoSauKhiLuu.ChayAsync(() => BaoAsync(yc, luot, bienSo, ten, no, ct), _log);
    }

    private async Task BaoAsync(YeuCauXacNhan yc, LuotGui luot, string bienSo, string ten, decimal no, CancellationToken ct)
    {
        var nguoiDung = await _goiCho.LayNguoiDungXeAsync(luot.XeId, ct);
        switch (yc.KetQua)
        {
            case KetQuaXacNhan.DongY when luot.TrangThai == TrangThaiLuot.ChoThanhToan:
                await _thongBao.GuiNhieuAsync(nguoiDung, LoaiThongBao.ChoThanhToan, NoiDungCong.ChoThanhToan(bienSo, ten, no),
                    new DichThongBao(HanhDongThongBao.TraPhi, luot.XeId), ct);
                break;
            case KetQuaXacNhan.DongY:
                await _phat.PhatAsync(new XeDaRa(luot.Id, luot.XeId, luot.ThoiGianRa!.Value), ct);
                await _thongBao.GuiNhieuAsync(nguoiDung, LoaiThongBao.Ra, NoiDungCong.DaRaBoi(bienSo, ten, luot.ThoiGianRa.Value),
                    new DichThongBao(HanhDongThongBao.XemLuot, luot.Id), ct);
                break;
            case KetQuaXacNhan.TuChoi:
                await _thongBao.GuiChoMoiAdminAsync(LoaiThongBao.NghiMat, NoiDungCong.NghiMat(bienSo), new DichThongBao(HanhDongThongBao.XuLyCongRa, luot.Id), ct);
                await _thongBao.GuiNhieuAsync(nguoiDung, LoaiThongBao.NghiMat, NoiDungCong.KhongMo(bienSo, yc.LyDo!), new DichThongBao(HanhDongThongBao.XemLuot, luot.Id), ct);
                break;
            default: // hết 60 giây: chuyển Admin
                await _thongBao.GuiNhieuAsync(nguoiDung, LoaiThongBao.Ra, NoiDungCong.KhongMo(bienSo, yc.LyDo!), new DichThongBao(HanhDongThongBao.XemLuot, luot.Id), ct);
                await _thongBao.GuiChoMoiAdminAsync(LoaiThongBao.Ra, NoiDungCong.HetGioChoAdmin(bienSo), new DichThongBao(HanhDongThongBao.XuLyCongRa, luot.Id), ct);
                break;
        }
    }

    /// <summary>Lượt đang CHO_THANH_TOAN: hết nợ thì mở barrier ngay. Trả về true nếu đã mở.</summary>
    public async Task<bool> MoSauThanhToanAsync(LuotGui luot, string bienSo, CancellationToken ct)
    {
        if (luot.TrangThai != TrangThaiLuot.ChoThanhToan) return false;
        if (await _goiCho.LaySoTienNoAsync(luot.XeId, ct) > 0) return false;
        var bayGio = _clock.BayGio;
        luot.Ra(bayGio);
        _repo.Them(SuKienCong.ChoRa(bienSo, luot.DoTinCayRa, luot.Id, "Đã trả phí quá hạn, mở cổng", bayGio));
        await _repo.LuuAsync(ct);
        await BaoSauKhiLuu.ChayAsync(async () =>
        {
            await _phat.PhatAsync(new XeDaRa(luot.Id, luot.XeId, luot.ThoiGianRa!.Value), ct);
            await _thongBao.GuiNhieuAsync(await _goiCho.LayNguoiDungXeAsync(luot.XeId, ct), LoaiThongBao.Ra,
                                          NoiDungCong.DaRa(bienSo, luot.ThoiGianRa.Value), new DichThongBao(HanhDongThongBao.XemLuot, luot.Id), ct);
        }, _log);
        return true;
    }
}

/// <summary>Chạy lại tối đa 3 lần khi người khác vừa sửa cùng yêu cầu (khoá lạc quan báo YEU_CAU_DA_THAY_DOI).</summary>
internal static class ThuLai
{
    public const string MaXungDot = "YEU_CAU_DA_THAY_DOI";

    public static async Task ChayAsync(ICongRaVaoRepository repo, Func<Task> viec)
    {
        for (var lan = 1; ; lan++)
        {
            try { await viec(); return; }
            catch (LoiNghiepVu e) when (e.Ma == MaXungDot && lan < 3) { repo.BoTheoDoi(); }
        }
    }
}

/// <summary>
/// Người có quyền dùng xe (chủ xe, hoặc người cùng nhóm khi xe được chia sẻ) bấm "Đúng là tôi" / "Không phải tôi".
/// Người trả lời TRƯỚC có hiệu lực; người sau nhận lỗi "Đã được [tên] xác nhận".
/// Hai người bấm cùng lúc: ROWVERSION chặn người lưu sau, lần thử lại thấy yêu cầu đã có người trả lời.
/// </summary>
public class NguoiDungXeXacNhanUseCase
{
    private readonly ICongRaVaoRepository _repo;
    private readonly IGoiChoModule _goiCho;
    private readonly ITaiKhoanModule _taiKhoan;
    private readonly KetThucYeuCau _ketThuc;
    private readonly IClock _clock;

    public NguoiDungXeXacNhanUseCase(ICongRaVaoRepository repo, IGoiChoModule goiCho, ITaiKhoanModule taiKhoan, KetThucYeuCau ketThuc, IClock clock)
    { _repo = repo; _goiCho = goiCho; _taiKhoan = taiKhoan; _ketThuc = ketThuc; _clock = clock; }

    public Task ThucHienAsync(int taiKhoanId, int yeuCauId, bool dongY, CancellationToken ct = default) =>
        ThuLai.ChayAsync(_repo, async () =>
        {
            var yc = await _repo.TimYeuCauAsync(yeuCauId, ct);
            var luot = yc is null ? null : await _repo.TimLuotAsync(yc.LuotGuiId, ct);
            // Không có quyền dùng xe thì trả "không tìm thấy", không lộ là yêu cầu có tồn tại
            if (yc is null || luot is null || !(await _goiCho.LayNguoiDungXeAsync(luot.XeId, ct)).Contains(taiKhoanId))
                throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_TIM_THAY_YEU_CAU", "Không tìm thấy yêu cầu xác nhận.");
            if (yc.ChuXeTraLoiId is int nguoiTruoc)
            {
                var ten = (await _taiKhoan.LayHoTenAsync(new[] { nguoiTruoc }, ct)).GetValueOrDefault(nguoiTruoc, $"#{nguoiTruoc}");
                throw new LoiNghiepVu(LoaiLoi.XungDot, "DA_CO_NGUOI_TRA_LOI", NoiDungCong.DaDuocXacNhan(ten));
            }
            yc.NguoiDungXeTraLoi(taiKhoanId, dongY, _clock.BayGio);
            await _ketThuc.LuuVaBaoAsync(yc, luot, ct);
        });
}

/// <summary>
/// Admin mở barrier thủ công (UC-A10): bắt buộc lý do, ghi nhật ký cổng kèm Admin. Dùng khi hết 60 giây không ai trả lời,
/// xe nghi mất đã kiểm tra xong, AI / app lỗi. Yêu cầu đang chờ (nếu có) được đóng bằng cột quan_ly_*.
/// </summary>
public class AdminMoTayUseCase
{
    private readonly ICongRaVaoRepository _repo;
    private readonly IGoiChoModule _goiCho;
    private readonly IThongBaoModule _thongBao;
    private readonly IPhatSuKien _phat;
    private readonly IClock _clock;
    private readonly ILogger? _log;

    public AdminMoTayUseCase(ICongRaVaoRepository repo, IGoiChoModule goiCho, IThongBaoModule thongBao, IPhatSuKien phat, IClock clock,
                             ILogger<AdminMoTayUseCase>? log = null)
    { _repo = repo; _goiCho = goiCho; _thongBao = thongBao; _phat = phat; _clock = clock; _log = log; }

    public async Task<KetQuaCongDto> ThucHienAsync(int adminId, int luotGuiId, string? lyDo, CancellationToken ct = default)
    {
        var ld = (lyDo ?? "").Trim();
        if (ld.Length == 0)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "THIEU_LY_DO", "Mở barrier thủ công phải ghi lý do.");
        if (ld.Length > 200)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "LY_DO_QUA_DAI", "Lý do tối đa 200 ký tự.");

        LuotGui? luot = null;
        await ThuLai.ChayAsync(_repo, async () =>
        {
            var bayGio = _clock.BayGio;
            luot = await _repo.TimLuotAsync(luotGuiId, ct)
                   ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_TIM_THAY_LUOT", "Không tìm thấy lượt gửi.");
            var bienSo = (await _goiCho.LayTinhTrangXeAsync(new[] { luot.XeId }, ct)).GetValueOrDefault(luot.XeId)?.BienSo ?? $"#{luot.XeId}";
            (await _repo.TimYeuCauDangChoAsync(luot.Id, ct))?.AdminMoTay(adminId, ld, bayGio);
            luot.RaThuCong(bayGio);
            _repo.Them(SuKienCong.MoTay(SuKienCong.Ra, bienSo, luot.Id, ld, adminId, bayGio));
            await _repo.LuuAsync(ct);
            await BaoSauKhiLuu.ChayAsync(async () =>
            {
                await _phat.PhatAsync(new XeDaRa(luot.Id, luot.XeId, luot.ThoiGianRa!.Value), ct);
                await _thongBao.GuiNhieuAsync(await _goiCho.LayNguoiDungXeAsync(luot.XeId, ct), LoaiThongBao.Ra,
                                              NoiDungCong.AdminMoTay(bienSo, ld, luot.ThoiGianRa.Value), new DichThongBao(HanhDongThongBao.XemLuot, luot.Id), ct);
            }, _log);
        });
        return new KetQuaCongDto(SuKienCong.Ra, true, false, null, "Đã mở barrier thủ công", luotGuiId, XeId: luot!.XeId);
    }
}

/// <summary>GoiCho báo "đã trả phí quá hạn": lượt đang CHO_THANH_TOAN thì mở barrier (đề cương v10).</summary>
public class NgheDaTraPhi : INghe<DaTraPhiQuaHan>
{
    private readonly ICongRaVaoRepository _repo;
    private readonly IGoiChoModule _goiCho;
    private readonly KetThucYeuCau _ketThuc;

    public NgheDaTraPhi(ICongRaVaoRepository repo, IGoiChoModule goiCho, KetThucYeuCau ketThuc)
    { _repo = repo; _goiCho = goiCho; _ketThuc = ketThuc; }

    public async Task XuLyAsync(DaTraPhiQuaHan e, CancellationToken ct)
    {
        var luot = await _repo.TimLuotAsync(e.LuotGuiId, ct);
        if (luot is null || luot.TrangThai != TrangThaiLuot.ChoThanhToan) return;
        var bienSo = (await _goiCho.LayTinhTrangXeAsync(new[] { luot.XeId }, ct)).GetValueOrDefault(luot.XeId)?.BienSo ?? $"#{luot.XeId}";
        await _ketThuc.MoSauThanhToanAsync(luot, bienSo, ct);
    }
}

/// <summary>Tác vụ nền gọi mỗi vài giây: kết thúc các yêu cầu quá hạn (cổng KHÔNG mở). Trả về số yêu cầu đã kết thúc.</summary>
public class HetHanXacNhanUseCase
{
    private readonly ICongRaVaoRepository _repo;
    private readonly KetThucYeuCau _ketThuc;
    private readonly IClock _clock;

    public HetHanXacNhanUseCase(ICongRaVaoRepository repo, KetThucYeuCau ketThuc, IClock clock)
    { _repo = repo; _ketThuc = ketThuc; _clock = clock; }

    public async Task<int> ThucHienAsync(CancellationToken ct = default)
    {
        var dem = 0;
        foreach (var id in await _repo.LayIdYeuCauQuaHanAsync(_clock.BayGio, ct))
        {
            try
            {
                var yc = await _repo.TimYeuCauAsync(id, ct);
                if (yc is null || !yc.HetHan(_clock.BayGio)) continue;
                var luot = await _repo.TimLuotAsync(yc.LuotGuiId, ct);
                if (luot is null) continue;
                await _ketThuc.LuuVaBaoAsync(yc, luot, ct);
                dem++;
            }
            // Instance API khác (hoặc người vừa bấm) đã xử lý yêu cầu này trước: bỏ qua, không phải lỗi
            catch (LoiNghiepVu e) when (e.Ma == ThuLai.MaXungDot) { }
            finally { _repo.BoTheoDoi(); }
        }
        return dem;
    }
}
