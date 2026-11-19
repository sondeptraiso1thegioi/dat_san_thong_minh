using NhaXe.Modules.GoiCho.Contracts;
using NhaXe.Modules.GoiCho.Domain;
using NhaXe.Modules.NhomGiaDinh.Contracts;
using NhaXe.Modules.TaiKhoan.Contracts;
using NhaXe.Modules.ThongBao.Contracts;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.GoiCho.Application;

// Mỗi use case là một class, một việc (SRP). Tất cả chỉ phụ thuộc interface (DIP).

/// <summary>
/// "Xe của tôi": ghép dữ liệu của 4 module, mỗi module chỉ trả lời phần của mình (qua Contracts, Buổi 12):
/// NhomGiaDinh (ai cùng nhóm) → GoiCho (xe, gói, bản sao xe trong bãi) → TaiKhoan (tên chủ xe).
/// </summary>
public class LayXeCuaToiUseCase
{
    private readonly IXeQuery _query;
    private readonly ITaiKhoanModule _taiKhoan;
    private readonly INhomGiaDinhModule _nhom;
    private readonly TraCuuTrongBai _trongBai;

    public LayXeCuaToiUseCase(IXeQuery query, ITaiKhoanModule taiKhoan, INhomGiaDinhModule nhom, TraCuuTrongBai trongBai)
    { _query = query; _taiKhoan = taiKhoan; _nhom = nhom; _trongBai = trongBai; }

    public async Task<IReadOnlyList<XeDuocDungDto>> ThucHienAsync(int taiKhoanId, CancellationToken ct = default)
    {
        var cungNhom = await _nhom.LayNguoiCungNhomAsync(taiKhoanId, ct);
        var ds = await _query.LayXeDuocDungAsync(taiKhoanId, cungNhom, ct);
        var trongBai = await _trongBai.LayAsync(ds.Select(x => x.XeId), ct);
        var ten = await _taiKhoan.LayHoTenAsync(ds.Select(x => x.ChuXeId), ct);
        foreach (var x in ds)
        {
            x.TenChuXe = ten.GetValueOrDefault(x.ChuXeId);
            if (trongBai.TryGetValue(x.XeId, out var t)) { x.DangTrongBai = true; x.SoNgayNo = t.SoNgayNo; x.SoTienNo = t.SoTienNo; }
        }
        return ds;
    }
}

public record ThemXeLenh(int LoaiXeId, string BienSo, bool ChiaSeNhom);

public class ThemXeUseCase
{
    private readonly IXeRepository _xe;
    private readonly IGoiChoRepository _goi;
    private readonly IClock _clock;
    private readonly ICauHinh _cauHinh;

    public const string KhoaSoXeChoDuyetToiDa = "so_xe_cho_duyet_toi_da";

    public ThemXeUseCase(IXeRepository xe, IGoiChoRepository goi, IClock clock, ICauHinh cauHinh)
    { _xe = xe; _goi = goi; _clock = clock; _cauHinh = cauHinh; }

    public async Task<int> ThucHienAsync(int taiKhoanId, ThemXeLenh lenh, CancellationToken ct = default)
    {
        var bienSo = BienSo.Tao(lenh.BienSo);
        if (!await _goi.TonTaiLoaiXeAsync(lenh.LoaiXeId, ct))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "LOAI_XE_SAI", "Loại xe không tồn tại.");
        if (await _xe.TonTaiBienSoAsync(bienSo.GiaTri, ct))
            throw new LoiNghiepVu(LoaiLoi.XungDot, "BIEN_SO_TRUNG", $"Biển số {bienSo} đã được đăng ký.");

        var toiDa = await _cauHinh.LaySoNguyenAsync(KhoaSoXeChoDuyetToiDa, Xe.SoXeChoDuyetToiDaMacDinh, ct);
        Xe.KiemTraGioiHanChoDuyet(await _xe.DemChoDuyetCuaAsync(taiKhoanId, ct), toiDa);

