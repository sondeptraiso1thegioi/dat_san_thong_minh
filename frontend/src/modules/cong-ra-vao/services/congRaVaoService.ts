import type { KetQuaCong, LuotCong, ManHinhCong, RaVaoCuaToi } from '../models';
import type { IHttpClient } from '@/shared/api/httpClient';

/** Lời gọi API của module Cổng ra vào. Trang chỉ biết interface này. */
export interface ICongRaVaoService {
  // Quản lý bãi (Admin)
  layManHinhCong(): Promise<ManHinhCong>;
  xeVao(bienSo: string): Promise<KetQuaCong>;
  xeRa(bienSo: string): Promise<KetQuaCong>;
  /** Gửi ảnh camera cho AI (Model A + PaddleOCR) đọc biển; bienSo gõ kèm thì được ưu tiên. */
  /** tuDong = true: khung hình do màn hình camera tự chụp; không thấy biển thì server bỏ qua, không ghi nhật ký. */
  xeVaoBangAnh(anh: File, bienSo?: string, tuDong?: boolean): Promise<KetQuaCong>;
  xeRaBangAnh(anh: File, bienSo?: string, tuDong?: boolean): Promise<KetQuaCong>;
  /** Trạng thái lượt gửi (cổng ra chờ người dùng xác nhận trên app). */
  layLuot(luotGuiId: number): Promise<LuotCong>;
  /** Ảnh đối chứng chụp ở cổng vào / cổng ra. */
  layAnhDoiChung(luotGuiId: number, loai: 'vao' | 'ra'): Promise<Blob>;
  /** Mở barrier thủ công (UC-A10): hết 60 giây, nghi mất đã kiểm tra, app / AI lỗi. Bắt buộc lý do. */
  moTay(luotGuiId: number, lyDo: string): Promise<KetQuaCong>;
  // Khách
  layRaVaoCuaToi(): Promise<RaVaoCuaToi>;
  nguoiDungXeTraLoi(yeuCauId: number, dongY: boolean): Promise<void>;
}

export class ApiCongRaVaoService implements ICongRaVaoService {
  constructor(private readonly http: IHttpClient) {}
  layManHinhCong() { return this.http.get<ManHinhCong>('/api/admin/cong'); }
  xeVao(bienSo: string) { return this.http.post<KetQuaCong>('/api/admin/cong/vao', { bienSo }); }
  xeRa(bienSo: string) { return this.http.post<KetQuaCong>('/api/admin/cong/ra', { bienSo }); }
  xeVaoBangAnh(anh: File, bienSo?: string, tuDong?: boolean) {
    return this.http.postForm<KetQuaCong>('/api/admin/cong/vao/anh', formAnh(anh, bienSo, tuDong));
  }
  xeRaBangAnh(anh: File, bienSo?: string, tuDong?: boolean) {
    return this.http.postForm<KetQuaCong>('/api/admin/cong/ra/anh', formAnh(anh, bienSo, tuDong));
  }
  layLuot(id: number) { return this.http.get<LuotCong>(`/api/admin/cong/luot/${id}`); }
  layAnhDoiChung(id: number, loai: 'vao' | 'ra') { return this.http.getBlob(`/api/admin/cong/luot/${id}/anh/${loai}`); }
  moTay(luotGuiId: number, lyDo: string) { return this.http.post<KetQuaCong>('/api/admin/cong/mo-tay', { luotGuiId, lyDo }); }
  layRaVaoCuaToi() { return this.http.get<RaVaoCuaToi>('/api/ra-vao'); }
  nguoiDungXeTraLoi(id: number, dongY: boolean) { return this.http.put<void>(`/api/ra-vao/yeu-cau/${id}`, { dongY }); }
}

function formAnh(anh: File, bienSo?: string, tuDong?: boolean) {
  const f = new FormData();
  f.append('anh', anh);
  if (bienSo?.trim()) f.append('bienSo', bienSo.trim());
  if (tuDong) f.append('tuDong', 'true');
  return f;
}
