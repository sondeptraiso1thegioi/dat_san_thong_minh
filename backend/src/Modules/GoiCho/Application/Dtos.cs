using NhaXe.Modules.GoiCho.Contracts;
namespace NhaXe.Modules.GoiCho.Application;

// Mô hình đọc (read model) trả ra ngoài. Tách khỏi thực thể Domain để Domain không bị kéo theo nhu cầu hiển thị.

public class XeDuocDungDto
{
    public int XeId { get; set; }
    public string BienSo { get; set; } = "";
    public int LoaiXeId { get; set; }
    public string LoaiXe { get; set; } = "";
    public string TrangThaiDuyet { get; set; } = "";
    public string? LyDoGo { get; set; }
    public string? LyDoTuChoi { get; set; }
    public bool CoAnhGiayDangKy { get; set; }
    public bool ChiaSeNhom { get; set; }
    public bool LaChuXe { get; set; }
    public int ChuXeId { get; set; }
    public string? TenChuXe { get; set; }
    public string? GoiHienTai { get; set; }
    public DateTime? HetHanLuc { get; set; }
    public bool ConHan { get; set; }
    public bool DangTrongBai { get; set; }
    public int SoNgayNo { get; set; }
    public decimal SoTienNo { get; set; }
    // gói gần nhất bị ADMIN huỷ (mà sau đó chưa mua gói mới), để app hiện hộp đỏ có lý do cho khách
    public string? GoiBiHuyTen { get; set; }
    public string? GoiBiHuyLyDo { get; set; }
    public DateTime? GoiBiHuyLuc { get; set; }
}

public class ChoConBanDto
{
    public int LoaiXeId { get; set; }
    public string LoaiXe { get; set; } = "";
    public int SucChua { get; set; }
    public int SoChoConBan { get; set; }
}

public record GoiChoDto(int Id, int LoaiXeId, string Ten, int SoNgay, decimal Gia);
public record BangGiaDto(IReadOnlyList<ChoConBanDto> ChoConBan, IReadOnlyList<GoiChoDto> Goi);

public class DangKyChoDto
{
    public int Id { get; set; }
    public string TenGoi { get; set; } = "";
    public DateTime NgayBatDau { get; set; }
    public DateTime NgayKetThuc { get; set; }
    public decimal SoTien { get; set; }
    public string TrangThai { get; set; } = "";
    public DateTime MuaLuc { get; set; }
    public string? LyDoHuy { get; set; }
    public string? GopY { get; set; }
    public DateTime? HuyLuc { get; set; }
    public bool HuyBoiAdmin { get; set; }
}

public record DangKyChoKetQua(int DangKyChoId, DateTime NgayBatDau, DateTime NgayKetThuc, decimal SoTien, bool LaGiaHan);

public class PhiQuaHanDto
{
    public int LuotGuiId { get; set; }
    public DateTime HetHanLuc { get; set; }
    public int SoNgayConNo { get; set; }
    public decimal SoTienConNo { get; set; }
}

public record XemHuyGoiDto(int DangKyChoId, string TenGoi, bool ChuaBatDau, int SoNgayConLai, decimal SoTienHoan);

public class XeQuanLyDto
{
    public int XeId { get; set; }
    public string BienSo { get; set; } = "";
    public int LoaiXeId { get; set; }
    public int ChuXeId { get; set; }
    public string TenChuXe { get; set; } = "";
    public DateTime? HetHanLuc { get; set; }
    public bool DangTrongBai { get; set; }
    /// <summary>Tên gói đang có hiệu lực lúc này; null: chưa mua / đã hết.</summary>
    public string? TenGoiHienTai { get; set; }
    /// <summary>CON_HAN (còn trên 72 giờ), SAP_HET (còn tối đa 72 giờ), CHUA_CO (hết hạn hoặc chưa từng mua).</summary>
    public string TrangThaiGoi { get; set; } = "CHUA_CO";
}

/// <summary>Một dòng hàng chờ duyệt. Đã sắp: loại xe còn chỗ trước, rồi xe gửi lâu nhất trước.</summary>
public record XeChoDuyetDto(int XeId, string BienSo, int LoaiXeId, int ChuXeId, string TenChuXe, bool CoAnh, DateTime TaoLuc,
    bool LoaiXeHetCho);

public record DuyetHangLoatLenh(IReadOnlyList<int> XeIds, bool DongY, string? LyDo);
public record LoiHangLoatDto(int XeId, string Ma, string ThongDiep);
public record KetQuaHangLoatDto(IReadOnlyList<int> ThanhCong, IReadOnlyList<LoiHangLoatDto> Loi);

public record ChinhSachHoanDto(string Ma, string Ten);

// ---------- Sức chứa và bảng giá cho Admin ----------

/// <summary>Dòng đọc từ v_cho_con_ban (EF Core SqlQuery cần class có setter), đổi sang SucChuaLoaiXe của Contracts.</summary>
public class SucChuaRow
{
    public int LoaiXeId { get; set; }
    public string LoaiXe { get; set; } = "";
    public int SucChua { get; set; }
    public int SoXeGiuSuat { get; set; }
    public int SoXeQuaHanTrongBai { get; set; }
    public int SoChoConBan { get; set; }
}

public class TinhTrangXeRow
{
    public int XeId { get; set; }
    public string BienSo { get; set; } = "";
    public int LoaiXeId { get; set; }
    public string LoaiXe { get; set; } = "";
    public int ChuXeId { get; set; }
    public DateTime? HetHanLuc { get; set; }
}

/// <summary>Một gói trong bảng giá Admin (gồm cả gói đã ngừng bán).</summary>
public record GoiAdminDto(int Id, int LoaiXeId, string Ten, int SoNgay, decimal Gia, bool DangBan);

public record BangGiaAdminDto(IReadOnlyList<SucChuaLoaiXe> SucChua, IReadOnlyList<GoiAdminDto> Goi);

/// <summary>Xe tìm theo biển số cho cổng ra vào (đọc bảng xe, dang_ky_cho của GoiCho).</summary>
public class XeTaiCongRow
{
    public int XeId { get; set; }
    public string BienSo { get; set; } = "";
    public string LoaiXe { get; set; } = "";
    public int ChuXeId { get; set; }
    public string TrangThaiDuyet { get; set; } = "";
    public bool CoGoiHieuLuc { get; set; }
    public DateTime? HetHanLuc { get; set; }
}

/// <summary>Một gói sắp hết hạn vừa được đánh dấu "đã nhắc" (cột dang_ky_cho.da_nhac_72h).</summary>
public class GoiCanNhacRow
{
    public int DangKyChoId { get; set; }
    public int XeId { get; set; }
    public string BienSo { get; set; } = "";
    public int ChuXeId { get; set; }
    public bool ChiaSeNhom { get; set; }
    public string TenGoi { get; set; } = "";
    public DateTime HetHanLuc { get; set; }
}
