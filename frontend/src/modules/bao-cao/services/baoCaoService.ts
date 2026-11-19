import type { IHttpClient } from '@/shared/api/httpClient';
import type { BaoCao } from '../models';

export interface IBaoCaoService {
  /** Không truyền khoảng: từ 00:00 hôm nay tới hiện tại. */
  lay(tu?: string, den?: string): Promise<BaoCao>;
}

export class ApiBaoCaoService implements IBaoCaoService {
  constructor(private readonly http: IHttpClient) {}
  lay(tu?: string, den?: string) {
    const q = new URLSearchParams();
    if (tu) q.set('tu', tu);
    if (den) q.set('den', den);
    const s = q.toString();
    return this.http.get<BaoCao>(`/api/admin/bao-cao${s ? `?${s}` : ''}`);
  }
}
