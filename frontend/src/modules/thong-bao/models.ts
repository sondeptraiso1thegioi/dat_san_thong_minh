// Module ThongBao: hộp thư trong app.

export type LoaiThongBao =
  | 'VAO' | 'RA' | 'SAP_HET_HAN' | 'HET_HAN' | 'QUA_HAN' | 'NGHI_MAT' | 'HUY_GOI' | 'XE_BI_GO' | 'KHACH_HUY'
  | 'KHU' | 'NHOM' | 'DOI_SOAT' | 'CHO_THANH_TOAN' | 'XE_CHO_DUYET' | 'KET_QUA_DUYET';

export interface ThongBaoApp {
  id: number;
  loai: LoaiThongBao | string;
  noiDung: string;
  guiLuc: string;
  daDoc: boolean;
  /** Việc cần làm / chỗ cần xem (xem dichDen.ts). null: thông báo cũ, frontend suy theo loai. */
  hanhDong?: string | null;
  /** Id đối tượng liên quan: luot_gui_id, xe_id... tuỳ hanhDong. */
  thamChieuId?: number | null;
}

export interface HopThu {
  soChuaDoc: number;
  thongBao: ThongBaoApp[];
}

/** Tên hiển thị và màu nhãn của từng loại. */
export const NHAN_LOAI: Record<string, { ten: string; mau: 'ok' | 'cho' | 'loi' | 'tin' }> = {
  VAO: { ten: 'Xe vào', mau: 'ok' },
  RA: { ten: 'Cổng ra', mau: 'tin' },
  SAP_HET_HAN: { ten: 'Sắp hết hạn', mau: 'cho' },
  HET_HAN: { ten: 'Hết hạn', mau: 'loi' },
  QUA_HAN: { ten: 'Quá hạn', mau: 'loi' },
  NGHI_MAT: { ten: 'Nghi mất xe', mau: 'loi' },
  HUY_GOI: { ten: 'Huỷ gói', mau: 'cho' },
  XE_BI_GO: { ten: 'Xe bị gỡ', mau: 'loi' },
  KHACH_HUY: { ten: 'Khách huỷ gói', mau: 'cho' },
  KHU: { ten: 'Khu xe máy', mau: 'tin' },
  NHOM: { ten: 'Nhóm gia đình', mau: 'tin' },
  DOI_SOAT: { ten: 'Đối soát khu', mau: 'cho' },
  CHO_THANH_TOAN: { ten: 'Chờ thanh toán', mau: 'cho' },
  XE_CHO_DUYET: { ten: 'Xe chờ duyệt', mau: 'cho' },
  KET_QUA_DUYET: { ten: 'Kết quả duyệt', mau: 'tin' },
};
