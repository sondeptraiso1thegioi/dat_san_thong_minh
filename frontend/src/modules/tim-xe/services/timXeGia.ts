import type { KhuCho, MucCho, SoXeKhu } from '../models';
import type { ITimXeService } from './timXeService';

/**
 * Dữ liệu GIẢ để duyệt giao diện khi backend chưa có khu R, S, Hầm. Chỉ bật khi chạy dev với VITE_DU_LIEU_GIA=1
 * (xem container.ts). Các hàm khác chuyển thẳng cho service thật.
 * Có cả khu ô tô A, B và khu xe máy mẫu C, D để thấy chúng KHÔNG hiện trên sơ đồ.
 */
export class GiaTimXeService implements ITimXeService {
  private readonly batDau = Date.now();
  constructor(private readonly that: ITimXeService) {}

  layTimXe() { return this.that.layTimXe(); }
  daDoXe(xeId: number, khuId: number) { return this.that.daDoXe(xeId, khuId); }
  layChiDan(luotGuiId: number) { return this.that.layChiDan(luotGuiId); }
  cameraKhuBienSo(cameraId: number, bienSo: string) { return this.that.cameraKhuBienSo(cameraId, bienSo); }
  cameraKhuAnh(cameraId: number, anh: File, bienSo?: string, tuDong?: boolean) { return this.that.cameraKhuAnh(cameraId, anh, bienSo, tuDong); }
  ganKhu(luotGuiId: number, khuId: number | null) { return this.that.ganKhu(luotGuiId, khuId); }
  layDiSaiKhu() { return this.that.layDiSaiKhu(); }

  async laySoXeKhu(): Promise<SoXeKhu> {
    // Cứ 30 giây khu R bớt 1 chỗ (23 → 0 rồi lặp lại), để thấy số trên chấm tự đổi và đổi màu
    const r = 23 - (Math.floor((Date.now() - this.batDau) / 30_000) % 24);
    return {
      bayGio: new Date().toISOString(),
      khu: [
        khu(1, 'Khu A', 1, 30, 10), khu(2, 'Khu B', 1, 30, 30),
        khu(3, 'Khu C', 2, 12, 5), khu(4, 'Khu D', 2, 8, 2),
        khu(10, 'Khu R', 2, 60, r, 'R'),
        khu(11, 'Khu S', 2, 40, 0, 'S'),
        khu(12, 'Hầm xe máy B1/B2', 2, 80, 37, 'HAM'),
      ],
    };
  }
}

function khu(id: number, ten: string, loaiXeId: number, sucChua: number, conLai: number, maSoDo: string | null = null): KhuCho {
  const muc: MucCho = conLai <= 0 ? 'DAY' : conLai * 100 > sucChua * 20 ? 'CON_NHIEU' : 'SAP_DAY';
  return { id, ten, loaiXeId, sucChua, soXe: sucChua - conLai, soGiu: 0, conLai, muc, khoangCachTuCong: null, maSoDo };
}