        var xe = Xe.DangKyMoi(taiKhoanId, lenh.LoaiXeId, bienSo, lenh.ChiaSeNhom, _clock.BayGio);
        _xe.Them(xe);
        await _xe.LuuAsync(ct);
        return xe.Id;
    }
}

public class LayBangGiaUseCase
{
    private readonly IGoiChoRepository _goi;
    private readonly IXeQuery _query;
    public LayBangGiaUseCase(IGoiChoRepository goi, IXeQuery query) { _goi = goi; _query = query; }

    public async Task<BangGiaDto> ThucHienAsync(CancellationToken ct = default)
    {
        var goi = (await _goi.LayDangBanAsync(ct)).Select(g => new GoiChoDto(g.Id, g.LoaiXeId, g.Ten, g.SoNgay, g.Gia)).ToList();
        return new BangGiaDto(await _query.LayChoConBanAsync(ct), goi);
    }
}

public class LayLichSuGoiUseCase
{
    private readonly IXeQuery _query;
    private readonly QuyenDungXe _quyen;
    public LayLichSuGoiUseCase(IXeQuery query, QuyenDungXe quyen) { _query = query; _quyen = quyen; }

    public async Task<IReadOnlyList<DangKyChoDto>> ThucHienAsync(int taiKhoanId, int xeId, CancellationToken ct = default)
    {
        await _quyen.KiemTraAsync(taiKhoanId, xeId, ct);
        return await _query.LayLichSuGoiAsync(xeId, ct);
    }
}

/// <summary>GiaKhachThay: giá khách nhìn thấy lúc bấm Thanh toán. Admin vừa đổi giá thì chặn, không thu khác số khách thấy.</summary>
public record MuaGoiLenh(int XeId, int GoiChoId, decimal? GiaKhachThay = null);

/// <summary>
/// Mua hoặc gia hạn gói. Quy tắc kiểm tra được ở Domain thì kiểm tra trước (có unit test);
/// sp_mua_goi vẫn là chốt chặn cuối cho phần cần khoá DB: còn chỗ hay không, còn nợ phí quá hạn hay không.
/// </summary>
public class MuaGoiUseCase
{
    private readonly IXeRepository _xe;
    private readonly IGoiChoRepository _goi;
    private readonly IThanhToanGateway _thanhToan;
    private readonly IDangKyChoStore _store;

    public MuaGoiUseCase(IXeRepository xe, IGoiChoRepository goi, IThanhToanGateway thanhToan, IDangKyChoStore store)
    {
        _xe = xe; _goi = goi; _thanhToan = thanhToan; _store = store;
    }

    public async Task<DangKyChoKetQua> ThucHienAsync(int taiKhoanId, MuaGoiLenh lenh, CancellationToken ct = default)
    {
        var xe = await _xe.TimAsync(lenh.XeId, ct)
                 ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_TIM_THAY_XE", "Không tìm thấy xe.");
        var goi = await _goi.TimAsync(lenh.GoiChoId, ct)
                  ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_TIM_THAY_GOI", "Không tìm thấy gói.");

        xe.KiemTraDuocMuaGoi(taiKhoanId);
        goi.KiemTraBanDuocCho(xe);
        goi.KiemTraGiaKhachThay(lenh.GiaKhachThay);

        var tt = await _thanhToan.ThuTienAsync(taiKhoanId, goi.Gia, $"Mua gói {goi.Ten} cho xe {xe.BienSo}", ct);
        if (!tt.ThanhCong)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "THANH_TOAN_LOI", tt.LyDo ?? "Thanh toán không thành công.");

        try
        {
            return await _store.MuaGoiAsync(taiKhoanId, xe.Id, goi.Id, ct);
        }
        catch
        {
            await _thanhToan.HoanTienAsync(tt.MaGiaoDich, ct); // DB từ chối (hết chỗ, còn nợ...) thì hoàn tiền
            throw;
        }
    }
}

