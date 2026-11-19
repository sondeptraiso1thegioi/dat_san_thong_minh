using NhaXe.SharedKernel;

namespace NhaXe.Modules.TaiKhoan.Domain;

public static class VaiTro
{
    public const string Admin = "ADMIN";
    public const string User = "USER";
}

/// <summary>Thực thể tài khoản. Chỉ chứa dữ liệu và quy tắc của chính nó (SRP), không biết tới DB hay HTTP.</summary>
public class TaiKhoan
{
    public int Id { get; private set; }
    public string HoTen { get; private set; } = "";
    public string Sdt { get; private set; } = "";
    public string? Email { get; private set; }
    public string MatKhauHash { get; private set; } = "";
    public string VaiTro { get; private set; } = Domain.VaiTro.User;
    public string TrangThai { get; private set; } = "HOAT_DONG";
    public string? PhienHienTai { get; private set; }
    public DateTime? DangNhapLuc { get; private set; }
    /// <summary>Mã đẩy thông báo (FCM) của máy đăng nhập gần nhất: đăng nhập máy mới thì ghi đè (v10, module 1).</summary>
    public string? MaDay { get; private set; }
    public DateTime TaoLuc { get; private set; }
    public string? MaKhoiPhucHash { get; private set; }
    public DateTime? MaKhoiPhucHetHanLuc { get; private set; }
    public DateTime? GuiMaKhoiPhucLuc { get; private set; }
    /// <summary>Số lần sai mật khẩu liên tiếp (đúng 1 lần là về 0).</summary>
    public int SoLanSai { get; private set; }
    public DateTime? KhoaDangNhapDen { get; private set; }
    public byte[] PhienBan { get; private set; } = [];

    private TaiKhoan() { } // cho EF Core

    public TaiKhoan(int id, string hoTen, string sdt, string matKhauHash, string vaiTro, string trangThai = "HOAT_DONG")
    {
        Id = id; HoTen = hoTen; Sdt = sdt; MatKhauHash = matKhauHash; VaiTro = vaiTro; TrangThai = trangThai;
    }

    public bool BiKhoa => TrangThai == "KHOA";

    /// <summary>
    /// Cứ mỗi 5 lần sai liên tiếp thì tạm khoá đăng nhập: 15 phút, rồi 30, tối đa 60 phút.
    /// Trần 60 phút (không phải 24 giờ) vì người lạ cũng nhập sai được: khoá dài = dễ bị phá để chặn chủ tài khoản.
    /// Chủ tài khoản luôn tự mở ngay bằng Quên mật khẩu (đổi mật khẩu thì xoá khoá).
    /// </summary>
    public const int SoLanSaiMoiDot = 5;

    /// <summary>Gọi TRƯỚC khi kiểm mật khẩu: đang tạm khoá thì không cho thử tiếp (thử sai lúc này cũng không tính).</summary>
    public void KiemTraChoThuDangNhap(DateTime luc)
    {
        if (KhoaDangNhapDen is { } den && luc < den)
            throw LoiDangNhap(); // CÙNG thông báo với sai mật khẩu: không lộ "số này có tài khoản và đang bị khoá"
    }

    /// <summary>
    /// Một thông báo chung cho: sai số điện thoại, sai mật khẩu, đang tạm khoá. Người dò không phân biệt được ba trường hợp;
    /// người dùng thật vẫn được nhắc có khoá tạm và cách mở.
    /// </summary>
    public static LoiNghiepVu LoiDangNhap() => new(LoaiLoi.ChuaDangNhap, "SAI_DANG_NHAP",
        "Số điện thoại hoặc mật khẩu không đúng. Sai 5 lần liên tiếp sẽ bị tạm khoá 15 phút; dùng Quên mật khẩu để mở ngay.");

    public void GhiSaiMatKhau(DateTime luc)
    {
        SoLanSai++;
        if (SoLanSai % SoLanSaiMoiDot != 0) return;
        var dot = SoLanSai / SoLanSaiMoiDot;                     // đợt thứ mấy bị khoá
        var phut = Math.Min(15 * (1 << Math.Min(dot - 1, 2)), 60);
        KhoaDangNhapDen = luc.AddMinutes(phut);
    }

