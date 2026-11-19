using NhaXe.SharedKernel;

namespace NhaXe.Modules.GoiCho.Domain;

public class LoaiXe
{
    public int Id { get; private set; }
    public string Ten { get; private set; } = "";
    public int SucChua { get; private set; }
    public decimal GiaQuaHanNgay { get; private set; }
    private LoaiXe() { }

    /// <summary>
    /// Đổi sức chứa (số suất được bán). Không được hạ xuống dưới số suất đang bị chiếm
    /// (xe giữ gói còn hạn + xe quá hạn còn trong bãi), nếu không sẽ bán âm chỗ.
    /// sp_doi_suc_chua kiểm tra lại dưới khoá DB để không bị tranh chấp với người đang mua gói.
    /// </summary>
    public static void KiemTraSucChuaMoi(int sucChuaMoi, int dangChiem)
    {
        if (sucChuaMoi < 0 || sucChuaMoi > 100000)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "SUC_CHUA_SAI", "Sức chứa phải từ 0 đến 100000.");
        if (sucChuaMoi < dangChiem)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "SUC_CHUA_QUA_THAP",
                $"Đang có {dangChiem} suất bị chiếm, không hạ sức chứa xuống {sucChuaMoi} được.");
    }
}

/// <summary>Gói chỗ (tuần/tháng/năm). Gói chỉ giữ SUẤT trong sức chứa, không gán ô cố định.</summary>
public class GoiCho
{
    public int Id { get; private set; }
    public int LoaiXeId { get; private set; }
    public string Ten { get; private set; } = "";
    public int SoNgay { get; private set; }
    public decimal Gia { get; private set; }
    public bool DangBan { get; private set; }

    private GoiCho() { }

    public GoiCho(int id, int loaiXeId, string ten, int soNgay, decimal gia, bool dangBan = true)
    {
        Id = id; LoaiXeId = loaiXeId; Ten = ten; SoNgay = soNgay; Gia = gia; DangBan = dangBan;
    }

    public const decimal GiaToiDa = 1_000_000_000m;

    /// <summary>Đổi giá: số dương, làm tròn tới nghìn đồng, không quá 1 tỷ. Chỉ ảnh hưởng lần mua sau.</summary>
    public void DoiGia(decimal giaMoi)
    {
        if (giaMoi <= 0 || giaMoi > GiaToiDa)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "GIA_SAI", "Giá phải lớn hơn 0 và không quá 1.000.000.000 đ.");
        if (giaMoi % 1000 != 0)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "GIA_SAI", "Giá phải tròn nghìn đồng (ví dụ 120.000).");
        Gia = giaMoi;
    }

    /// <summary>Giá khách thấy trên màn hình phải đúng giá hiện tại, nếu không thì báo để khách xem lại.</summary>
    public void KiemTraGiaKhachThay(decimal? giaKhachThay)
    {
        if (giaKhachThay is { } g && g != Gia)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "GIA_DA_DOI",
                $"Giá gói {Ten} vừa đổi từ {Tien(g)} đ thành {Tien(Gia)} đ. Vui lòng xem lại rồi thanh toán.");
    }

    // 30000 -> "30.000" (dấu chấm kiểu Việt Nam, không phụ thuộc ngôn ngữ của máy chủ)
    private static string Tien(decimal v) => v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');

    public void KiemTraBanDuocCho(Xe xe)
    {
        if (!DangBan)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "GOI_NGUNG_BAN", "Gói này đã ngừng bán.");
        if (LoaiXeId != xe.LoaiXeId)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "SAI_LOAI_XE", "Gói không dành cho loại xe này.");
    }
}
