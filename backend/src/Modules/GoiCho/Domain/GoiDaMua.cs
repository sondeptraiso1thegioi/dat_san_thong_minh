using NhaXe.SharedKernel;

namespace NhaXe.Modules.GoiCho.Domain;

/// <summary>
/// Một gói xe đã mua (một dòng dang_ky_cho) cùng những thông tin cần để quyết định có huỷ được không.
/// XeDangTrongBai do module CongRaVao trả lời (qua ICongRaVaoModule), không đọc từ bảng luot_gui.
/// Các quy tắc huỷ nằm ở đây nên unit test được mà không cần database.
/// </summary>
public sealed record GoiDaMua(
    int Id, int XeId, int ChuXeId, string TenGoi,
    DateTime NgayBatDau, DateTime NgayKetThuc, decimal SoTien, string TrangThai,
    bool LaGoiCuoi, bool XeDangTrongBai, string BienSo = "")
{
    public bool ConHieuLuc(DateTime bayGio) => TrangThai == "CON_HAN" && NgayKetThuc > bayGio;
    public bool ChuaBatDau(DateTime bayGio) => NgayBatDau > bayGio;

    /// <summary>Điều kiện để XEM số tiền hoàn: chủ xe, gói còn hiệu lực, là gói cuối.</summary>
    public void KiemTraXemHuy(int taiKhoanId, DateTime bayGio)
    {
        if (ChuXeId != taiKhoanId)
            throw new LoiNghiepVu(LoaiLoi.KhongCoQuyen, "KHONG_PHAI_CHU_XE", "Chỉ chủ xe được huỷ gói của xe này.");
        if (!ConHieuLuc(bayGio))
            throw new LoiNghiepVu(LoaiLoi.XungDot, "GOI_HET_HIEU_LUC", "Gói không tồn tại hoặc đã hết hiệu lực.");
        if (!LaGoiCuoi)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "KHONG_PHAI_GOI_CUOI", "Chỉ huỷ được gói cuối cùng. Hãy huỷ gói gia hạn phía sau trước.");
    }

    /// <summary>Điều kiện để HUỶ thật: như trên, thêm: gói đang dùng mà xe trong bãi thì không huỷ.</summary>
    public void KiemTraHuyDuoc(int taiKhoanId, DateTime bayGio)
    {
        KiemTraXemHuy(taiKhoanId, bayGio);
        if (!ChuaBatDau(bayGio) && XeDangTrongBai)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "XE_TRONG_BAI", "Xe đang trong bãi. Đưa xe ra trước khi huỷ gói đang dùng.");
    }

    /// <summary>Điều kiện để Admin XEM trước khi huỷ: gói còn hiệu lực và là gói cuối. Không cần là chủ xe.</summary>
    public void KiemTraAdminXemHuy(DateTime bayGio)
    {
        if (!ConHieuLuc(bayGio))
            throw new LoiNghiepVu(LoaiLoi.XungDot, "GOI_HET_HIEU_LUC", "Gói không tồn tại hoặc đã hết hiệu lực.");
        if (!LaGoiCuoi)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "KHONG_PHAI_GOI_CUOI", "Chỉ huỷ được gói cuối cùng. Hãy huỷ gói gia hạn phía sau trước.");
    }

    /// <summary>
    /// Admin huỷ gói: bắt buộc ghi lý do (khách hàng sẽ đọc lý do này trên app).
    /// Khác người dùng tự huỷ: xe đang trong bãi vẫn huỷ được, vì Admin xử lý vi phạm; xe ở lại sẽ tính phí quá hạn.
    /// </summary>
    public void KiemTraAdminHuyDuoc(string? lyDo, DateTime bayGio)
    {
        if (string.IsNullOrWhiteSpace(lyDo) || lyDo.Trim().Length < 5)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "THIEU_LY_DO", "Lý do phải có ít nhất 5 ký tự.");
        KiemTraAdminXemHuy(bayGio);
    }
}

public sealed record TienHoan(int SoNgayConLai, decimal SoTien);

