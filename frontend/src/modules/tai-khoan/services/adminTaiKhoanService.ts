import type { IHttpClient } from '@/shared/api/httpClient';

export interface TaiKhoanAdmin {
  id: number; hoTen: string; sdt: string; email: string | null; vaiTro: 'ADMIN' | 'USER';
  trangThai: 'HOAT_DONG' | 'KHOA' | string; dangNhapLuc: string | null; dangTamKhoaDangNhap: boolean; taoLuc: string;
}

/** Admin xem / khoá / mở khoá tài khoản (module TaiKhoan). */
export interface IAdminTaiKhoanService {
  layDanhSach(tuKhoa?: string, trangThai?: string): Promise<TaiKhoanAdmin[]>;
  khoa(id: number): Promise<void>;
  moKhoa(id: number): Promise<void>;
}

export class ApiAdminTaiKhoanService implements IAdminTaiKhoanService {
  constructor(private readonly http: IHttpClient) {}
  layDanhSach(tuKhoa?: string, trangThai?: string) {
    const q = new URLSearchParams();
    if (tuKhoa?.trim()) q.set('tuKhoa', tuKhoa.trim());
    if (trangThai) q.set('trangThai', trangThai);
    const s = q.toString();
    return this.http.get<TaiKhoanAdmin[]>(`/api/admin/tai-khoan${s ? `?${s}` : ''}`);
  }
  khoa(id: number) { return this.http.put<void>(`/api/admin/tai-khoan/${id}/khoa`); }
  moKhoa(id: number) { return this.http.put<void>(`/api/admin/tai-khoan/${id}/mo-khoa`); }
}
