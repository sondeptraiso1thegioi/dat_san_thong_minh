// Module TaiKhoan: phiên đăng nhập.
import type { VaiTro } from '@/shared/models';

export interface PhienDangNhap {
  token: string;
  id: number;
  hoTen: string;
  vaiTro: VaiTro;
}
