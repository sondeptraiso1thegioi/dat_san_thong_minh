import type { PhienDangNhap } from '../models';
import type { IHttpClient } from '@/shared/api/httpClient';

export interface IAuthService {
  dangNhap(sdt: string, matKhau: string): Promise<PhienDangNhap>;
  dangKy(hoTen: string, sdt: string, email: string, matKhau: string): Promise<unknown>;
  quenMatKhau(sdt: string): Promise<{ thongDiep: string }>;
  datLaiMatKhau(sdt: string, ma: string, matKhauMoi: string): Promise<void>;
  doiMatKhau(matKhauCu: string, matKhauMoi: string): Promise<void>;
  dangXuat(): Promise<void>;
}

export class ApiAuthService implements IAuthService {
  constructor(private readonly http: IHttpClient) {}
  dangKy(hoTen: string, sdt: string, email: string, matKhau: string) {
    return this.http.post('/api/auth/dang-ky', { hoTen, sdt, email, matKhau });
  }
  quenMatKhau(sdt: string) { return this.http.post<{ thongDiep: string }>('/api/auth/quen-mat-khau', { sdt }); }
  datLaiMatKhau(sdt: string, ma: string, matKhauMoi: string) { return this.http.post<void>('/api/auth/dat-lai-mat-khau', { sdt, ma, matKhauMoi }); }
  doiMatKhau(matKhauCu: string, matKhauMoi: string) { return this.http.put<void>('/api/auth/mat-khau', { matKhauCu, matKhauMoi }); }
  dangXuat() { return this.http.post<void>('/api/auth/dang-xuat'); }
  dangNhap(sdt: string, matKhau: string) {
    return this.http.post<PhienDangNhap>('/api/auth/dang-nhap', { sdt, matKhau });
  }
}
