namespace NhaXe.Modules.GoiCho.Contracts;

/// <summary>
/// Cổng giao tiếp CÔNG KHAI của module GoiCho (Xe &amp; mua gói), theo Modular Monolith (Buổi 12).
/// Module khác (BaiXe, CongRaVao, sau này TimXe, BaoCao...) chỉ hỏi GoiCho qua interface này,
/// không đọc bảng xe / dang_ky_cho và không dùng class nội bộ của GoiCho.
/// </summary>
public interface IGoiChoModule
{
    /// <summary>Sức chứa và số suất còn bán của từng loại xe.</summary>
    Task<IReadOnlyList<SucChuaLoaiXe>> LaySucChuaAsync(CancellationToken ct = default);

    /// <summary>Biển số, chủ xe, hạn gói, nợ quá hạn của các xe (khoá theo xeId).</summary>
    Task<IReadOnlyDictionary<int, TinhTrangXeGoi>> LayTinhTrangXeAsync(IEnumerable<int> xeIds, CancellationToken ct = default);

    /// <summary>
    /// Cổng ra vào hỏi: biển số này là xe nào, đã duyệt chưa, còn gói không, còn nợ không.
    /// null = biển số chưa đăng ký. Biển số sai định dạng thì ném LoiNghiepVu loại DuLieuKhongHopLe.
    /// </summary>
    Task<XeTaiCong?> TimXeTaiCongAsync(string bienSo, CancellationToken ct = default);

    /// <summary>
    /// Khớp gần đúng (lệch tối đa 1 ký tự) với các xe ĐÃ DUYỆT khi không có xe nào trùng hẳn (đề cương v10).
    /// chiTrongXeIds khác null: chỉ so với các xe này (cổng ra so với xe đang trong bãi).
    /// Kết quả: đúng một ứng viên thì Xe khác null; nhiều ứng viên thì Xe = null và UngVien để Admin chọn.
    /// </summary>
    Task<KetQuaKhopGanDung> TimXeGanDungAsync(string bienSo, IReadOnlyCollection<int>? chiTrongXeIds = null, CancellationToken ct = default);

    /// <summary>Đã trả phí quá hạn (hoặc không còn nợ) cho xe này chưa. Cổng ra dùng khi lượt gửi đang chờ thanh toán.</summary>
    Task<decimal> LaySoTienNoAsync(int xeId, CancellationToken ct = default);

    /// <summary>Doanh thu thanh toán THÀNH CÔNG trong [tu, den) cho báo cáo Admin (module BaoCao).</summary>
    Task<DoanhThu> LayDoanhThuAsync(DateTime tu, DateTime den, CancellationToken ct = default) =>
        Task.FromResult(new DoanhThu(0, 0, 0, 0));

    /// <summary>Những người được dùng xe: chủ xe, cộng người cùng nhóm nếu xe bật chia sẻ (để gửi thông báo, xác nhận ra).</summary>
    Task<IReadOnlyList<int>> LayNguoiDungXeAsync(int xeId, CancellationToken ct = default);

    /// <summary>Các xe tài khoản này được dùng (xe của mình + xe được chia sẻ trong nhóm).</summary>
    Task<IReadOnlyList<XeTomTat>> LayXeDuocDungAsync(int taiKhoanId, CancellationToken ct = default);

    /// <summary>Mọi xe đã duyệt (màn hình cổng dùng để chọn nhanh khi giả lập).</summary>
    Task<IReadOnlyList<XeTomTat>> LayXeDaDuyetAsync(CancellationToken ct = default);
}

public record SucChuaLoaiXe(int LoaiXeId, string LoaiXe, int SucChua, int SoXeGiuSuat, int SoXeQuaHanTrongBai, int SoChoConBan)
{
    /// <summary>Suất đang bị chiếm = xe giữ gói còn hạn + xe quá hạn còn trong bãi.</summary>
    public int DangChiem => SoXeGiuSuat + SoXeQuaHanTrongBai;
    public bool HetCho => SoChoConBan <= 0;
}

public record TinhTrangXeGoi(int XeId, string BienSo, int LoaiXeId, string LoaiXe, int ChuXeId,
                             DateTime? HetHanLuc, int SoNgayNo, decimal SoTienNo);

/// <summary>Thông tin cổng cần để quyết định cho xe vào / ra.</summary>
public record XeTaiCong(int XeId, string BienSo, string LoaiXe, int ChuXeId, bool DaDuyet, bool CoGoiHieuLuc,
                        DateTime? HetHanLuc, decimal SoTienNo);

/// <param name="Thuc">Mua gói + phí quá hạn − hoàn tiền.</param>
public record DoanhThu(decimal MuaGoi, decimal PhiQuaHan, decimal HoanTien, int SoGiaoDich)
{
    public decimal Thuc => MuaGoi + PhiQuaHan - HoanTien;
}

public record KetQuaKhopGanDung(XeTaiCong? Xe, IReadOnlyList<string> UngVien);

public record XeTomTat(int XeId, string BienSo, int LoaiXeId, int ChuXeId);
