namespace NhaXe.Modules.CongRaVao.Application;

/// <summary>Kết quả một lần xe tới cổng (vào hoặc ra).</summary>
/// <param name="ChoQua">true = barie mở.</param>
/// <param name="DangChoXacNhan">true = cổng ra đang chờ hai bên xác nhận (chưa mở, chưa từ chối).</param>
/// <param name="UngVien">AI không chắc / biển gần giống nhiều xe: các biển để Admin bấm chọn rồi gửi lại (UC-A08).</param>
/// <param name="CheDoThuCong">AI lỗi hoặc quá 3 giây: màn hình cổng yêu cầu Admin nhập biển số.</param>
/// <param name="CanAdmin">Kết quả cần Admin xem (từ chối ở cổng ra, biển lạ, ứng viên để chọn).</param>
/// <param name="ChoThanhToan">Người dùng đã xác nhận nhưng còn phí quá hạn: trả phí trên app rồi barrier mới mở.</param>
/// <param name="KhungBien">Vị trí biển AI tìm thấy trên ảnh (tỉ lệ 0..1), để màn hình camera vẽ ô vuông.</param>
/// <param name="KhongThayBien">Chế độ tự quét: khung hình bị bỏ qua (không thấy biển / biển vừa xử lý), không ghi nhật ký.</param>
public record KetQuaCongDto(string Loai, bool ChoQua, bool DangChoXacNhan, string? BienSo, string ThongDiep,
                            int? LuotGuiId = null, int? YeuCauId = null, DateTime? HetHanLuc = null, int? SoGiayConLai = null,
                            IReadOnlyList<string>? UngVien = null, decimal? DoTinCay = null,
                            string? LoaiBien = null, bool KhopGanDung = false, bool CheDoThuCong = false, bool CanAdmin = false,
                            bool ChoThanhToan = false, int? XeId = null,
                            NhaXe.Modules.AiBienSo.Contracts.KhungBien? KhungBien = null, bool KhongThayBien = false,
                            DateTime? ThoiGianVao = null, bool CoAnhVao = false);

/// <summary>Trạng thái một lượt gửi cho màn hình camera cổng ra (hỏi lại mỗi 2 giây trong lúc chờ xác nhận trên app).</summary>
public record LuotCongDto(int Id, int XeId, string TrangThai, DateTime ThoiGianVao, DateTime? ThoiGianRa, bool CoAnhVao, bool CoAnhRa);

public record YeuCauDto(int Id, int LuotGuiId, int XeId, string BienSo, int ChuXeId, string TenChuXe,
                        DateTime TaoLuc, DateTime HetHanLuc, int SoGiayConLai,
                        bool? NguoiDungXeDongY, string? NguoiDungXeTraLoi, bool? QuanLyDongY, string? QuanLyTraLoi);

public record SuKienCongDto(int Id, string Loai, string? BienSo, decimal? DoTinCay, string KetQua, string? LyDo, DateTime ThoiGian);

public record XeChonNhanhDto(int XeId, string BienSo, bool TrongBai);

/// <summary>Màn hình cổng của quản lý: yêu cầu đang chờ, nhật ký gần đây, danh sách xe để chọn nhanh khi giả lập.</summary>
public record ManHinhCongDto(DateTime BayGio, IReadOnlyList<YeuCauDto> DangCho, IReadOnlyList<SuKienCongDto> GanDay,
                             IReadOnlyList<XeChonNhanhDto> Xe, IReadOnlyList<LuotCanXuLyDto>? CanXuLy = null);

/// <param name="YeuCauKetQua">Kết quả yêu cầu xác nhận ra gần nhất (CHO / DONG_Y / TU_CHOI / HET_HAN), null nếu chưa ra cổng lần nào.</param>
public record LuotGuiDto(int Id, int XeId, string BienSo, DateTime ThoiGianVao, DateTime? ThoiGianRa, string TrangThai,
                         decimal? PhiQuaHan = null, string? YeuCauKetQua = null, DateTime? YeuCauHetHanLuc = null);

/// <summary>Trang Ra vào của khách: yêu cầu cần mình xác nhận và lịch sử ra vào các xe mình được dùng.</summary>
public record RaVaoCuaToiDto(DateTime BayGio, IReadOnlyList<YeuCauDto> DangCho, IReadOnlyList<LuotGuiDto> LichSu);

/// <summary>Lượt gửi cần Admin xử lý: nghi mất xe, chờ thanh toán, hết 60 giây không ai xác nhận.</summary>
public record LuotCanXuLyDto(int LuotGuiId, int XeId, string BienSo, DateTime ThoiGianVao, string TrangThai, string LyDo);

public record XeTaiCongLenh(string? BienSo);
/// <summary>Admin mở barrier thủ công (UC-A10): bắt buộc lý do.</summary>
public record MoTayLenh(int LuotGuiId, string? LyDo);
public record TraLoiLenh(bool DongY, string? LyDo);
