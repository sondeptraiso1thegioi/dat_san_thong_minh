import type { BangGia, KetQuaMuaGoi } from '../models';
import type { IHttpClient } from '@/shared/api/httpClient';

export interface IGoiChoService {
  layBangGia(): Promise<BangGia>;
  /** giaKhachThay: giá đang hiện trên màn hình. Admin vừa đổi giá thì server trả 409 GIA_DA_DOI, không trừ tiền. */
  muaGoi(xeId: number, goiChoId: number, giaKhachThay: number): Promise<KetQuaMuaGoi>;
}

export class ApiGoiChoService implements IGoiChoService {
  constructor(private readonly http: IHttpClient) {}
  layBangGia() { return this.http.get<BangGia>('/api/goi-cho'); }
  muaGoi(xeId: number, goiChoId: number, giaKhachThay: number) {
    return this.http.post<KetQuaMuaGoi>('/api/dang-ky-cho', { xeId, goiChoId, giaKhachThay });
  }
}
