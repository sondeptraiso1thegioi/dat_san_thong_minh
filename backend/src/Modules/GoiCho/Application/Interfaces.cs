using NhaXe.Modules.GoiCho.Contracts;
using NhaXe.Modules.GoiCho.Domain;
using GoiChoEntity = NhaXe.Modules.GoiCho.Domain.GoiCho;

namespace NhaXe.Modules.GoiCho.Application;

// ISP: tách nhỏ theo việc. Phía GHI (repository) tách khỏi phía ĐỌC (query) và khỏi phần gọi thủ tục mua gói.

public interface IXeRepository
{
    Task<Xe?> TimAsync(int xeId, CancellationToken ct);
    Task<bool> TonTaiBienSoAsync(string bienSo, CancellationToken ct);
    Task<IReadOnlyList<Xe>> LayChoDuyetAsync(CancellationToken ct);
    Task<int> DemChoDuyetCuaAsync(int taiKhoanId, CancellationToken ct);
    void Them(Xe xe);
    Task LuuAsync(CancellationToken ct);
}

/// <summary>
/// Kho lưu tệp (ảnh giấy đăng ký). Hiện lưu vào thư mục dùng chung (Docker volume) của các container API;
/// chuyển sang MinIO / S3 chỉ cần viết class mới cài interface này và đổi 1 dòng đăng ký DI (OCP + DIP).
/// </summary>
public interface ILuuTruTep
{
    /// <summary>Lưu tệp, trả về khoá (ví dụ giay-dk/12-3f2a.png) để ghi vào DB.</summary>
    Task<string> LuuAsync(string thuMuc, string duoi, Stream noiDung, CancellationToken ct);
    /// <summary>Mở tệp theo khoá; null nếu không có.</summary>
    Task<Stream?> MoAsync(string khoa, CancellationToken ct);
}

/// <summary>Đọc tham số cấu hình (bảng cau_hinh). Interface nhỏ riêng (ISP): use case chỉ cần đọc 1 số.</summary>
public interface ICauHinh
{
    Task<int> LaySoNguyenAsync(string khoa, int macDinh, CancellationToken ct);
}

public interface IGoiChoRepository
{
    Task<GoiChoEntity?> TimAsync(int goiId, CancellationToken ct);
    Task<IReadOnlyList<GoiChoEntity>> LayDangBanAsync(CancellationToken ct);
    Task<IReadOnlyList<GoiChoEntity>> LayTatCaAsync(CancellationToken ct);
    /// <summary>Lấy gói để sửa (được theo dõi thay đổi), rồi gọi LuuAsync.</summary>
    Task<GoiChoEntity?> TimDeSuaAsync(int goiId, CancellationToken ct);
    Task LuuAsync(CancellationToken ct);
    Task<bool> TonTaiLoaiXeAsync(int loaiXeId, CancellationToken ct);
    /// <summary>Mã loại xe (XE_MAY, O_TO). null: không có loại này.</summary>
    Task<string?> LayMaLoaiXeAsync(int loaiXeId, CancellationToken ct);
}

public interface IXeQuery
{
    /// <summary>Xe của tài khoản + xe bật chia sẻ của những người cùng nhóm (danh sách do NhomGiaDinh trả lời).
    /// Phần "đang trong bãi / nợ" không có ở đây: use case hỏi CongRaVao rồi ghép vào.</summary>
    Task<IReadOnlyList<XeDuocDungDto>> LayXeDuocDungAsync(int taiKhoanId, IReadOnlyCollection<int> nguoiCungNhom, CancellationToken ct);
    Task<IReadOnlyList<DangKyChoDto>> LayLichSuGoiAsync(int xeId, CancellationToken ct);
    Task<IReadOnlyList<ChoConBanDto>> LayChoConBanAsync(CancellationToken ct);
    Task<IReadOnlyList<TinhTrangXeRow>> LayTinhTrangXeAsync(IReadOnlyCollection<int> xeIds, CancellationToken ct);
    /// <summary>
    /// Xe đã duyệt, tìm "chứa" theo biển số ĐÃ chuẩn hoá (chỉ chữ hoa + số), lọc theo trạng thái gói, phân trang.
    /// Trả tổng số xe khớp (trước khi phân trang) và các xe của trang. TenChuXe / DangTrongBai do use case ghép.
    /// </summary>
    Task<(int Tong, IReadOnlyList<XeQuanLyDto> Ds)> TimXeDaDuyetAsync(string bienSoChuan, string loc, int boQua, int lay, CancellationToken ct);
    /// <summary>MỌI xe đã duyệt (gọn: id, biển, loại, chủ, hạn). Cổng ra vào dùng để khớp biển gần đúng.</summary>
    Task<IReadOnlyList<XeQuanLyDto>> LayXeDaDuyetAsync(CancellationToken ct);
    /// <summary>Số xe đang chờ duyệt (badge menu Admin).</summary>
    Task<int> DemChoDuyetAsync(CancellationToken ct);
    /// <summary>Tìm xe theo biển số đã chuẩn hoá (cổng ra vào hỏi). null = chưa đăng ký.</summary>
    Task<XeTaiCongRow?> TimXeTaiCongAsync(string bienSoChuan, CancellationToken ct);
}