/// <summary>Nợ phí quá hạn: lượt gửi đang mở hỏi CongRaVao, số tiền nợ GoiCho tự tính.</summary>
public class XemPhiQuaHanUseCase
{
    private readonly QuyenDungXe _quyen;
    private readonly TraCuuTrongBai _trongBai;
    public XemPhiQuaHanUseCase(QuyenDungXe quyen, TraCuuTrongBai trongBai) { _quyen = quyen; _trongBai = trongBai; }

    public async Task<PhiQuaHanDto?> ThucHienAsync(int taiKhoanId, int xeId, CancellationToken ct = default)
    {
        await _quyen.KiemTraAsync(taiKhoanId, xeId, ct);
        var no = (await _trongBai.LayAsync(new[] { xeId }, ct)).GetValueOrDefault(xeId)?.No;
        return no is { SoNgayConNo: > 0 } ? no : null;
    }
}

public class TraPhiQuaHanUseCase
{
    private readonly XemPhiQuaHanUseCase _xem;
    private readonly IThanhToanGateway _thanhToan;
    private readonly IPhiQuaHanStore _phi;
    private readonly IPhatSuKien? _phat;
    private readonly IClock? _clock;

    public TraPhiQuaHanUseCase(XemPhiQuaHanUseCase xem, IThanhToanGateway thanhToan, IPhiQuaHanStore phi,
                               IPhatSuKien? phat = null, IClock? clock = null)
    {
        _xem = xem; _thanhToan = thanhToan; _phi = phi; _phat = phat; _clock = clock;
    }

    public async Task<PhiQuaHanDto> ThucHienAsync(int taiKhoanId, int xeId, CancellationToken ct = default)
    {
        var no = await _xem.ThucHienAsync(taiKhoanId, xeId, ct)
                 ?? throw new LoiNghiepVu(LoaiLoi.XungDot, "KHONG_CON_NO", "Xe không còn nợ phí quá hạn.");

        var tt = await _thanhToan.ThuTienAsync(taiKhoanId, no.SoTienConNo, $"Phí quá hạn {no.SoNgayConNo} ngày", ct);
        if (!tt.ThanhCong)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "THANH_TOAN_LOI", tt.LyDo ?? "Thanh toán không thành công.");
        try
        {
            await _phi.ThanhToanAsync(taiKhoanId, no.LuotGuiId, ct);
        }
        catch
        {
            await _thanhToan.HoanTienAsync(tt.MaGiaoDich, ct);
            throw;
        }
        // Lưu xong mới báo: cổng ra đang chờ thanh toán thì mở barrier (CongRaVao nghe sự kiện này)
        if (_phat is not null)
            await _phat.PhatAsync(new DaTraPhiQuaHan(no.LuotGuiId, xeId, taiKhoanId, _clock?.BayGio ?? DateTime.Now), ct);
        return no;
    }
}

/// <summary>Hàng chờ duyệt đã sắp xếp: loại xe còn chỗ lên trước (duyệt xong khách mua gói được ngay), rồi xe gửi lâu nhất.</summary>
public class LayXeChoDuyetUseCase
{
    private readonly IXeRepository _xe;
    private readonly ITaiKhoanModule _taiKhoan;
    private readonly IXeQuery _query;
    public LayXeChoDuyetUseCase(IXeRepository xe, ITaiKhoanModule taiKhoan, IXeQuery query) { _xe = xe; _taiKhoan = taiKhoan; _query = query; }

    public async Task<IReadOnlyList<XeChoDuyetDto>> ThucHienAsync(CancellationToken ct = default)
    {
        var ds = await _xe.LayChoDuyetAsync(ct);
        var ten = await _taiKhoan.LayHoTenAsync(ds.Select(x => x.ChuXeId), ct);
        var hetCho = (await _query.LayChoConBanAsync(ct)).Where(c => c.SoChoConBan <= 0).Select(c => c.LoaiXeId).ToHashSet();
        return ds.Select(x => new XeChoDuyetDto(x.Id, x.BienSo, x.LoaiXeId, x.ChuXeId, ten.GetValueOrDefault(x.ChuXeId, "?"),
                    !string.IsNullOrEmpty(x.AnhGiayDangKy), x.TaoLuc, hetCho.Contains(x.LoaiXeId)))
                 .OrderBy(x => x.LoaiXeHetCho).ThenBy(x => x.TaoLuc).ThenBy(x => x.XeId).ToList();
    }
}