/// <summary>
/// Chính sách hoàn tiền khi huỷ gói (OCP + Strategy). Muốn đổi cách hoàn (không hoàn, hoàn 80%...)
/// thì viết class mới cài interface này và đổi đăng ký DI, KHÔNG sửa use case huỷ gói.
/// </summary>
public interface IChinhSachHoanTien
{
    /// <summary>Mã để Admin chọn chính sách qua API, ví dụ THEO_NGAY_CON_LAI.</summary>
    string Ma { get; }
    string Ten { get; }
    TienHoan Tinh(GoiDaMua goi, DateTime bayGio);
}

/// <summary>Hoàn theo số ngày chưa dùng (làm tròn xuống); gói chưa bắt đầu hoàn 100%.</summary>
public sealed class HoanTheoNgayConLai : IChinhSachHoanTien
{
    public const string MaChinhSach = "THEO_NGAY_CON_LAI";
    public string Ma => MaChinhSach;
    public string Ten => "Hoàn phần chưa dùng";

    public TienHoan Tinh(GoiDaMua goi, DateTime bayGio)
    {
        var tongNgay = Math.Max(1, (int)Math.Round((goi.NgayKetThuc - goi.NgayBatDau).TotalDays));
        if (goi.ChuaBatDau(bayGio))
            return new TienHoan(tongNgay, goi.SoTien);

        var conLai = Math.Clamp((int)Math.Floor((goi.NgayKetThuc - bayGio).TotalDays), 0, tongNgay);
        var tien = Math.Floor(goi.SoTien * conLai / tongNgay);
        return new TienHoan(conLai, tien);
    }
}

/// <summary>Không hoàn tiền (Admin huỷ do khách vi phạm). Thêm chính sách mới = thêm class, không sửa use case (OCP).</summary>
public sealed class KhongHoanTien : IChinhSachHoanTien
{
    public const string MaChinhSach = "KHONG_HOAN";
    public string Ma => MaChinhSach;
    public string Ten => "Không hoàn tiền";

    public TienHoan Tinh(GoiDaMua goi, DateTime bayGio)
    {
        var conLai = Math.Max(0, (int)Math.Floor((goi.NgayKetThuc - (goi.ChuaBatDau(bayGio) ? goi.NgayBatDau : bayGio)).TotalDays));
        return new TienHoan(conLai, 0);
    }
}

/// <summary>Danh sách chính sách hoàn tiền Admin được chọn, tra theo mã.</summary>
public sealed class BangChinhSachHoan
{
    private readonly IReadOnlyList<IChinhSachHoanTien> _ds;
    public BangChinhSachHoan(IEnumerable<IChinhSachHoanTien> ds) => _ds = ds.ToList();

    public IReadOnlyList<IChinhSachHoanTien> TatCa => _ds;

    public IChinhSachHoanTien Lay(string? ma) =>
        _ds.FirstOrDefault(c => c.Ma == (ma ?? HoanTheoNgayConLai.MaChinhSach))
        ?? throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "CHINH_SACH_SAI", $"Không có chính sách hoàn tiền '{ma}'.");
}

/// <summary>
/// Lý do khách tự huỷ gói + góp ý. Danh sách cố định để Admin thống kê được; muốn thêm lý do chỉ thêm vào danh sách (OCP).
/// Cả hai đều không bắt buộc: không ép khách phải giải thích mới được huỷ.
/// </summary>
public static class LyDoKhachHuy
{
    public const int GopYToiDa = 500;

    public static readonly IReadOnlyList<string> DanhSach = new[]
    {
        "Không còn nhu cầu gửi xe",
        "Giá cao",
        "Chuyển sang bãi khác",
        "Dịch vụ chưa tốt",
        "Khác",
    };

    public static (string? LyDo, string? GopY) ChuanHoa(string? lyDo, string? gopY)
    {
        var ld = string.IsNullOrWhiteSpace(lyDo) ? null : lyDo.Trim();
        var gy = string.IsNullOrWhiteSpace(gopY) ? null : gopY.Trim();
        if (ld is not null && !DanhSach.Contains(ld))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "LY_DO_SAI", "Lý do huỷ không nằm trong danh sách.");
        if (gy is not null && gy.Length > GopYToiDa)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "GOP_Y_QUA_DAI", $"Góp ý tối đa {GopYToiDa} ký tự.");
        return (ld, gy);
    }
}