/// <summary>Ghi đăng ký gói. Cài đặt gọi sp_mua_goi: thủ tục khoá theo loại xe để không bán vượt sức chứa.</summary>
public interface IDangKyChoStore
{
    Task<DangKyChoKetQua> MuaGoiAsync(int taiKhoanId, int xeId, int goiId, CancellationToken ct);
    Task<GoiDaMua?> LayGoiDaMuaAsync(int dangKyChoId, CancellationToken ct);
    /// <summary>Gọi sp_huy_goi: kiểm tra lại quy tắc, khoá, đổi trạng thái HUY và ghi HOAN_TIEN trong một giao dịch.</summary>
    Task HuyGoiAsync(int taiKhoanId, int dangKyChoId, decimal soTienHoan, string? lyDo, string? gopY, CancellationToken ct);
    /// <summary>Gọi sp_admin_huy_goi: ghi lý do, hoàn tiền (nếu có). Thông báo do use case gửi qua IThongBaoModule.</summary>
    Task AdminHuyGoiAsync(int adminId, int dangKyChoId, string lyDo, decimal soTienHoan, CancellationToken ct);
}

/// <summary>Thao tác quản trị trên xe (ISP: tách khỏi IXeRepository vì chỉ Admin dùng).</summary>
public interface IQuanLyXeStore
{
    /// <summary>Gọi sp_go_xe: gỡ mềm, huỷ gói còn hạn. Thông báo do use case gửi qua IThongBaoModule.</summary>
    Task GoXeAsync(int adminId, int xeId, string lyDo, CancellationToken ct);
}

/// <summary>Đọc sức chứa / suất còn bán (view v_cho_con_ban, cùng nguồn với lúc chặn bán).</summary>
public interface ISucChuaQuery
{
    Task<IReadOnlyList<SucChuaLoaiXe>> LaySucChuaAsync(CancellationToken ct);
}

/// <summary>Phía GHI sức chứa.</summary>
public interface ISucChuaStore
{
    /// <summary>Gọi sp_doi_suc_chua: khoá loại xe, kiểm tra lại số suất đang chiếm rồi mới đổi.</summary>
    Task DoiSucChuaAsync(int adminId, int loaiXeId, int sucChua, CancellationToken ct);
}

public interface IPhiQuaHanStore
{
    /// <summary>Nợ phí quá hạn của các lượt gửi đang mở (id lượt gửi lấy từ bản sao xe_trong_bai).</summary>
    Task<IReadOnlyList<PhiQuaHanDto>> LayNoAsync(IReadOnlyCollection<int> luotGuiIds, CancellationToken ct);
    Task ThanhToanAsync(int taiKhoanId, int luotGuiId, CancellationToken ct);
}

/// <summary>
/// Cổng thanh toán (OCP + LSP): hôm nay dùng ThanhToanGiaLap. Thêm VNPay/MoMo = viết class mới cài interface này,
/// KHÔNG sửa MuaGoiUseCase. Mọi cài đặt phải thay thế được cho nhau.
/// </summary>
public interface IThanhToanGateway
{
    Task<KetQuaThanhToan> ThuTienAsync(int taiKhoanId, decimal soTien, string noiDung, CancellationToken ct);
    Task HoanTienAsync(string maGiaoDich, CancellationToken ct);
}

public record KetQuaThanhToan(bool ThanhCong, string MaGiaoDich, string? LyDo = null);

/// <summary>Đọc tổng tiền theo loại thanh toán (bảng thanh_toan của GoiCho) cho báo cáo.</summary>
public interface IDoanhThuQuery
{
    Task<NhaXe.Modules.GoiCho.Contracts.DoanhThu> LayAsync(DateTime tu, DateTime den, CancellationToken ct);
}