/// <summary>Admin duyệt / từ chối một xe. Ghi ai duyệt, lúc nào; lưu DB xong mới báo chủ xe.</summary>
public class DuyetXeUseCase
{
    private readonly IXeRepository _xe;
    private readonly IClock _clock;
    private readonly IThongBaoModule _thongBao;
    public DuyetXeUseCase(IXeRepository xe, IClock clock, IThongBaoModule thongBao) { _xe = xe; _clock = clock; _thongBao = thongBao; }

    public async Task ThucHienAsync(int adminId, int xeId, bool dongY, string? lyDo = null, CancellationToken ct = default)
    {
        var xe = await _xe.TimAsync(xeId, ct)
                 ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_TIM_THAY_XE", "Không tìm thấy xe.");
        if (dongY) xe.Duyet(adminId, _clock.BayGio); else xe.TuChoi(lyDo);
        await _xe.LuuAsync(ct);
        await BaoKetQuaDuyet.GuiAsync(_thongBao, xe, ct);
    }
}

/// <summary>Báo chủ xe kết quả duyệt. Gọi SAU khi lưu DB; gửi lỗi thì không làm hỏng việc duyệt đã lưu.</summary>
internal static class BaoKetQuaDuyet
{
    public static async Task GuiAsync(IThongBaoModule thongBao, Xe xe, CancellationToken ct)
    {
        var daDuyet = xe.TrangThaiDuyet == TrangThaiDuyet.DaDuyet;
        var noiDung = daDuyet ? NoiDungThongBao.XeDaDuyet(xe.BienSo) : NoiDungThongBao.XeBiTuChoi(xe.BienSo, xe.LyDoTuChoi ?? "");
        // được duyệt → mua gói cho xe đó; bị từ chối → mở xe đó để tải lại ảnh giấy đăng ký
        var dich = new DichThongBao(daDuyet ? HanhDongThongBao.MuaGoi : HanhDongThongBao.GuiLaiGiay, xe.Id);
        try { await thongBao.GuiAsync(xe.ChuXeId, LoaiThongBao.KetQuaDuyet, noiDung, dich, ct); }
        catch (Exception e) when (e is not OperationCanceledException) { /* đã duyệt xong; chủ xe vẫn thấy trạng thái trong "Xe của tôi" */ }
    }
}

/// <summary>
/// Duyệt / từ chối nhiều xe một lần. Xe nào lỗi (đã xử lý, thiếu ảnh...) thì bỏ qua và báo riêng,
/// các xe còn lại vẫn được xử lý; ghi DB một lần ở cuối.
/// </summary>
public class DuyetHangLoatUseCase
{
    public const int ToiDaMotLan = 50;
    private readonly IXeRepository _xe;
    private readonly IClock _clock;
    private readonly IThongBaoModule _thongBao;
    public DuyetHangLoatUseCase(IXeRepository xe, IClock clock, IThongBaoModule thongBao) { _xe = xe; _clock = clock; _thongBao = thongBao; }

