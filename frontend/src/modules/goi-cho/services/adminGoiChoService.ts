import type {
  BangGiaAdmin, ChiTietXeAdmin, ChinhSachHoan, DangKyCho, GoiAdmin, KetQuaHangLoat, LocXeDaDuyet, SucChuaLoaiXe, TrangXeDaDuyet,
  XeChoDuyet, XemHuyGoi,
} from '../models';
import type { IHttpClient } from '@/shared/api/httpClient';

/** Các việc Admin làm trong module GoiCho: duyệt / gỡ xe, huỷ gói có lý do, đổi giá gói, đổi sức chứa. */
export interface IAdminGoiChoService {
  layXeChoDuyet(): Promise<XeChoDuyet[]>;
  duyetXe(xeId: number, dongY: boolean, lyDo?: string): Promise<void>;
  duyetHangLoat(xeIds: number[], dongY: boolean, lyDo?: string): Promise<KetQuaHangLoat>;
  /** Số xe đang chờ duyệt (badge trên menu "Quản lý xe"). */
  demXeChoDuyet(): Promise<number>;
  /** q: biển số (server chuẩn hoá: bỏ chấm, gạch, khoảng trắng, không phân biệt hoa thường) rồi tìm "chứa". */
  layXeDaDuyet(q: string, loc: LocXeDaDuyet, trang: number, soDong: number): Promise<TrangXeDaDuyet>;
  layChiTietXe(xeId: number): Promise<ChiTietXeAdmin>;
  goXe(xeId: number, lyDo: string): Promise<void>;
  // huỷ gói có lý do
  layLichSuGoi(xeId: number): Promise<DangKyCho[]>;
  layChinhSachHoan(): Promise<ChinhSachHoan[]>;
  xemHuyGoi(dangKyChoId: number, chinhSach: string): Promise<XemHuyGoi>;
  huyGoi(dangKyChoId: number, lyDo: string, chinhSach: string): Promise<XemHuyGoi>;
  // gói, giá, sức chứa
  layBangGia(): Promise<BangGiaAdmin>;
  doiGia(goiId: number, gia: number): Promise<GoiAdmin>;
  doiSucChua(loaiXeId: number, sucChua: number): Promise<SucChuaLoaiXe>;
}

export class ApiAdminGoiChoService implements IAdminGoiChoService {
  constructor(private readonly http: IHttpClient) {}
  layXeChoDuyet() { return this.http.get<XeChoDuyet[]>('/api/admin/xe/cho-duyet'); }
  duyetXe(xeId: number, dongY: boolean, lyDo?: string) { return this.http.put<void>(`/api/admin/xe/${xeId}/duyet`, { dongY, lyDo: lyDo ?? null }); }
  duyetHangLoat(xeIds: number[], dongY: boolean, lyDo?: string) {
    return this.http.post<KetQuaHangLoat>('/api/admin/xe/duyet-hang-loat', { xeIds, dongY, lyDo: lyDo ?? null });
  }
  async demXeChoDuyet() { return (await this.http.get<{ soLuong: number }>('/api/admin/xe/cho-duyet/so-luong')).soLuong; }
  layXeDaDuyet(q: string, loc: LocXeDaDuyet, trang: number, soDong: number) {
    const p = new URLSearchParams({ q, loc, trang: String(trang), soDong: String(soDong) });
    return this.http.get<TrangXeDaDuyet>(`/api/admin/xe/da-duyet?${p}`);
  }
  layChiTietXe(xeId: number) { return this.http.get<ChiTietXeAdmin>(`/api/admin/xe/${xeId}`); }
  goXe(xeId: number, lyDo: string) { return this.http.put<void>(`/api/admin/xe/${xeId}/go`, { lyDo }); }

  layLichSuGoi(xeId: number) { return this.http.get<DangKyCho[]>(`/api/admin/xe/${xeId}/dang-ky`); }
  layChinhSachHoan() { return this.http.get<ChinhSachHoan[]>('/api/admin/chinh-sach-hoan'); }
  xemHuyGoi(id: number, chinhSach: string) {
    return this.http.get<XemHuyGoi>(`/api/admin/dang-ky-cho/${id}/huy?chinhSach=${encodeURIComponent(chinhSach)}`);
  }
  huyGoi(id: number, lyDo: string, chinhSach: string) {
    return this.http.post<XemHuyGoi>(`/api/admin/dang-ky-cho/${id}/huy`, { lyDo, chinhSach });
  }

  layBangGia() { return this.http.get<BangGiaAdmin>('/api/admin/goi-cho'); }
  doiGia(goiId: number, gia: number) { return this.http.put<GoiAdmin>(`/api/admin/goi-cho/${goiId}/gia`, { gia }); }
  doiSucChua(loaiXeId: number, sucChua: number) { return this.http.put<SucChuaLoaiXe>(`/api/admin/loai-xe/${loaiXeId}/suc-chua`, { sucChua }); }
}
