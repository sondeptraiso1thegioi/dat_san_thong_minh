// Module 5. CongRaVao: khớp DTO của NhaXe.Modules.CongRaVao.Application.

export interface KetQuaCong {
  loai: 'VAO' | 'RA';
  choQua: boolean;
  dangChoXacNhan: boolean;
  bienSo: string | null;
  thongDiep: string;
  luotGuiId: number | null;
  yeuCauId: number | null;
  hetHanLuc: string | null;
  soGiayConLai: number | null;
  /** AI chưa chắc: các biển AI đoán, bảo vệ bấm chọn rồi gửi lại. */
  ungVien?: string[] | null;
  /** Điểm tin cậy của lần đọc biển (1 = Admin gõ tay). */
  doTinCay?: number | null;
  /** Loại biển AI suy ra từ dạng biển: XE_MAY / O_TO / KHONG_RO. */
  loaiBien?: string | null;
  /** Biển đọc được lệch ≤ 1 ký tự so với đúng một xe: đã khớp gần đúng. */
  khopGanDung?: boolean;
  /** AI lỗi hoặc quá 3 giây: Admin nhập biển số tay. */
  cheDoThuCong?: boolean;
  /** Kết quả cần Admin xem (từ chối ở cổng ra, biển lạ, có ứng viên để chọn). */
  canAdmin?: boolean;
  /** Người dùng đã xác nhận nhưng còn phí quá hạn: trả trên app rồi barrier mới mở. */
  choThanhToan?: boolean;
  xeId?: number | null;
  /** Vị trí biển trên ảnh (tỉ lệ 0..1 theo chiều rộng / cao), để vẽ ô vuông đè lên hình camera. */
  khungBien?: KhungBien | null;
  /** Chế độ tự quét: khung hình không thấy biển nên bỏ qua (không ghi nhật ký). */
  khongThayBien?: boolean;
  /** Giờ xe vào (cổng ra dùng để tính thời gian đỗ) và lượt có ảnh đối chứng lúc vào không. */
  thoiGianVao?: string | null;
  coAnhVao?: boolean;
}

/** Trạng thái lượt gửi: màn hình cổng ra hỏi lại trong lúc chờ xác nhận trên app. */
export interface LuotCong { id: number; xeId: number; trangThai: TrangThaiLuot; thoiGianVao: string; thoiGianRa: string | null; coAnhVao: boolean; coAnhRa: boolean }

export interface KhungBien { x: number; y: number; rong: number; cao: number }

/**
 * Yêu cầu xác nhận xe ra (60 giây), gửi mọi người được dùng xe: ai trả lời trước thì tính.
 * nguoiDungXe*: người trả lời đầu tiên. quanLy*: Admin mở barrier thủ công (UC-A10). null = chưa trả lời.
 */
export interface YeuCauXacNhan {
  id: number;
  luotGuiId: number;
  xeId: number;
  bienSo: string;
  chuXeId: number;
  tenChuXe: string;
  taoLuc: string;
  hetHanLuc: string;
  soGiayConLai: number;
  nguoiDungXeDongY: boolean | null;
  nguoiDungXeTraLoi: string | null;
  quanLyDongY: boolean | null;
  quanLyTraLoi: string | null;
}

export interface SuKienCong {
  id: number;
  loai: 'VAO' | 'RA';
  bienSo: string | null;
  doTinCay: number | null;
  ketQua: 'CHO_QUA' | 'TU_CHOI';
  lyDo: string | null;
  thoiGian: string;
}

export interface XeChonNhanh { xeId: number; bienSo: string; trongBai: boolean }

/** Lượt cần Admin xử lý: nghi mất xe, chờ thanh toán, hết 60 giây không ai xác nhận. */
export interface LuotCanXuLy { luotGuiId: number; xeId: number; bienSo: string; thoiGianVao: string; trangThai: TrangThaiLuot; lyDo: string }

export interface ManHinhCong { bayGio: string; dangCho: YeuCauXacNhan[]; ganDay: SuKienCong[]; xe: XeChonNhanh[]; canXuLy?: LuotCanXuLy[] | null }

export type TrangThaiLuot = 'DANG_GUI' | 'CHO_XAC_NHAN' | 'DA_RA' | 'NGHI_MAT' | 'CHO_THANH_TOAN';

export interface LuotGui {
  id: number; xeId: number; bienSo: string; thoiGianVao: string; thoiGianRa: string | null; trangThai: TrangThaiLuot; phiQuaHan?: number | null;
  /** Yêu cầu xác nhận ra gần nhất của lượt (để báo "đã hết hạn lúc …" khi mở từ thông báo cũ). */
  yeuCauKetQua?: 'CHO' | 'DONG_Y' | 'TU_CHOI' | 'HET_HAN' | null;
  yeuCauHetHanLuc?: string | null;
}

export interface RaVaoCuaToi { bayGio: string; dangCho: YeuCauXacNhan[]; lichSu: LuotGui[] }

export const TEN_TRANG_THAI_LUOT: Record<TrangThaiLuot, string> = {
  DANG_GUI: 'Đang gửi',
  CHO_XAC_NHAN: 'Đang chờ xác nhận ra',
  DA_RA: 'Đã ra',
  NGHI_MAT: 'Nghi mất xe',
  CHO_THANH_TOAN: 'Chờ thanh toán',
};
