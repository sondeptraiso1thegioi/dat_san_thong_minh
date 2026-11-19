using NhaXe.Modules.GoiCho.Domain;
using NhaXe.Modules.GoiCho.Contracts;
using NhaXe.Modules.ThongBao.Contracts;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.GoiCho.Application;

// Use case của Admin trong module GoiCho: lịch sử gói, huỷ gói có lý do, sức chứa, bảng giá. Mỗi class một việc (SRP).

public class LayLichSuGoiAdminUseCase
{
    private readonly IXeQuery _query;
    public LayLichSuGoiAdminUseCase(IXeQuery query) => _query = query;
    public Task<IReadOnlyList<DangKyChoDto>> ThucHienAsync(int xeId, CancellationToken ct = default) => _query.LayLichSuGoiAsync(xeId, ct);
}

/// <summary>Admin xem trước số tiền hoàn theo chính sách đã chọn (chưa ghi DB).</summary>
public class AdminXemHuyGoiUseCase
{
    private readonly IDangKyChoStore _store;
    private readonly BangChinhSachHoan _bang;
    private readonly IClock _clock;

    public AdminXemHuyGoiUseCase(IDangKyChoStore store, BangChinhSachHoan bang, IClock clock) { _store = store; _bang = bang; _clock = clock; }

    public async Task<XemHuyGoiDto> ThucHienAsync(int dangKyChoId, string? maChinhSach, CancellationToken ct = default)
    {
        var chinhSach = _bang.Lay(maChinhSach);
        var goi = await _store.LayGoiDaMuaAsync(dangKyChoId, ct)
                  ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_TIM_THAY_GOI", "Không tìm thấy gói.");
        var bayGio = _clock.BayGio;
        goi.KiemTraAdminXemHuy(bayGio);
        var hoan = chinhSach.Tinh(goi, bayGio);
        return new XemHuyGoiDto(goi.Id, goi.TenGoi, goi.ChuaBatDau(bayGio), hoan.SoNgayConLai, hoan.SoTien);
    }
}

public record AdminHuyGoiLenh(string? LyDo, string? ChinhSach);

/// <summary>Admin huỷ gói: bắt buộc lý do, chọn chính sách hoàn tiền. Chủ xe thấy lý do trên thẻ xe và trong thông báo.</summary>
public class AdminHuyGoiUseCase
{
    private readonly IDangKyChoStore _store;
    private readonly BangChinhSachHoan _bang;
    private readonly IClock _clock;
    private readonly IThongBaoModule _thongBao;

    public AdminHuyGoiUseCase(IDangKyChoStore store, BangChinhSachHoan bang, IClock clock, IThongBaoModule thongBao)
    { _store = store; _bang = bang; _clock = clock; _thongBao = thongBao; }

    public async Task<XemHuyGoiDto> ThucHienAsync(int adminId, int dangKyChoId, AdminHuyGoiLenh lenh, CancellationToken ct = default)
    {
        var chinhSach = _bang.Lay(lenh.ChinhSach);
        var goi = await _store.LayGoiDaMuaAsync(dangKyChoId, ct)
                  ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_TIM_THAY_GOI", "Không tìm thấy gói.");
        var bayGio = _clock.BayGio;
        goi.KiemTraAdminHuyDuoc(lenh.LyDo, bayGio);
        var hoan = chinhSach.Tinh(goi, bayGio);
        await _store.AdminHuyGoiAsync(adminId, goi.Id, lenh.LyDo!.Trim(), hoan.SoTien, ct);
        await _thongBao.GuiAsync(goi.ChuXeId, LoaiThongBao.HuyGoi,
            NoiDungThongBao.AdminDaHuy(goi.TenGoi, goi.BienSo, lenh.LyDo.Trim(), hoan.SoTien),
            new DichThongBao(HanhDongThongBao.XemXe, goi.XeId), ct);
        return new XemHuyGoiDto(goi.Id, goi.TenGoi, goi.ChuaBatDau(bayGio), hoan.SoNgayConLai, hoan.SoTien);
    }
}

/// <summary>Đổi sức chứa (số suất bán) của một loại xe. Không hạ dưới số suất đang bị chiếm.</summary>
public class DoiSucChuaUseCase
{
    private readonly ISucChuaQuery _query;
    private readonly ISucChuaStore _store;
    public DoiSucChuaUseCase(ISucChuaQuery query, ISucChuaStore store) { _query = query; _store = store; }

    public async Task<SucChuaLoaiXe> ThucHienAsync(int adminId, int loaiXeId, int sucChuaMoi, CancellationToken ct = default)
    {
        var hienTai = (await _query.LaySucChuaAsync(ct)).FirstOrDefault(s => s.LoaiXeId == loaiXeId)
                      ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "LOAI_XE_SAI", "Loại xe không tồn tại.");
        LoaiXe.KiemTraSucChuaMoi(sucChuaMoi, hienTai.DangChiem);
        await _store.DoiSucChuaAsync(adminId, loaiXeId, sucChuaMoi, ct);
        return (await _query.LaySucChuaAsync(ct)).First(s => s.LoaiXeId == loaiXeId);
    }
}

/// <summary>Bảng giá cho Admin: sức chứa từng loại xe + mọi gói (kể cả gói ngừng bán).</summary>
public class LayBangGiaAdminUseCase
{
    private readonly IGoiChoRepository _goi;
    private readonly ISucChuaQuery _sucChua;
    public LayBangGiaAdminUseCase(IGoiChoRepository goi, ISucChuaQuery sucChua) { _goi = goi; _sucChua = sucChua; }

    public async Task<BangGiaAdminDto> ThucHienAsync(CancellationToken ct = default) => new(
        await _sucChua.LaySucChuaAsync(ct),
        (await _goi.LayTatCaAsync(ct)).Select(g => new GoiAdminDto(g.Id, g.LoaiXeId, g.Ten, g.SoNgay, g.Gia, g.DangBan)).ToList());
}

/// <summary>
/// Admin đổi giá gói. Giá mới áp cho LẦN MUA SAU; gói khách đã mua giữ số tiền đã trả
/// (dang_ky_cho.so_tien), nên tiền hoàn khi huỷ vẫn tính theo số đã trả.
/// Khách mở trang Mua gói là thấy giá mới (đọc trực tiếp từ goi_cho).
/// </summary>
public class DoiGiaGoiUseCase
{
    private readonly IGoiChoRepository _goi;
    public DoiGiaGoiUseCase(IGoiChoRepository goi) => _goi = goi;

    public async Task<GoiAdminDto> ThucHienAsync(int goiId, decimal giaMoi, CancellationToken ct = default)
    {
        var goi = await _goi.TimDeSuaAsync(goiId, ct)
                  ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_TIM_THAY_GOI", "Không tìm thấy gói.");
        goi.DoiGia(giaMoi);
        await _goi.LuuAsync(ct);
        return new GoiAdminDto(goi.Id, goi.LoaiXeId, goi.Ten, goi.SoNgay, goi.Gia, goi.DangBan);
    }
}
