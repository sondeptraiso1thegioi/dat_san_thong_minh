// Module GoiCho (Đăng ký xe và mua gói): khớp DTO của NhaXe.Modules.GoiCho.
import type { LoaiXeMa } from '@/shared/models';

export type TrangThaiDuyet = 'CHO_DUYET' | 'DA_DUYET' | 'TU_CHOI' | 'BI_GO';

export interface XeDuocDung {
  xeId: number;
  bienSo: string;
  loaiXeId: number;
  loaiXe: LoaiXeMa;
  trangThaiDuyet: TrangThaiDuyet;
  lyDoGo: string | null;
  /** Admin từ chối duyệt: lý do để chủ xe sửa và gửi lại ảnh. */
  lyDoTuChoi: string | null;
  coAnhGiayDangKy: boolean;
  chiaSeNhom: boolean;
  laChuXe: boolean;
  chuXeId: number;
  tenChuXe: string | null;
  goiHienTai: string | null;
  hetHanLuc: string | null;
  conHan: boolean;
  dangTrongBai: boolean;
  soNgayNo: number;
  soTienNo: number;
  /** Gói gần nhất bị Admin huỷ (sau đó chưa mua gói mới): app hiện hộp đỏ có lý do. */
  goiBiHuyTen: string | null;
  goiBiHuyLyDo: string | null;
  goiBiHuyLuc: string | null;
}

export interface ChoConBan {
  loaiXeId: number;
  loaiXe: LoaiXeMa;
  sucChua: number;
  soChoConBan: number;
}

export interface GoiCho {
  id: number;
  loaiXeId: number;
  ten: string;
  soNgay: number;
  gia: number;
}

export interface BangGia {
  choConBan: ChoConBan[];
  goi: GoiCho[];
}

export interface DangKyCho {
  id: number;
  tenGoi: string;
  ngayBatDau: string;
  ngayKetThuc: string;
  soTien: number;
  trangThai: string;
  muaLuc: string;
  lyDoHuy: string | null;
  gopY: string | null;
  huyLuc: string | null;
  huyBoiAdmin: boolean;
}

export interface KetQuaMuaGoi {
  dangKyChoId: number;
  ngayBatDau: string;
  ngayKetThuc: string;
  soTien: number;
  laGiaHan: boolean;
}

export interface PhiQuaHan {
  luotGuiId: number;
  hetHanLuc: string;
  soNgayConNo: number;
  soTienConNo: number;
}

export interface XeChoDuyet {
  xeId: number;
  bienSo: string;
  loaiXeId: number;
  chuXeId: number;
  tenChuXe: string;
  coAnh: boolean;
  taoLuc: string;
  /** Loại xe đang hết chỗ: duyệt xong khách chưa mua gói được (server xếp các xe này xuống cuối). */
  loaiXeHetCho: boolean;
}

export interface KetQuaHangLoat {
  thanhCong: number[];
  loi: { xeId: number; ma: string; thongDiep: string }[];
}

export interface XemHuyGoi {
  dangKyChoId: number;
  tenGoi: string;
  chuaBatDau: boolean;
  soNgayConLai: number;
  soTienHoan: number;
}

export interface XeQuanLy {
  xeId: number;
  bienSo: string;
  loaiXeId: number;
  chuXeId: number;
  tenChuXe: string;
  hetHanLuc: string | null;
  dangTrongBai: boolean;
  /** Tên gói đang dùng (gói còn hạn mới nhất); null: chưa mua / đã hết. */
  tenGoiHienTai?: string | null;
  trangThaiGoi?: TrangThaiGoiXe;
}

/** Trạng thái gói của xe trên trang Quản lý xe. SAP_HET: còn ≤ 72 giờ. CHUA_CO: hết hạn hoặc chưa từng mua. */
export type TrangThaiGoiXe = 'CON_HAN' | 'SAP_HET' | 'CHUA_CO';
/** Bộ lọc nhanh tab "Xe đã duyệt". */
export type LocXeDaDuyet = 'TAT_CA' | 'CON_HAN' | 'SAP_HET' | 'CHUA_CO' | 'TRONG_BAI';

/** GET /api/admin/xe/da-duyet?q=&loc=&trang=&soDong= */
export interface TrangXeDaDuyet { tong: number; trang: number; soDong: number; ds: XeQuanLy[] }

/** GET /api/admin/xe/{xeId}: mọi thứ cho khung chi tiết xe. */
export interface ChiTietXeAdmin {
  xe: {
    xeId: number; bienSo: string; loaiXeId: number; trangThaiDuyet: TrangThaiDuyet; coAnh: boolean; chiaSeNhom: boolean;
    taoLuc: string;
    /** null: xe duyệt trước khi có cột duyet_luc (hiện "Không rõ"). */
    duyetLuc: string | null; tenAdminDuyet: string | null;
  };
  /** Không có mật khẩu hay thông tin nhạy cảm khác. */
  chuXe: { id: number; hoTen: string; sdt: string; email: string | null; taoLuc: string | null };
  trongBai: { dangTrongBai: boolean; vaoLuc: string | null; khu: string | null };
  /** Gói gần nhất (còn hạn, hoặc đã hết nếu chưa mua tiếp). null: chưa từng mua gói. */
  goiHienTai: {
    dangKyChoId: number; tenGoi: string; soTien: number; ngayBatDau: string; ngayKetThuc: string;
    conHan: boolean; soNgayConLai: number; soNgayQuaHan: number; phiQuaHan: number;
  } | null;
  lichSuGoi: DangKyCho[];
}

export interface ChinhSachHoan {
  ma: string;
  ten: string;
}

// ---------- Sơ đồ bãi (Admin) ----------

// ---------- Admin: gói, giá, sức chứa ----------

/** Một gói nhìn từ phía Admin (cả gói đã ngừng bán). */
export interface GoiAdmin {
  id: number;
  loaiXeId: number;
  ten: string;
  soNgay: number;
  gia: number;
  dangBan: boolean;
}

/** Sức chứa theo loại xe (cùng nguồn v_cho_con_ban với lúc chặn bán). */
export interface SucChuaLoaiXe {
  loaiXeId: number;
  loaiXe: LoaiXeMa;
  sucChua: number;
  soXeGiuSuat: number;
  soXeQuaHanTrongBai: number;
  soChoConBan: number;
  dangChiem: number;
  hetCho: boolean;
}

export interface BangGiaAdmin {
  sucChua: SucChuaLoaiXe[];
  goi: GoiAdmin[];
}
