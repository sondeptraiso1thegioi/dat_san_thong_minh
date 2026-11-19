import type { IHttpClient } from '@/shared/api/httpClient';
import type { DieuHuongCuaToi, DuongDiBanDo, PhienDieuHuong } from '../models';

/** Điều hướng trong bãi (người dùng). Trang và component chỉ biết interface này. */
export interface IDieuHuongService {
  /** Phiên điều hướng gần nhất của các xe tài khoản được dùng. */
  layCuaToi(): Promise<DieuHuongCuaToi>;
  /** Đổi khu đích khi đang dẫn đường: server kiểm tra khu còn chỗ, chuyển suất giữ sang khu mới, trả đường mới. */
  doiKhuDich(khuId: number): Promise<PhienDieuHuong>;
  /** Chỉ đọc: đường từ Cổng vào X tới khu (không tạo phiên, không giữ suất). Dùng chung đồ thị + Dijkstra ở server. */
  layDuongDi(denKhuId: number): Promise<DuongDiBanDo>;
}

export class ApiDieuHuongService implements IDieuHuongService {
  constructor(private readonly http: IHttpClient) {}
  layCuaToi() { return this.http.get<DieuHuongCuaToi>('/api/dieu-huong/cua-toi'); }
  doiKhuDich(khuId: number) { return this.http.put<PhienDieuHuong>('/api/dieu-huong/cua-toi/khu-dich', { khuId }); }
  layDuongDi(denKhuId: number) { return this.http.get<DuongDiBanDo>(`/api/dieu-huong/duong-di?tuKhu=CONG_X&denKhu=${denKhuId}`); }
}
