namespace NhaXe.SharedKernel;

/// <summary>Loại lỗi nghiệp vụ. Tầng Api đổi loại lỗi thành mã HTTP; tầng trong không biết gì về HTTP.</summary>
public enum LoaiLoi
{
    DuLieuKhongHopLe,   // 400
    ChuaDangNhap,       // 401
    KhongCoQuyen,       // 403
    KhongTimThay,       // 404
    XungDot,            // 409: hết chỗ, còn nợ, biển số trùng...
    QuaNhieuLan,        // 429: sai mật khẩu quá nhiều lần, đang tạm khoá
    DichVuTamNgung      // 503: dịch vụ bên ngoài (máy chủ email, mô hình AI...) đang không dùng được, thử lại sau
}

/// <summary>Lỗi do vi phạm quy tắc nghiệp vụ (không phải lỗi hệ thống). Ma là mã máy đọc, Message là câu cho người đọc.</summary>
public sealed class LoiNghiepVu : Exception
{
    public LoaiLoi Loai { get; }
    public string Ma { get; }

    public LoiNghiepVu(LoaiLoi loai, string ma, string thongDiep) : base(thongDiep)
    {
        Loai = loai;
        Ma = ma;
    }
}
