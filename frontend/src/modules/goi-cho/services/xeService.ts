import type { DangKyCho, PhiQuaHan, XeDuocDung, XemHuyGoi } from '../models';
import type { IHttpClient } from '@/shared/api/httpClient';

export interface IXeService {
  layDanhSach(): Promise<XeDuocDung[]>;
  them(loaiXeId: number, bienSo: string, chiaSeNhom: boolean): Promise<{ xeId: number }>;
  layLichSuGoi(xeId: number): Promise<DangKyCho[]>;
  layPhiQuaHan(xeId: number): Promise<PhiQuaHan | null>;
  traPhiQuaHan(xeId: number): Promise<PhiQuaHan>;
  xemHuyGoi(dangKyChoId: number): Promise<XemHuyGoi>;
  huyGoi(dangKyChoId: number, lyDo?: string, gopY?: string): Promise<XemHuyGoi>;
  layLyDoHuy(): Promise<string[]>;
  taiAnhGiayDangKy(xeId: number, anh: File): Promise<void>;
  layAnhGiayDangKy(xeId: number): Promise<Blob>;
}

export class ApiXeService implements IXeService {
  constructor(private readonly http: IHttpClient) {}
  layDanhSach() { return this.http.get<XeDuocDung[]>('/api/xe'); }
  them(loaiXeId: number, bienSo: string, chiaSeNhom: boolean) {
    return this.http.post<{ xeId: number }>('/api/xe', { loaiXeId, bienSo, chiaSeNhom });
  }
  layLichSuGoi(xeId: number) { return this.http.get<DangKyCho[]>(`/api/xe/${xeId}/dang-ky`); }
  async layPhiQuaHan(xeId: number) { return (await this.http.get<PhiQuaHan | undefined>(`/api/xe/${xeId}/phi-qua-han`)) ?? null; }
  traPhiQuaHan(xeId: number) { return this.http.post<PhiQuaHan>(`/api/xe/${xeId}/phi-qua-han/thanh-toan`); }
  xemHuyGoi(dangKyChoId: number) { return this.http.get<XemHuyGoi>(`/api/dang-ky-cho/${dangKyChoId}/huy`); }
  huyGoi(dangKyChoId: number, lyDo?: string, gopY?: string) {
    return this.http.post<XemHuyGoi>(`/api/dang-ky-cho/${dangKyChoId}/huy`, { lyDo: lyDo || null, gopY: gopY || null });
  }
  layLyDoHuy() { return this.http.get<string[]>('/api/dang-ky-cho/ly-do-huy'); }
  taiAnhGiayDangKy(xeId: number, anh: File) {
    const f = new FormData(); f.append('anh', anh);
    return this.http.putForm<void>(`/api/xe/${xeId}/giay-dang-ky`, f);
  }
  layAnhGiayDangKy(xeId: number) { return this.http.getBlob(`/api/xe/${xeId}/giay-dang-ky`); }
}
