import type { PhienDangNhap } from '../models';
import type { ITokenStore } from '@/shared/api/httpClient';

const KHOA = 'nhaxe.phien';

/** Lưu phiên đăng nhập trên trình duyệt và báo cho giao diện khi phiên bị huỷ. */
export class PhienStore implements ITokenStore {
  private nghe: ((p: PhienDangNhap | null, thongDiep?: string) => void) | null = null;

  doc(): PhienDangNhap | null {
    try { return JSON.parse(localStorage.getItem(KHOA) ?? 'null'); } catch { return null; }
  }
  luu(p: PhienDangNhap | null, thongDiep?: string) {
    try { p ? localStorage.setItem(KHOA, JSON.stringify(p)) : localStorage.removeItem(KHOA); } catch { /* trình duyệt chặn lưu */ }
    this.nghe?.(p, thongDiep);
  }
  dangKyNghe(fn: (p: PhienDangNhap | null, thongDiep?: string) => void) { this.nghe = fn; }

  layToken() { return this.doc()?.token ?? null; }
  khiHetPhien(thongDiep: string) { this.luu(null, thongDiep); }
}
