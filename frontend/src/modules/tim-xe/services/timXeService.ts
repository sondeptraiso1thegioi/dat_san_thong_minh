import type { IHttpClient } from '@/shared/api/httpClient';
import type { ChiDan, DiSaiKhuAdmin, KetQuaCameraKhu, SoXeKhu, TimXeCuaToi } from '../models';

/** Lời gọi API của module TimXe. Trang chỉ biết interface này. */
export interface ITimXeService {
  // Khách
  layTimXe(): Promise<TimXeCuaToi>;
  daDoXe(xeId: number, khuId: number): Promise<string>;
  // Admin và khách: sơ đồ chỗ trống
  laySoXeKhu(): Promise<SoXeKhu>;
  // Admin
  /** "Mời vào khu X" cho màn hình cổng sau khi xe vào. null: ô tô hoặc mọi khu đã đầy. */
  layChiDan(luotGuiId: number): Promise<ChiDan | null>;
  cameraKhuBienSo(cameraId: number, bienSo: string): Promise<KetQuaCameraKhu>;
  /** tuDong: màn hình tự quét mỗi giây (khung không có biển thì backend trả KHONG_THAY_BIEN). */
  cameraKhuAnh(cameraId: number, anh: File, bienSo?: string, tuDong?: boolean): Promise<KetQuaCameraKhu>;
  /** khuId = null: gỡ xe khỏi khu. */
  ganKhu(luotGuiId: number, khuId: number | null): Promise<void>;
  /** Cảnh báo đi sai khu gần đây và ai hay đi sai (trang Quản lý bãi). */
  layDiSaiKhu(): Promise<DiSaiKhuAdmin>;
}

export class ApiTimXeService implements ITimXeService {
  constructor(private readonly http: IHttpClient) {}
  layTimXe() { return this.http.get<TimXeCuaToi>('/api/tim-xe'); }
  async daDoXe(xeId: number, khuId: number) {
    return (await this.http.post<{ thongDiep: string }>('/api/tim-xe/da-do-xe', { xeId, khuId })).thongDiep;
  }
  laySoXeKhu() { return this.http.get<SoXeKhu>('/api/so-xe-khu'); }
  async layChiDan(luotGuiId: number) { return (await this.http.get<ChiDan | undefined>(`/api/admin/tim-xe/chi-dan/${luotGuiId}`)) ?? null; }
  cameraKhuBienSo(cameraId: number, bienSo: string) { return this.http.post<KetQuaCameraKhu>(`/api/admin/tim-xe/camera/${cameraId}`, { bienSo }); }
  cameraKhuAnh(cameraId: number, anh: File, bienSo?: string, tuDong?: boolean) {
    const f = new FormData();
    f.append('anh', anh);
    if (bienSo?.trim()) f.append('bienSo', bienSo.trim());
    if (tuDong) f.append('tuDong', 'true');
    return this.http.postForm<KetQuaCameraKhu>(`/api/admin/tim-xe/camera/${cameraId}/anh`, f);
  }
  ganKhu(luotGuiId: number, khuId: number | null) { return this.http.put<void>('/api/admin/tim-xe/gan-khu', { luotGuiId, khuId }); }
  layDiSaiKhu() { return this.http.get<DiSaiKhuAdmin>('/api/admin/tim-xe/di-sai-khu'); }
}
