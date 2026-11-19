namespace NhaXe.Modules.BaiXe.Application;

// ---------- Dòng đọc từ DB (chỉ bảng của module BaiXe: khu_vuc, o_do) ----------

public class KhuRow
{
    public int Id { get; set; }
    public string Ten { get; set; } = "";
    public int LoaiXeId { get; set; }
}

public class ODoRow
{
    public int Id { get; set; }
    public int KhuId { get; set; }
    public string MaO { get; set; } = "";
}

// ---------- Kết quả trả cho Admin ----------

/// <summary>Sức chứa (lấy từ module GoiCho qua IGoiChoModule) ghép với số liệu của bãi.</summary>
public record SucChuaBaiDto(int LoaiXeId, string LoaiXe, int SucChua, int SoXeGiuSuat, int SoXeQuaHanTrongBai, int SoChoConBan,
                            int SoXeTrongBai, int SoODo)
{
    public int DangChiem => SoXeGiuSuat + SoXeQuaHanTrongBai;
    public bool HetCho => SoChoConBan <= 0;
}

/// <summary>Một xe đang trong bãi, kèm vị trí mới nhất và tình trạng gói.</summary>
public class XeTrongBaiDto
{
    public int LuotGuiId { get; set; }
    public int XeId { get; set; }
    public string BienSo { get; set; } = "";
    public int LoaiXeId { get; set; }
    public string LoaiXe { get; set; } = "";
    public int ChuXeId { get; set; }
    public string TenChuXe { get; set; } = "";
    public DateTime ThoiGianVao { get; set; }
    public int? KhuId { get; set; }
    public string? Khu { get; set; }
    public int? OId { get; set; }
    public string? MaO { get; set; }
    public DateTime? ViTriLuc { get; set; }
    public DateTime? HetHanLuc { get; set; }
    public int SoNgayNo { get; set; }
    public decimal SoTienNo { get; set; }
    /// <summary>CON_HAN, SAP_HET (còn dưới 72 giờ), QUA_HAN, KHONG_CO_GOI.</summary>
    public string TinhTrangGoi { get; set; } = "";
}

public record ODoDto(int Id, string MaO, XeTrongBaiDto? Xe, string TrangThai = "KHONG_RO");

public record KhuDto(int Id, string Ten, int LoaiXeId, string LoaiXe, int SoO, int SoOTrong, int SoXe,
                     IReadOnlyList<ODoDto> O, IReadOnlyList<XeTrongBaiDto> XeChuaRoO);

public record SoDoBaiDto(DateTime CapNhatLuc, IReadOnlyList<SucChuaBaiDto> SucChua, IReadOnlyList<KhuDto> Khu,
                         IReadOnlyList<XeTrongBaiDto> XeChuaDinhVi, IReadOnlyList<XeTrongBaiDto> XeTrongBai);

// ---------- Quản lý bãi (Admin) và chỗ trống (khách) ----------

public record LoaiXeDto(int Id, string Ten);
public record KhuQuanLyDto(int Id, string Ten, int LoaiXeId, int SoO, int SoCamera, int SucChua = 0, string? MaSoDo = null, bool DangDung = true);
public record OQuanLyDto(int Id, int KhuId, string MaO, string MaQr, int? CameraId, string TrangThai, string? Nguon, DateTime? CapNhatLuc);
public record CameraDto(int Id, int KhuId, string Ten, string NguonAnh, bool DangBat, string VaiTro = "GIAM_SAT");
public record CauHinhBaiDto(IReadOnlyList<LoaiXeDto> LoaiXe, IReadOnlyList<KhuQuanLyDto> Khu, IReadOnlyList<OQuanLyDto> O,
                            IReadOnlyList<CameraDto> Camera);

/// <param name="DangDung">null: giữ nguyên. false: tạm tắt khu (không chỉ dẫn xe vào, không hiện cho người dùng).</param>
public record KhuLenh(string? Ten, int LoaiXeId, int? SucChua = null, bool? DangDung = null);
public record OLenh(int KhuId, string? MaO, int? CameraId);
public record CameraLenh(int KhuId, string? Ten, string? NguonAnh, bool DangBat, string? VaiTro = null);
public record TrangThaiOLenh(string? TrangThai);

public record OChoTrongDto(int Id, string MaO, string TrangThai);
public record KhuChoTrongDto(int Id, string Ten, int LoaiXeId, int SoO, int SoTrong, int SoCoXe, int SoKhongRo, IReadOnlyList<OChoTrongDto> O);
public record ChoTrongDto(DateTime CapNhatLuc, IReadOnlyList<KhuChoTrongDto> Khu);