    public static TaiKhoan DangKy(string hoTen, string sdt, string email, string hash) =>
        new(0, hoTen, sdt, hash, Domain.VaiTro.User) { Email = email };

    /// <summary>Admin đầu tiên của hệ thống, tạo từ cấu hình lúc khởi động (đăng ký công khai chỉ tạo được USER).</summary>
    public static TaiKhoan TaoAdmin(string hoTen, string sdt, string email, string hash) =>
        new(0, hoTen, sdt, hash, Domain.VaiTro.Admin) { Email = email };

    public void DongPhien() => PhienHienTai = null;

    public bool CoTheGuiMa(DateTime luc) => !BiKhoa && Email is not null &&
        (GuiMaKhoiPhucLuc is null || luc >= GuiMaKhoiPhucLuc.Value.AddMinutes(1));

    public void CapMaKhoiPhuc(string hash, DateTime luc)
    {
        if (!CoTheGuiMa(luc))
            throw new LoiNghiepVu(LoaiLoi.XungDot, "CHUA_THE_GUI_MA", "Chưa thể gửi mã khôi phục.");
        MaKhoiPhucHash = hash;
        GuiMaKhoiPhucLuc = luc;
        MaKhoiPhucHetHanLuc = luc.AddMinutes(15);
    }

    /// <summary>
    /// Gửi email thất bại: bỏ mã vừa cấp và mốc thời gian gửi, để người dùng bấm lại được ngay
    /// (không bị chặn 1 phút cho một email chưa hề đi). Chỉ huỷ nếu mã hiện tại vẫn là mã đó.
    /// </summary>
    public void HuyMaKhoiPhucChuaGui(string hash, DateTime? guiLucTruoc)
    {
        if (MaKhoiPhucHash != hash) return;
        MaKhoiPhucHash = null;
        MaKhoiPhucHetHanLuc = null;
        GuiMaKhoiPhucLuc = guiLucTruoc;
    }

    public void KiemTraMaKhoiPhuc(string hash, DateTime luc)
    {
        if (BiKhoa || MaKhoiPhucHash is null || MaKhoiPhucHash != hash ||
            MaKhoiPhucHetHanLuc is null || luc >= MaKhoiPhucHetHanLuc)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "MA_KHOI_PHUC_SAI", "Mã khôi phục không hợp lệ hoặc đã hết hạn.");
    }

    public void DoiMatKhau(string hash)
    {
        if (BiKhoa)
            throw new LoiNghiepVu(LoaiLoi.KhongCoQuyen, "TAI_KHOAN_KHOA", "Tài khoản đã bị khoá.");
        MatKhauHash = hash;
        MaKhoiPhucHash = null;
        MaKhoiPhucHetHanLuc = null;
        SoLanSai = 0; KhoaDangNhapDen = null; // đặt lại mật khẩu thành công thì mở khoá đăng nhập
        DongPhien();
    }

    /// <summary>Mở phiên mới. Phiên cũ (máy khác) mất hiệu lực vì bị ghi đè.</summary>
    public void MoPhienMoi(string phien, DateTime luc, string? maDay = null)
    {
        if (BiKhoa)
            throw new LoiNghiepVu(LoaiLoi.KhongCoQuyen, "TAI_KHOAN_KHOA", "Tài khoản đã bị khoá.");
        PhienHienTai = phien;
        DangNhapLuc = luc;
        MaDay = string.IsNullOrWhiteSpace(maDay) ? null : maDay.Trim(); // máy mới không gửi mã thì máy cũ cũng không nhận đẩy nữa
        SoLanSai = 0; KhoaDangNhapDen = null;
    }

    /// <summary>Admin khoá tài khoản người dùng: máy đang đăng nhập mất phiên ngay. Không khoá được Admin.</summary>
    public void KhoaBoiAdmin()
    {
        if (VaiTro == Domain.VaiTro.Admin)
            throw new LoiNghiepVu(LoaiLoi.KhongCoQuyen, "KHONG_KHOA_ADMIN", "Không khoá được tài khoản quản trị.");
        TrangThai = "KHOA";
        DongPhien();
        MaDay = null;
    }

    public void MoKhoaBoiAdmin()
    {
        TrangThai = "HOAT_DONG";
        SoLanSai = 0; KhoaDangNhapDen = null;
    }
}
