import type { CameraLenh, CauHinhBai, ChoTrong, KhuLenh, OLenh, SoDoBai, TrangThaiO } from '../models';
import type { IHttpClient } from '@/shared/api/httpClient';

/** Mọi lời gọi API của module BaiXe. Trang chỉ biết interface này (DIP), không biết đường dẫn API. */
export interface IBaiXeService {
  laySoDoBai(): Promise<SoDoBai>;
  layCauHinh(): Promise<CauHinhBai>;
  themKhu(l: KhuLenh): Promise<number>;
  suaKhu(id: number, l: KhuLenh): Promise<void>;
  xoaKhu(id: number): Promise<void>;
  themO(l: OLenh): Promise<number>;
  suaO(id: number, l: OLenh): Promise<void>;
  xoaO(id: number): Promise<void>;
  datTrangThaiO(id: number, trangThai: TrangThaiO): Promise<void>;
  themCamera(l: CameraLenh): Promise<number>;
  suaCamera(id: number, l: CameraLenh): Promise<void>;
  xoaCamera(id: number): Promise<void>;
  layChoTrong(): Promise<ChoTrong>;
}

export class ApiBaiXeService implements IBaiXeService {
  constructor(private readonly http: IHttpClient) {}
  laySoDoBai() { return this.http.get<SoDoBai>('/api/admin/bai'); }
  layCauHinh() { return this.http.get<CauHinhBai>('/api/admin/bai/cau-hinh'); }
  async themKhu(l: KhuLenh) { return (await this.http.post<{ id: number }>('/api/admin/bai/khu', l)).id; }
  suaKhu(id: number, l: KhuLenh) { return this.http.put<void>(`/api/admin/bai/khu/${id}`, l); }
  xoaKhu(id: number) { return this.http.del<void>(`/api/admin/bai/khu/${id}`); }
  async themO(l: OLenh) { return (await this.http.post<{ id: number }>('/api/admin/bai/o', l)).id; }
  suaO(id: number, l: OLenh) { return this.http.put<void>(`/api/admin/bai/o/${id}`, l); }
  xoaO(id: number) { return this.http.del<void>(`/api/admin/bai/o/${id}`); }
  datTrangThaiO(id: number, trangThai: TrangThaiO) { return this.http.put<void>(`/api/admin/bai/o/${id}/trang-thai`, { trangThai }); }
  async themCamera(l: CameraLenh) { return (await this.http.post<{ id: number }>('/api/admin/bai/camera', l)).id; }
  suaCamera(id: number, l: CameraLenh) { return this.http.put<void>(`/api/admin/bai/camera/${id}`, l); }
  xoaCamera(id: number) { return this.http.del<void>(`/api/admin/bai/camera/${id}`); }
  layChoTrong() { return this.http.get<ChoTrong>('/api/bai/cho-trong'); }
}
