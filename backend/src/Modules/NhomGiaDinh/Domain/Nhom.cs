using NhaXe.SharedKernel;

namespace NhaXe.Modules.NhomGiaDinh.Domain;

public class Nhom
{
    public int Id { get; private set; }
    public int ChuNhomId { get; private set; }
    public string Ten { get; private set; } = "";
    public string MaMoi { get; private set; } = "";
    public DateTime? MaHetHanLuc { get; private set; }
    public DateTime TaoLuc { get; private set; }
    private readonly List<ThanhVienNhom> _thanhVien = [];
    private readonly List<YeuCauThamGia> _yeuCau = [];
    public IReadOnlyCollection<ThanhVienNhom> ThanhVien => _thanhVien.AsReadOnly();
    public IReadOnlyCollection<YeuCauThamGia> YeuCau => _yeuCau.AsReadOnly();
    private Nhom() { }

    public static string ChuanHoaTen(string? ten)
    {
        var value = ten?.Trim() ?? "";
        if (value.Length is < 2 or > 100)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "TEN_NHOM_SAI", "Tên nhóm phải từ 2 đến 100 ký tự.");
        return value;
    }

    public static Nhom Tao(int chuNhomId, string ten, string ma, DateTime luc)
    {
        var nhom = new Nhom { ChuNhomId = chuNhomId, Ten = ChuanHoaTen(ten), TaoLuc = luc };
        nhom.DoiMaMoi(chuNhomId, ma, luc);
        nhom._thanhVien.Add(new ThanhVienNhom(chuNhomId, "CHU", luc));
        return nhom;
    }

    public void KiemTraChuNhom(int taiKhoanId)
    {
        if (taiKhoanId != ChuNhomId)
            throw new LoiNghiepVu(LoaiLoi.KhongCoQuyen, "KHONG_PHAI_CHU_NHOM", "Chỉ chủ nhóm được thực hiện thao tác này.");
    }

    public void DoiMaMoi(int taiKhoanId, string ma, DateTime luc)
    {
        KiemTraChuNhom(taiKhoanId);
        if (ma.Length != 10 || ma.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "MA_MOI_SAI", "Mã mời phải gồm 10 chữ hoặc số.");
        MaMoi = ma;
        MaHetHanLuc = luc.AddDays(7);
    }

    public void XinThamGia(int taiKhoanId, DateTime luc)
    {
        if (MaHetHanLuc is null || luc >= MaHetHanLuc)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "MA_MOI_HET_HAN", "Mã mời đã hết hạn. Hãy xin mã mới từ chủ nhóm.");
        if (_thanhVien.Any(x => x.TaiKhoanId == taiKhoanId))
            throw new LoiNghiepVu(LoaiLoi.XungDot, "DA_CO_NHOM", "Bạn đã là thành viên nhóm.");
        if (_yeuCau.Any(x => x.TaiKhoanId == taiKhoanId && x.TrangThai == "CHO_DUYET"))
            throw new LoiNghiepVu(LoaiLoi.XungDot, "YEU_CAU_TRUNG", "Bạn đã gửi yêu cầu và đang chờ chủ nhóm duyệt.");
        _yeuCau.Add(new YeuCauThamGia(taiKhoanId, luc));
    }

    public void XuLyYeuCau(int chuNhomId, int yeuCauId, bool dongY, DateTime luc)
    {
        KiemTraChuNhom(chuNhomId);
        var yc = LayYeuCau(yeuCauId);
        yc.XuLy(dongY, luc);
        if (dongY) _thanhVien.Add(new ThanhVienNhom(yc.TaiKhoanId, "THANH_VIEN", luc));
    }

    public YeuCauThamGia LayYeuCau(int id) => _yeuCau.SingleOrDefault(x => x.Id == id) ??
        throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_CO_YEU_CAU", "Không tìm thấy yêu cầu trong nhóm này.");

    public void RoiNhom(int nguoiThucHienId, int thanhVienId)
    {
        if (nguoiThucHienId != thanhVienId) KiemTraChuNhom(nguoiThucHienId);
        if (thanhVienId == ChuNhomId)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "CHU_NHOM_KHONG_THE_ROI", "Chủ nhóm phải giải tán nhóm để rời nhóm.");
        var tv = _thanhVien.SingleOrDefault(x => x.TaiKhoanId == thanhVienId) ??
            throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_CO_THANH_VIEN", "Không tìm thấy thành viên trong nhóm.");
        _thanhVien.Remove(tv);
    }
}

public class ThanhVienNhom
{
    public int Id { get; private set; }
    public int NhomId { get; private set; }
    public int TaiKhoanId { get; private set; }
    public string VaiTro { get; private set; } = "";
    public DateTime ThamGiaLuc { get; private set; }
    private ThanhVienNhom() { }
    public ThanhVienNhom(int taiKhoanId, string vaiTro, DateTime luc)
    { TaiKhoanId = taiKhoanId; VaiTro = vaiTro; ThamGiaLuc = luc; }
}

public class YeuCauThamGia
{
    public int Id { get; private set; }
    public int NhomId { get; private set; }
    public int TaiKhoanId { get; private set; }
    public string TrangThai { get; private set; } = "CHO_DUYET";
    public DateTime TaoLuc { get; private set; }
    public DateTime? XuLyLuc { get; private set; }
    private YeuCauThamGia() { }
    public YeuCauThamGia(int taiKhoanId, DateTime luc) { TaiKhoanId = taiKhoanId; TaoLuc = luc; }
    public void XuLy(bool dongY, DateTime luc)
    {
        if (TrangThai != "CHO_DUYET")
            throw new LoiNghiepVu(LoaiLoi.XungDot, "YEU_CAU_DA_XU_LY", "Yêu cầu đã được xử lý.");
        TrangThai = dongY ? "DONG_Y" : "TU_CHOI";
        XuLyLuc = luc;
    }
}