    public async Task<KetQuaHangLoatDto> ThucHienAsync(int adminId, DuyetHangLoatLenh lenh, CancellationToken ct = default)
    {
        var ids = (lenh.XeIds ?? Array.Empty<int>()).Distinct().ToList();
        if (ids.Count == 0)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "CHUA_CHON_XE", "Chưa chọn xe nào.");
        if (ids.Count > ToiDaMotLan)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "QUA_NHIEU_XE", $"Mỗi lần xử lý tối đa {ToiDaMotLan} xe.");
        if (!lenh.DongY) Xe.ChuanHoaLyDoTuChoi(lenh.LyDo); // sai lý do thì dừng ngay, không xử lý xe nào

        var thanhCong = new List<int>();
        var daXuLy = new List<Xe>();
        var loi = new List<LoiHangLoatDto>();
        var bayGio = _clock.BayGio;
        foreach (var id in ids)
        {
            var xe = await _xe.TimAsync(id, ct);
            if (xe is null) { loi.Add(new(id, "KHONG_TIM_THAY_XE", "Không tìm thấy xe.")); continue; }
            try
            {
                if (lenh.DongY) xe.Duyet(adminId, bayGio); else xe.TuChoi(lenh.LyDo);
                thanhCong.Add(id); daXuLy.Add(xe);
            }
            catch (LoiNghiepVu e) { loi.Add(new(id, e.Ma, e.Message)); }
        }
        if (thanhCong.Count > 0) await _xe.LuuAsync(ct);
        foreach (var xe in daXuLy) await BaoKetQuaDuyet.GuiAsync(_thongBao, xe, ct); // sau khi lưu DB
        return new KetQuaHangLoatDto(thanhCong, loi);
    }
}

/// <summary>Chủ xe tải ảnh giấy đăng ký (lần đầu, thay ảnh, hoặc gửi lại sau khi bị từ chối).</summary>
public class TaiAnhGiayDangKyUseCase
{
    private readonly IXeRepository _xe;
    private readonly ILuuTruTep _luuTru;
    private readonly ICauHinh _cauHinh;
    private readonly IGoiChoRepository _goi;
    private readonly ITaiKhoanModule _taiKhoan;
    private readonly IThongBaoModule _thongBao;
    public TaiAnhGiayDangKyUseCase(IXeRepository xe, ILuuTruTep luuTru, ICauHinh cauHinh, IGoiChoRepository goi,
                                   ITaiKhoanModule taiKhoan, IThongBaoModule thongBao)
    { _xe = xe; _luuTru = luuTru; _cauHinh = cauHinh; _goi = goi; _taiKhoan = taiKhoan; _thongBao = thongBao; }

    public async Task ThucHienAsync(int taiKhoanId, int xeId, Stream noiDung, long kichThuoc, CancellationToken ct = default)
    {
        var xe = await _xe.TimAsync(xeId, ct)
                 ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_TIM_THAY_XE", "Không tìm thấy xe.");

        // Đọc vài byte đầu để nhận dạng định dạng thật của tệp
        var buf = new MemoryStream();
        await noiDung.CopyToAsync(buf, ct);
        if (buf.Length != kichThuoc) kichThuoc = buf.Length;
        var tep = TepAnh.NhanDien(buf.GetBuffer().AsSpan(0, (int)Math.Min(buf.Length, TepAnh.SoByteDauCanDoc)), kichThuoc);

        // Gửi lại xe bị từ chối = thêm 1 xe vào hàng chờ, nên phải tôn trọng giới hạn số xe chờ duyệt
        if (xe.TrangThaiDuyet == TrangThaiDuyet.TuChoi && xe.ChuXeId == taiKhoanId)
        {
            var toiDa = await _cauHinh.LaySoNguyenAsync(ThemXeUseCase.KhoaSoXeChoDuyetToiDa, Xe.SoXeChoDuyetToiDaMacDinh, ct);
            Xe.KiemTraGioiHanChoDuyet(await _xe.DemChoDuyetCuaAsync(taiKhoanId, ct), toiDa);
        }

        // Kiểm tra quyền và trạng thái TRƯỚC khi ghi tệp (khoá tạm), rồi mới lưu tệp thật
        xe.KiemTraDoiAnhDuoc(taiKhoanId);
        var coAnhLanDau = string.IsNullOrEmpty(xe.AnhGiayDangKy);
        buf.Position = 0;
        var khoa = await _luuTru.LuuAsync("giay-dk", tep.Duoi, buf, ct);
        var guiLai = xe.GanAnhGiayDangKy(taiKhoanId, khoa);
        await _xe.LuuAsync(ct);

        // Admin cần ảnh mới duyệt được: báo khi xe vừa có ảnh lần đầu, hoặc gửi lại sau khi bị từ chối (không báo 2 lần)
        if (!Xe.CanBaoAdmin(guiLai, coAnhLanDau)) return;
        try
        {
            var ten = (await _taiKhoan.LayHoTenAsync(new[] { xe.ChuXeId }, ct)).GetValueOrDefault(xe.ChuXeId, "?");
            var loai = NoiDungThongBao.TenLoai(await _goi.LayMaLoaiXeAsync(xe.LoaiXeId, ct));
            await _thongBao.GuiChoMoiAdminAsync(LoaiThongBao.XeChoDuyet, NoiDungThongBao.XeChoDuyet(xe.BienSo, loai, ten, guiLai),
                new DichThongBao(HanhDongThongBao.DuyetXe, xe.Id), ct);
        }
        catch (Exception e) when (e is not OperationCanceledException) { /* ảnh đã lưu; badge "Quản lý xe" vẫn đếm được xe này */ }
    }
}

