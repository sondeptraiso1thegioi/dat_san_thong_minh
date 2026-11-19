import type { IHttpClient } from '@/shared/api/httpClient';
import type { TrangThaiNhom } from '../models';
export interface INhomGiaDinhService {
  xem(): Promise<TrangThaiNhom>;
  tao(ten: string): Promise<unknown>;
  xinThamGia(maMoi: string): Promise<unknown>;
  xuLy(id: number, dongY: boolean): Promise<unknown>;
  doiMa(): Promise<unknown>;
  roi(): Promise<void>;
  xoaThanhVien(id: number): Promise<void>;
  giaiTan(): Promise<void>;
}
export class ApiNhomGiaDinhService implements INhomGiaDinhService {
  constructor(private readonly http: IHttpClient) {}
  xem() { return this.http.get<TrangThaiNhom>('/api/nhom-gia-dinh'); }
  tao(ten: string) { return this.http.post('/api/nhom-gia-dinh', { ten }); }
  xinThamGia(maMoi: string) { return this.http.post('/api/nhom-gia-dinh/yeu-cau', { maMoi }); }
  xuLy(id: number, dongY: boolean) { return this.http.put(`/api/nhom-gia-dinh/yeu-cau/${id}`, { dongY }); }
  doiMa() { return this.http.post('/api/nhom-gia-dinh/ma-moi'); }
  roi() { return this.http.post<void>('/api/nhom-gia-dinh/roi-nhom'); }
  xoaThanhVien(id: number) { return this.http.post<void>(`/api/nhom-gia-dinh/thanh-vien/${id}/xoa`); }
  giaiTan() { return this.http.post<void>('/api/nhom-gia-dinh/giai-tan'); }
}
