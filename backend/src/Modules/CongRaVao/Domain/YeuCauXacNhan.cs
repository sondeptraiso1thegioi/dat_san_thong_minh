using NhaXe.SharedKernel;

namespace NhaXe.Modules.CongRaVao.Domain;

public static class KetQuaXacNhan
{
    public const string Cho = "CHO";
    public const string DongY = "DONG_Y";
    public const string TuChoi = "TU_CHOI";
    public const string HetHan = "HET_HAN";
}

/// <summary>
/// Yêu cầu xác nhận khi xe ra (đề cương v10, UC-U08): gửi tới chủ xe và mọi người có quyền dùng xe,
/// hiệu lực 60 giây, chỉ dùng một lần. NGƯỜI TRẢ LỜI TRƯỚC có hiệu lực, người sau thấy "Đã được [tên] xác nhận".
/// Hết 60 giây không ai trả lời: KHÔNG mở cổng, chuyển Admin xử lý (Admin mở tay có lý do, ghi ở nhật ký cổng).
/// Cột quan_ly_* của bảng giữ lại cho tương thích, không dùng nữa.
/// </summary>
public class YeuCauXacNhan
{
    public const int SoGiayMacDinh = 60;
    public const int SoGiayToiThieu = 10;
    public const int SoGiayToiDa = 600;

    public int Id { get; private set; }
    public int LuotGuiId { get; private set; }
    public DateTime TaoLuc { get; private set; }
    public DateTime HetHanLuc { get; private set; }
    public string KetQua { get; private set; } = KetQuaXacNhan.Cho;

    public bool? ChuXeDongY { get; private set; }
    public int? ChuXeTraLoiId { get; private set; }
    public DateTime? ChuXeTraLoiLuc { get; private set; }

    public bool? QuanLyDongY { get; private set; }
    public int? QuanLyId { get; private set; }
    public DateTime? QuanLyTraLoiLuc { get; private set; }

    public string? LyDo { get; private set; }
    public DateTime? KetThucLuc { get; private set; }

    /// <summary>ROWVERSION của SQL Server: hai người bấm cùng lúc thì lần lưu sau bị từ chối (khoá lạc quan).</summary>
    public byte[] PhienBan { get; private set; } = Array.Empty<byte>();

    private YeuCauXacNhan() { }

    public static YeuCauXacNhan Tao(int luotGuiId, DateTime bayGio, int soGiay)
    {
        if (soGiay < SoGiayToiThieu || soGiay > SoGiayToiDa)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "THOI_GIAN_CHO_SAI", "Thời gian chờ xác nhận phải từ 10 đến 600 giây.");
        return new() { LuotGuiId = luotGuiId, TaoLuc = bayGio, HetHanLuc = bayGio.AddSeconds(soGiay) };
    }

    public bool DangCho => KetQua == KetQuaXacNhan.Cho;
    public bool DaQuaHan(DateTime bayGio) => bayGio >= HetHanLuc;
    public int SoGiayConLai(DateTime bayGio) => DangCho ? Math.Max(0, (int)Math.Ceiling((HetHanLuc - bayGio).TotalSeconds)) : 0;

    /// <summary>Người dùng xe trả lời ("Đúng là tôi" / "Không phải tôi"). Quyền dùng xe do use case kiểm.</summary>
    public void NguoiDungXeTraLoi(int taiKhoanId, bool dongY, DateTime bayGio)
    {
        if (!DangCho && ChuXeDongY is not null)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "DA_CO_NGUOI_TRA_LOI", "Yêu cầu này đã có người trả lời trước.");
        KiemTraConMo(bayGio);
        ChuXeDongY = dongY;
        ChuXeTraLoiId = taiKhoanId;
        ChuXeTraLoiLuc = bayGio;
        if (dongY) KetThuc(KetQuaXacNhan.DongY, null, bayGio);
        else KetThuc(KetQuaXacNhan.TuChoi, "Người dùng xe báo không phải mình đang lấy xe", bayGio);
    }

    /// <summary>Admin mở barrier thủ công khi yêu cầu còn chờ (UC-A10): đóng yêu cầu, ghi Admin vào cột quan_ly_*.</summary>
    public void AdminMoTay(int adminId, string lyDo, DateTime bayGio)
    {
        if (!DangCho) return;
        QuanLyDongY = true;
        QuanLyId = adminId;
        QuanLyTraLoiLuc = bayGio;
        KetThuc(KetQuaXacNhan.DongY, "Admin mở tay: " + lyDo, bayGio);
    }

    /// <summary>Tác vụ nền gọi: quá hạn mà còn chờ thì kết thúc HET_HAN. Trả về true nếu vừa hết hạn.</summary>
    public bool HetHan(DateTime bayGio)
    {
        if (!DangCho || !DaQuaHan(bayGio)) return false;
        KetThuc(KetQuaXacNhan.HetHan,
            $"Quá {(int)(HetHanLuc - TaoLuc).TotalSeconds} giây không ai xác nhận, chuyển Admin xử lý", bayGio);
        return true;
    }

    /// <summary>Người dùng xe bấm "Không phải tôi" → nghi mất xe.</summary>
    public bool NguoiDungXeBaoKhongPhai => ChuXeDongY == false;

    private void KiemTraConMo(DateTime bayGio)
    {
        if (!DangCho)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "DA_KET_THUC", "Yêu cầu này đã kết thúc.");
        if (DaQuaHan(bayGio))
            throw new LoiNghiepVu(LoaiLoi.XungDot, "XAC_NHAN_HET_HAN", "Đã quá thời gian xác nhận, cổng không mở. Hãy cho xe quay lại cổng ra.");
    }

    private void KetThuc(string ketQua, string? lyDo, DateTime bayGio)
    {
        KetQua = ketQua;
        LyDo = lyDo;
        KetThucLuc = bayGio;
    }
}
