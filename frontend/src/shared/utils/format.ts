import type { LoaiXeMa } from '../models';

const tien = new Intl.NumberFormat('vi-VN');
export const dinhDangTien = (so: number) => `${tien.format(so)} đ`;

export const dinhDangNgay = (iso: string | null) =>
  iso ? new Date(iso).toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '—';

export const tenLoaiXe = (l: LoaiXeMa | number) => (l === 'O_TO' || l === 1 ? 'Ô tô' : 'Xe máy');

/** 30A12345 -> 30A-123.45 ; 29B112345 -> 29B1-123.45 */
export function hienThiBienSo(bs: string): string {
  const m = /^(\d{2}[A-Z]{1,2})(\d+)$/.exec(bs);
  if (!m) return bs;
  const so = m[2];
  if (so.length >= 5) {
    const dau = m[1] + so.slice(0, so.length - 5);
    const cuoi = so.slice(-5);
    return `${dau}-${cuoi.slice(0, 3)}.${cuoi.slice(3)}`;
  }
  return `${m[1]}-${so}`;
}

/** Thời gian còn lại tới một mốc, dạng ngắn: "12 ngày 3 giờ", "5 giờ", "quá 2 ngày". */
export function conLai(iso: string | null, bayGio = Date.now()): string {
  if (!iso) return 'Không có gói';
  const ms = new Date(iso).getTime() - bayGio;
  const qua = ms <= 0;
  const gio = Math.floor(Math.abs(ms) / 3_600_000);
  const ngay = Math.floor(gio / 24);
  const chu = ngay > 0 ? `${ngay} ngày${gio % 24 ? ` ${gio % 24} giờ` : ''}` : gio > 0 ? `${gio} giờ` : 'dưới 1 giờ';
  return qua ? `Quá hạn ${chu}` : `Còn ${chu}`;
}

/**
 * Chuẩn hoá chuỗi tìm biển số giống cách backend lưu (BienSo trong Domain): viết hoa, bỏ dấu chấm / gạch / khoảng trắng.
 * "29a-1", "29A.12" → "29A1", "29A12". Dùng để tìm "chứa".
 */
export const chuanHoaBienSo = (s: string) => s.toUpperCase().replace(/[^0-9A-Z]/g, '');
