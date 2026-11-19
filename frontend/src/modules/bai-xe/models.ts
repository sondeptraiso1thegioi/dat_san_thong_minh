// Module BaiXe: sơ đồ bãi cho Admin, khớp DTO của NhaXe.Modules.BaiXe.
import type { LoaiXeMa } from '@/shared/models';

export type TinhTrangGoi = 'CON_HAN' | 'SAP_HET' | 'QUA_HAN' | 'KHONG_CO_GOI';

/** Sức chứa (lấy từ module GoiCho) ghép với số liệu của bãi. */
export interface SucChuaBai {
  loaiXeId: number;
  loaiXe: LoaiXeMa;
  sucChua: number;
  soXeGiuSuat: number;
  soXeQuaHanTrongBai: number;
  soChoConBan: number;
  soXeTrongBai: number;
  soODo: number;
  dangChiem: number;
  hetCho: boolean;
}

export interface XeTrongBai {
  luotGuiId: number;
  xeId: number;
  bienSo: string;
  loaiXeId: number;
  loaiXe: LoaiXeMa;
  chuXeId: number;
  tenChuXe: string;
  thoiGianVao: string;
  khuId: number | null;
  khu: string | null;
  oId: number | null;
  maO: string | null;
  viTriLuc: string | null;
  hetHanLuc: string | null;
  soNgayNo: number;
  soTienNo: number;
  tinhTrangGoi: TinhTrangGoi;
}

/** Trạng thái ô do Admin đặt tay hoặc AI ô trống báo. AI báo quá 15 phút thì thành KHONG_RO. */
export type TrangThaiO = 'TRONG' | 'CO_XE' | 'KHONG_RO';

export interface ODo {
  id: number;
  maO: string;
  xe: XeTrongBai | null;
  trangThai: TrangThaiO;
}

export interface Khu {
  id: number;
  ten: string;
  loaiXeId: number;
  loaiXe: LoaiXeMa;
  soO: number;
  soOTrong: number;
  soXe: number;
  o: ODo[];
  xeChuaRoO: XeTrongBai[];
}

export interface SoDoBai {
  capNhatLuc: string;
  sucChua: SucChuaBai[];
  khu: Khu[];
  xeChuaDinhVi: XeTrongBai[];
  xeTrongBai: XeTrongBai[];
}

// ---------- Quản lý bãi (Admin) ----------

export interface LoaiXe { id: number; ten: LoaiXeMa }
/** sucChua: số xe tối đa của khu (khu xe máy dùng để tính mức chỗ và chọn khu). */
/** maSoDo: mã chấm trên ảnh sơ đồ (R, S, HAM). dangDung = false: khu tạm tắt (không chỉ dẫn xe vào, người dùng không thấy). */
export interface KhuQuanLy { id: number; ten: string; loaiXeId: number; soO: number; soCamera: number; sucChua: number; maSoDo: string | null; dangDung: boolean }
export interface OQuanLy {
  id: number; khuId: number; maO: string; maQr: string; cameraId: number | null;
  trangThai: TrangThaiO; nguon: 'ADMIN' | 'AI' | null; capNhatLuc: string | null;
}
/**
 * KHU_VAO: camera khu soi hướng xe đi vào (ghi xe vào khu, số xe +1). KHU_RA: soi hướng xe đi ra (số xe −1).
 * LOI_VAO_KHU: kiểu cũ, một camera dùng chung hai hướng. GIAM_SAT: camera khác.
 */
export type VaiTroCamera = 'KHU_VAO' | 'KHU_RA' | 'LOI_VAO_KHU' | 'GIAM_SAT';
export interface CameraBai { id: number; khuId: number; ten: string; nguonAnh: string; dangBat: boolean; vaiTro: VaiTroCamera }
export interface CauHinhBai { loaiXe: LoaiXe[]; khu: KhuQuanLy[]; o: OQuanLy[]; camera: CameraBai[] }

export interface KhuLenh { ten: string; loaiXeId: number; sucChua?: number | null; dangDung?: boolean | null }
export interface OLenh { khuId: number; maO: string; cameraId: number | null }
export interface CameraLenh { khuId: number; ten: string; nguonAnh: string; dangBat: boolean; vaiTro?: VaiTroCamera }

// ---------- Chỗ trống (khách) ----------

export interface OChoTrong { id: number; maO: string; trangThai: TrangThaiO }
export interface KhuChoTrong { id: number; ten: string; loaiXeId: number; soO: number; soTrong: number; soCoXe: number; soKhongRo: number; o: OChoTrong[] }
export interface ChoTrong { capNhatLuc: string; khu: KhuChoTrong[] }

export const TEN_TRANG_THAI_O: Record<TrangThaiO, string> = { TRONG: 'Trống', CO_XE: 'Có xe', KHONG_RO: 'Không rõ' };

export const TEN_VAI_TRO_CAMERA: Record<VaiTroCamera, string> = {
  KHU_VAO: 'Camera khu · hướng vào', KHU_RA: 'Camera khu · hướng ra', LOI_VAO_KHU: 'Lối vào khu (2 hướng, cũ)', GIAM_SAT: 'Giám sát',
};
