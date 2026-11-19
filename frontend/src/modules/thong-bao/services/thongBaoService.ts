import type { HopThu } from '../models';
import type { IHttpClient } from '@/shared/api/httpClient';

export interface IThongBaoService {
  /** Hộp thư Admin (khách huỷ gói, nghi mất xe, cổng ra không mở). */
  layHopThuAdmin(): Promise<HopThu>;
  /** Hộp thư của chính người đang đăng nhập (mọi loại). */
  layHopThu(): Promise<HopThu>;
  soChuaDoc(): Promise<number>;
  danhDauDaDoc(id: number): Promise<void>;
  docHet(): Promise<number>;
}

export class ApiThongBaoService implements IThongBaoService {
  constructor(private readonly http: IHttpClient) {}
  layHopThuAdmin() { return this.http.get<HopThu>('/api/admin/thong-bao'); }
  layHopThu() { return this.http.get<HopThu>('/api/thong-bao'); }
  async soChuaDoc() { return (await this.http.get<{ soChuaDoc: number }>('/api/thong-bao/so-chua-doc')).soChuaDoc; }
  danhDauDaDoc(id: number) { return this.http.put<void>(`/api/thong-bao/${id}/da-doc`); }
  async docHet() { return (await this.http.put<{ soDaDanhDau: number }>('/api/thong-bao/da-doc-tat-ca')).soDaDanhDau; }
}
