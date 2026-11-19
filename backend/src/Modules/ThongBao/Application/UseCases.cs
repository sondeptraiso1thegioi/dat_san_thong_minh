using NhaXe.Modules.ThongBao.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.ThongBao.Application;

/// <summary>Hộp thư của Admin: khách huỷ gói, xe nghi mất, xe ra không đủ xác nhận (các loại trong LoaiThongBao.HopThuAdmin).</summary>
public class LayThongBaoAdminUseCase
{
    public static IReadOnlyCollection<string> CacLoai => LoaiThongBao.HopThuAdmin;
    private readonly IThongBaoStore _store;
    public LayThongBaoAdminUseCase(IThongBaoStore store) => _store = store;

    public async Task<HopThuDto> ThucHienAsync(int adminId, CancellationToken ct = default)
    {
        var ds = await _store.LayAsync(adminId, CacLoai, ct);
        return new HopThuDto(ds.Count(t => !t.DaDoc), ds);
    }
}

/// <summary>Hộp thư của người dùng: mọi thông báo gửi cho chính mình (xe vào / ra, xác nhận, sắp hết hạn, huỷ gói...).</summary>
public class LayHopThuUseCase
{
    private readonly IThongBaoStore _store;
    public LayHopThuUseCase(IThongBaoStore store) => _store = store;

    public async Task<HopThuDto> ThucHienAsync(int taiKhoanId, CancellationToken ct = default)
    {
        var ds = await _store.LayAsync(taiKhoanId, null, ct);
        return new HopThuDto(await _store.DemChuaDocAsync(taiKhoanId, ct), ds);
    }
}

/// <summary>Chỉ đếm số chưa đọc: menu gọi mỗi 15 giây, nên không tải cả danh sách.</summary>
public class DemChuaDocUseCase
{
    private readonly IThongBaoStore _store;
    public DemChuaDocUseCase(IThongBaoStore store) => _store = store;
    public Task<int> ThucHienAsync(int taiKhoanId, CancellationToken ct = default) => _store.DemChuaDocAsync(taiKhoanId, ct);
}

public class DanhDauDaDocUseCase
{
    private readonly IThongBaoStore _store;
    public DanhDauDaDocUseCase(IThongBaoStore store) => _store = store;

    public async Task ThucHienAsync(int taiKhoanId, int thongBaoId, CancellationToken ct = default)
    {
        if (!await _store.DanhDauDaDocAsync(taiKhoanId, thongBaoId, ct))
            throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_TIM_THAY_THONG_BAO", "Không tìm thấy thông báo.");
    }
}

public class DocHetUseCase
{
    private readonly IThongBaoStore _store;
    public DocHetUseCase(IThongBaoStore store) => _store = store;
    public Task<int> ThucHienAsync(int taiKhoanId, CancellationToken ct = default) => _store.DocHetAsync(taiKhoanId, ct);
}