public record AnhTep(Stream NoiDung, string ContentType);

/// <summary>Xem ảnh giấy đăng ký: chỉ chủ xe và Admin.</summary>
public class XemAnhGiayDangKyUseCase
{
    private readonly IXeRepository _xe;
    private readonly ILuuTruTep _luuTru;
    public XemAnhGiayDangKyUseCase(IXeRepository xe, ILuuTruTep luuTru) { _xe = xe; _luuTru = luuTru; }

    public async Task<AnhTep> ThucHienAsync(int taiKhoanId, bool laAdmin, int xeId, CancellationToken ct = default)
    {
        var xe = await _xe.TimAsync(xeId, ct)
                 ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_TIM_THAY_XE", "Không tìm thấy xe.");
        xe.KiemTraXemAnhDuoc(taiKhoanId, laAdmin);
        var s = await _luuTru.MoAsync(xe.AnhGiayDangKy!, ct)
                ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "CHUA_CO_ANH", "Không tìm thấy tệp ảnh giấy đăng ký.");
        return new AnhTep(s, TepAnh.TuDuoi(xe.AnhGiayDangKy!).ContentType);
    }
}

/// <summary>Xem trước số tiền được hoàn nếu huỷ gói (chưa ghi gì vào DB).</summary>
public class XemHuyGoiUseCase
{
    private readonly IDangKyChoStore _store;
    private readonly IChinhSachHoanTien _chinhSach;
    private readonly IClock _clock;

    public XemHuyGoiUseCase(IDangKyChoStore store, IChinhSachHoanTien chinhSach, IClock clock)
    {
        _store = store; _chinhSach = chinhSach; _clock = clock;
    }

    public async Task<XemHuyGoiDto> ThucHienAsync(int taiKhoanId, int dangKyChoId, CancellationToken ct = default)
    {
        var goi = await _store.LayGoiDaMuaAsync(dangKyChoId, ct)
                  ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_TIM_THAY_GOI", "Không tìm thấy gói.");
        var bayGio = _clock.BayGio;
        goi.KiemTraXemHuy(taiKhoanId, bayGio);
        var hoan = _chinhSach.Tinh(goi, bayGio);
        return new XemHuyGoiDto(goi.Id, goi.TenGoi, goi.ChuaBatDau(bayGio), hoan.SoNgayConLai, hoan.SoTien);
    }
}

