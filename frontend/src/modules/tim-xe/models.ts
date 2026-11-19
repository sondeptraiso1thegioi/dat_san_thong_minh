// Module TimXe (xe máy, định vị mức khu): khớp DTO của NhaXe.Modules.TimXe.Application.

export type MucCho = 'CON_NHIEU' | 'SAP_DAY' | 'DAY';
export type TrangThaiGiu = 'DANG_GIU' | 'DA_VAO' | 'HET_HAN' | 'HUY';

/** Suất được giữ trong khu sau khi qua cổng vào (mặc định 5 phút). */
export interface ChiDan { luotGuiId: number; khuId: number; khu: string; hetHanLuc: string; trangThai: TrangThaiGiu }

export interface KhuCho {
  id: number; ten: string; loaiXeId: number; sucChua: number;
  soXe: number; soGiu: number; conLai: number; muc: MucCho; khoangCachTuCong: number | null;
  /** Mã chấm trên ảnh sơ đồ (R, S, HAM). Backend thêm sau (cột khu_vuc.ma_so_do); chưa có thì frontend suy theo tên. */
  maSoDo?: string | null;
}

export interface DiemDuong { nutId: number; ten: string; x: number; y: number }
/** Đường ngắn nhất từ cổng tới khu (Dijkstra trên đồ thị nút / cạnh của bãi). */
export interface DuongDi { khuId: number; tongMet: number; diem: DiemDuong[] }

/** QUET: nguồn camera của dữ liệu mẫu cũ (trước V004), hiển thị như camera. */
export type NguonKhu = 'CAMERA_KHU' | 'QUET' | 'NGUOI_DUNG' | 'ADMIN';

export interface XeTimXe {
  xeId: number; bienSo: string; loaiXe: string; trongBai: boolean; thoiGianVao: string | null;
  khuId: number | null; khu: string | null; vaoKhuLuc: string | null; nguonKhu: NguonKhu | null;
  moiVao: ChiDan | null; duong: DuongDi | null;
  /** false: ô tô, định vị trong khu chưa làm. */
  hoTroDinhVi: boolean;
}

export interface TimXeCuaToi { bayGio: string; xe: XeTimXe[]; khuXeMay: KhuCho[] }

/** Số xe từng khu (mọi loại xe): camera hướng vào +1, camera hướng ra −1. Admin và người dùng cùng xem. */
export interface SoXeKhu { bayGio: string; khu: KhuCho[] }

/**
 * DA_GHI: đã ghi vào / ra khu · BO_QUA: đọc lặp hoặc không cần ghi · KHONG_KHOP: biển không thuộc xe nào trong bãi
 * KHONG_THAY_BIEN: khung không có biển · KHU_DAY: khu đã đầy, KHÔNG +1 (nhân viên hướng dẫn xe sang khu khác).
 */
export type KetQuaCamera = 'DA_GHI' | 'BO_QUA' | 'KHONG_KHOP' | 'KHONG_THAY_BIEN' | 'KHU_DAY';

/** Một lần đi sai khu (Admin, trang Quản lý bãi). khuChiDinh null: xe không có chỉ dẫn mà tới khu đầy. */
export interface DiSaiKhu { id: number; bienSo: string; tenChuXe: string; khuChiDinh: string | null; khuThucTe: string; khuDay: boolean; luc: string }
export interface DemDiSai { chuXeId: number; tenChuXe: string; sdt: string; soLan: number; soLanKhuDay: number; lanCuoi: string }
export interface DiSaiKhuAdmin { ganDay: DiSaiKhu[]; theoChuXe: DemDiSai[] }
export interface KetQuaCameraKhu {
  ketQua: KetQuaCamera; bienSo: string | null; khuId: number; huong: 'VAO' | 'RA' | null; thongDiep: string; ungVien: string[] | null;
  doTinCay: number | null; khungBien: { x: number; y: number; rong: number; cao: number } | null;
  khu: string | null; soXeTrongKhu: number | null; sucChua: number | null;
}

export const TEN_MUC: Record<MucCho, string> = { CON_NHIEU: 'Còn nhiều', SAP_DAY: 'Sắp đầy', DAY: 'Đầy' };
export const TEN_NGUON: Record<NguonKhu, string> = { CAMERA_KHU: 'camera lối vào khu', QUET: 'camera', NGUOI_DUNG: 'bạn bấm "Đã đỗ xe"', ADMIN: 'quản lý bãi' };

// ---------------- Điều hướng trong bãi (mở rộng luồng chọn khu + giữ suất) ----------------

/**
 * DANG_DAN: đang chỉ đường (suất đang giữ) · DA_TOI: camera VÀO của khu đích thấy xe
 * TOI_KHU_KHAC: camera VÀO của khu khác thấy xe (tin camera, không phạt) · HET_HAN: quá giờ giữ mà camera chưa thấy xe
 * DA_DONG: xe ra cổng khi đang dẫn đường.
 */
export type TrangThaiPhien = 'DANG_DAN' | 'DA_TOI' | 'TOI_KHU_KHAC' | 'HET_HAN' | 'DA_DONG';

/** Một điểm trên đường đi, toạ độ theo tỉ lệ ảnh sơ đồ (0..1) để ảnh co giãn vẫn đúng. */
export interface DiemBanDo { x: number; y: number; ten: string | null }
/** Đường ngắn nhất (Dijkstra trên đồ thị mốc) từ Cổng vào X tới đầu khu. */
export interface DuongDiBanDo { denKhuId: number; tongMet: number; diem: DiemBanDo[] }

export interface PhienDieuHuong {
  id: number; luotGuiId: number; xeId: number; bienSo: string;
  trangThai: TrangThaiPhien;
  batDauLuc: string;
  /** Hết giờ giữ suất (mặc định 5 phút; đổi khu đích thì GIỮ NGUYÊN thời hạn còn lại). */
  hetHanLuc: string;
  /** Khu đích hiện tại (hệ thống gợi ý, hoặc khu người dùng tự chọn). Số chỗ đã trừ suất đang giữ. */
  khuDich: KhuCho;
  /** Khu hệ thống gợi ý lúc vào cổng (khác khuDich nếu người dùng đã đổi). */
  khuGoiYId: number;
  /** Khu camera thật sự thấy xe vào (DA_TOI, TOI_KHU_KHAC). */
  khuThucTe: { id: number; ten: string; vaoLuc: string } | null;
  /** Đường từ cổng tới khu đích (chỉ khi DANG_DAN). */
  duong: DuongDiBanDo | null;
  /** Xe tới khu đã đầy: nhân viên không cho vào, camera không +1, phiên vẫn DANG_DAN tới khu đích. */
  canhBao: { khuId: number; ten: string; luc: string } | null;
}

/** GET /api/dieu-huong/cua-toi. hetCho: xe máy vừa vào cổng nhưng mọi khu đã đầy (không có phiên). */
export interface DieuHuongCuaToi { bayGio: string; phien: PhienDieuHuong | null; hetCho: { bienSo: string; luc: string } | null }
