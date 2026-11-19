using NhaXe.Modules.ThongBao.Contracts;
using NhaXe.Modules.NhomGiaDinh.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.NhomGiaDinh.Application;

internal static class NhomCanCo
{
    public static async Task<Nhom> LayAsync(INhomRepository repo, int id, CancellationToken ct) =>
        await repo.LayCuaThanhVienAsync(id, ct) ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "CHUA_CO_NHOM", "Bạn chưa thuộc nhóm nào.");
    public static async Task KiemTraChuaCoNhomAsync(INhomRepository repo, int id, CancellationToken ct)
    {
        if (await repo.DaCoNhomAsync(id, ct))
            throw new LoiNghiepVu(LoaiLoi.XungDot, "DA_CO_NHOM", "Tài khoản đã thuộc một nhóm.");
    }
}

public class XemNhomUseCase(INhomRepository repo)
{
    public async Task<TrangThaiNhomDto> ThucHienAsync(int id, CancellationToken ct = default)
    {
        var nhom = await repo.LayCuaThanhVienAsync(id, ct);
        return new(nhom is null ? null : NhomDto.Tu(nhom, id), await repo.LayYeuCauCuaAsync(id, ct));
    }
}

public class TaoNhomUseCase(INhomRepository repo, IMaMoiGenerator ma, IClock clock)
{
    public Task<NhomDto> ThucHienAsync(int id, TaoNhomLenh lenh, CancellationToken ct = default)
    {
        var ten = Nhom.ChuanHoaTen(lenh.Ten);
        return repo.TrongGiaoDichAsync(async () =>
        {
            await NhomCanCo.KiemTraChuaCoNhomAsync(repo, id, ct);
            var nhom = Nhom.Tao(id, ten, ma.Tao(), clock.BayGio);
            repo.Them(nhom);
            await repo.LuuAsync(ct);
            return NhomDto.Tu(nhom, id);
        }, ct);
    }
}

/// <summary>Xin vào nhóm bằng mã mời. Lưu xong thì báo chủ nhóm có yêu cầu chờ duyệt (đề cương v10, luồng 3).</summary>
public class XinThamGiaUseCase(INhomRepository repo, IClock clock, IThongBaoModule? thongBao = null)
{
    public async Task<int> ThucHienAsync(int id, XinThamGiaLenh lenh, CancellationToken ct = default)
    {
        var ma = lenh.MaMoi?.Trim().ToUpperInvariant() ?? "";
        // Dữ liệu mẫu cũ dùng mã 6 ký tự; mã tạo mới dùng 10 ký tự.
        if (ma.Length is < 6 or > 10 || ma.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "MA_MOI_SAI", "Mã mời phải gồm 6 đến 10 chữ hoặc số.");
        var chuNhom = 0;
        var kq = await repo.TrongGiaoDichAsync(async () =>
        {
            await NhomCanCo.KiemTraChuaCoNhomAsync(repo, id, ct);
            var nhom = await repo.TimTheoMaAsync(ma, ct) ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_CO_MA_MOI", "Không tìm thấy mã mời.");
            nhom.XinThamGia(id, clock.BayGio);
            await repo.LuuAsync(ct);
            chuNhom = nhom.ChuNhomId;
            return nhom.YeuCau.Single(x => x.TaiKhoanId == id && x.TrangThai == "CHO_DUYET").Id;
        }, ct);
        await BaoNhom.ChayAsync(thongBao, chuNhom, "Có người xin tham gia nhóm gia đình của bạn. Mở mục Nhóm để duyệt.", ct);
        return kq;
    }
}

/// <summary>Chủ nhóm duyệt / từ chối. Lưu xong thì báo người xin kết quả.</summary>
public class XuLyYeuCauUseCase(INhomRepository repo, IClock clock, IThongBaoModule? thongBao = null)
{
    public async Task<NhomDto> ThucHienAsync(int id, int yeuCauId, bool dongY, CancellationToken ct = default)
    {
        var nguoiXin = 0; var tenNhom = "";
        var kq = await repo.TrongGiaoDichAsync(async () =>
        {
            var nhom = await NhomCanCo.LayAsync(repo, id, ct);
            nhom.KiemTraChuNhom(id);
            var yc = nhom.LayYeuCau(yeuCauId);
            if (dongY) await NhomCanCo.KiemTraChuaCoNhomAsync(repo, yc.TaiKhoanId, ct);
            nhom.XuLyYeuCau(id, yeuCauId, dongY, clock.BayGio);
            await repo.LuuAsync(ct);
            nguoiXin = yc.TaiKhoanId; tenNhom = nhom.Ten;
            return NhomDto.Tu(nhom, id);
        }, ct);
        await BaoNhom.ChayAsync(thongBao, nguoiXin, dongY
            ? $"Bạn đã được duyệt vào nhóm {tenNhom}. Xe được chia sẻ trong nhóm sẽ hiện ở mục Xe."
            : $"Yêu cầu tham gia nhóm {tenNhom} bị từ chối.", ct);
        return kq;
    }
}

/// <summary>Thông báo chỉ là phụ: gửi lỗi thì bỏ qua, dữ liệu nhóm đã lưu.</summary>
internal static class BaoNhom
{
    public static async Task ChayAsync(IThongBaoModule? tb, int taiKhoanId, string noiDung, CancellationToken ct)
    {
        if (tb is null || taiKhoanId == 0) return;
        try { await tb.GuiAsync(taiKhoanId, LoaiThongBao.Nhom, noiDung, new DichThongBao(HanhDongThongBao.XemNhom), ct); }
        catch (Exception e) when (e is not OperationCanceledException) { }
    }
}

public class DoiMaMoiUseCase(INhomRepository repo, IMaMoiGenerator ma, IClock clock)
{
    public Task<NhomDto> ThucHienAsync(int id, CancellationToken ct = default) => repo.TrongGiaoDichAsync(async () =>
    {
        var nhom = await NhomCanCo.LayAsync(repo, id, ct);
        nhom.DoiMaMoi(id, ma.Tao(), clock.BayGio);
        await repo.LuuAsync(ct);
        return NhomDto.Tu(nhom, id);
    }, ct);
}

public class RoiNhomUseCase(INhomRepository repo)
{
    public Task<bool> ThucHienAsync(int id, int thanhVienId, CancellationToken ct = default) => repo.TrongGiaoDichAsync(async () =>
    {
        var nhom = await NhomCanCo.LayAsync(repo, id, ct);
        nhom.RoiNhom(id, thanhVienId);
        await repo.LuuAsync(ct);
        return true;
    }, ct);
}

public class GiaiTanNhomUseCase(INhomRepository repo)
{
    public Task<bool> ThucHienAsync(int id, CancellationToken ct = default) => repo.TrongGiaoDichAsync(async () =>
    {
        var nhom = await NhomCanCo.LayAsync(repo, id, ct);
        nhom.KiemTraChuNhom(id);
        repo.Xoa(nhom);
        await repo.LuuAsync(ct);
        return true;
    }, ct);
}