/// <summary>
/// Huỷ gói trước hạn. Use case chỉ hỏi chính sách "hoàn bao nhiêu", không tự tính (OCP).
/// Hỏi CongRaVao xem xe có đang trong bãi; huỷ xong gửi thông báo cho khách và mọi Admin qua module ThongBao.
/// </summary>
public class HuyGoiUseCase
{
    private readonly IDangKyChoStore _store;
    private readonly IChinhSachHoanTien _chinhSach;
    private readonly IClock _clock;
    private readonly TraCuuTrongBai _trongBai;
    private readonly IThongBaoModule _thongBao;
    private readonly ITaiKhoanModule _taiKhoan;

    public HuyGoiUseCase(IDangKyChoStore store, IChinhSachHoanTien chinhSach, IClock clock,
                         TraCuuTrongBai trongBai, IThongBaoModule thongBao, ITaiKhoanModule taiKhoan)
    {
        _store = store; _chinhSach = chinhSach; _clock = clock; _trongBai = trongBai; _thongBao = thongBao; _taiKhoan = taiKhoan;
    }

    public async Task<XemHuyGoiDto> ThucHienAsync(int taiKhoanId, int dangKyChoId, string? lyDo = null, string? gopY = null,
                                                  CancellationToken ct = default)
    {
        var (ld, gy) = LyDoKhachHuy.ChuanHoa(lyDo, gopY); // kiểm tra trước, sai thì không đụng DB
        var goi = await _store.LayGoiDaMuaAsync(dangKyChoId, ct)
                  ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_TIM_THAY_GOI", "Không tìm thấy gói.");
        goi = goi with { XeDangTrongBai = await _trongBai.DangTrongBaiAsync(goi.XeId, ct) };
        var bayGio = _clock.BayGio;
        goi.KiemTraHuyDuoc(taiKhoanId, bayGio);
        var hoan = _chinhSach.Tinh(goi, bayGio);
        await _store.HuyGoiAsync(taiKhoanId, goi.Id, hoan.SoTien, ld, gy, ct);

        await _thongBao.GuiAsync(taiKhoanId, LoaiThongBao.HuyGoi, NoiDungThongBao.KhachDaHuy(goi.TenGoi, goi.BienSo, hoan.SoTien),
            new DichThongBao(HanhDongThongBao.XemXe, goi.XeId), ct);
        var khach = (await _taiKhoan.LayLienHeAsync(new[] { taiKhoanId }, ct)).GetValueOrDefault(taiKhoanId);
        await _thongBao.GuiChoMoiAdminAsync(LoaiThongBao.KhachHuy,
            NoiDungThongBao.BaoAdminKhachHuy(khach?.HoTen ?? "?", khach?.Sdt ?? "?", goi.TenGoi, goi.BienSo, hoan.SoTien, ld, gy),
            new DichThongBao(HanhDongThongBao.XemXeAdmin, goi.XeId), ct);
        return new XemHuyGoiDto(goi.Id, goi.TenGoi, goi.ChuaBatDau(bayGio), hoan.SoNgayConLai, hoan.SoTien);
    }
}

/// <summary>Admin gỡ xe phạm quy, rồi báo cho chủ xe qua module ThongBao.</summary>
public class GoXeUseCase
{
    private readonly IXeRepository _xe;
    private readonly IQuanLyXeStore _store;
    private readonly IThongBaoModule _thongBao;
    public GoXeUseCase(IXeRepository xe, IQuanLyXeStore store, IThongBaoModule thongBao) { _xe = xe; _store = store; _thongBao = thongBao; }

    public async Task ThucHienAsync(int adminId, int xeId, string? lyDo, CancellationToken ct = default)
    {
        var xe = await _xe.TimAsync(xeId, ct)
                 ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_TIM_THAY_XE", "Không tìm thấy xe.");
        xe.KiemTraGoDuoc(lyDo);
        await _store.GoXeAsync(adminId, xeId, lyDo!.Trim(), ct);
        await _thongBao.GuiAsync(xe.ChuXeId, LoaiThongBao.XeBiGo, NoiDungThongBao.XeBiGo(xe.BienSo, lyDo.Trim()),
            new DichThongBao(HanhDongThongBao.XemXe, xe.Id), ct);
    }
}
