using NhaXe.Modules.NhomGiaDinh.Domain;

namespace NhaXe.Modules.NhomGiaDinh.Application;

public record TaoNhomLenh(string Ten);
public record XinThamGiaLenh(string MaMoi);
public record XuLyYeuCauLenh(bool DongY);
public record ThanhVienDto(int TaiKhoanId, string VaiTro, DateTime ThamGiaLuc);
public record YeuCauDto(int Id, int NhomId, int TaiKhoanId, string TrangThai, DateTime TaoLuc);
public record NhomDto(int Id, string Ten, int ChuNhomId, string? MaMoi, DateTime? MaHetHanLuc,
    IReadOnlyList<ThanhVienDto> ThanhVien, IReadOnlyList<YeuCauDto> YeuCau)
{
    public static NhomDto Tu(Nhom n, int nguoiXem) => new(n.Id, n.Ten, n.ChuNhomId,
        n.ChuNhomId == nguoiXem ? n.MaMoi : null, n.ChuNhomId == nguoiXem ? n.MaHetHanLuc : null,
        n.ThanhVien.Select(x => new ThanhVienDto(x.TaiKhoanId, x.VaiTro, x.ThamGiaLuc)).ToArray(),
        n.ChuNhomId == nguoiXem ? n.YeuCau.Where(x => x.TrangThai == "CHO_DUYET")
            .Select(x => new YeuCauDto(x.Id, n.Id, x.TaiKhoanId, x.TrangThai, x.TaoLuc)).ToArray() : []);
}
public record TrangThaiNhomDto(NhomDto? Nhom, IReadOnlyList<YeuCauDto> YeuCauCuaToi);
