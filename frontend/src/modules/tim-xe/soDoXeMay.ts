import type { KhuCho } from './models';

/**
 * Khu XE MÁY trên ảnh "Phương án bố trí bãi đỗ xe" (so-do-bai.png).
 * Hiện chỉ làm xe máy: các chữ A–Q trên ảnh là bãi ô tô, KHÔNG hiện chấm (dữ liệu khu ô tô trong DB vẫn giữ để làm sau).
 *
 * Toạ độ x, y tính theo % chiều rộng / chiều cao ảnh, nên ảnh co giãn vẫn đúng chỗ. Muốn dời chấm thì sửa ở đây.
 * Hầm B1/B2 là MỘT khu: camera đặt ở lối xuống chung, không biết xe xuống tầng nào.
 */
export type MaSoDo = 'R' | 'S' | 'HAM';

export interface KhuTrenSoDo { ma: MaSoDo; nhan: string; ten: string; x: number; y: number }

export const KHU_XE_MAY_TREN_SO_DO: readonly KhuTrenSoDo[] = [
  { ma: 'R', nhan: 'R', ten: 'R – Xe máy', x: 47.6, y: 27.2 },
  { ma: 'S', nhan: 'S', ten: 'S – Xe máy', x: 85, y: 27.8 },
  { ma: 'HAM', nhan: 'Hầm', ten: 'Hầm để xe máy B1/B2', x: 66.9, y: 62 },
];

/** Kích thước gốc của so-do-bai.png: lớp SVG vẽ đường dùng cùng tỉ lệ nên khớp ảnh ở mọi cỡ màn hình. */
export const KICH_THUOC_ANH = { rong: 683, cao: 342 } as const;

/** Cổng vào X (mũi tên vào bãi bên trái ảnh), tỉ lệ 0..1. "Bạn đang ở đây" khi vừa qua cổng. */
export const CONG_VAO_X = { x: 0.035, y: 0.41 } as const;

const LOAI_O_TO = 1;

/**
 * Khu trong DB ứng với chấm nào trên ảnh. Ưu tiên cột ma_so_do (backend trả maSoDo); khi chưa có thì suy theo tên:
 * "Khu R" / "R" → R, "Khu S" / "S" → S, tên có chữ "hầm" → HAM. Khu ô tô và khu khác (ví dụ Khu C, D mẫu) → null.
 */
export function maSoDoCua(k: KhuCho): MaSoDo | null {
  if (k.loaiXeId === LOAI_O_TO) return null;
  const ma = k.maSoDo?.trim().toUpperCase();
  if (ma === 'R' || ma === 'S' || ma === 'HAM') return ma;
  if (ma) return null;
  const ten = k.ten.trim();
  const chu = /^(?:khu\s+)?([rs])(?:\s*[-–]\s*xe\s*máy)?$/i.exec(ten)?.[1].toUpperCase();
  if (chu === 'R' || chu === 'S') return chu;
  return /hầm/i.test(ten) ? 'HAM' : null;
}

/** Chỉ các khu xe máy có trên bản đồ, theo thứ tự R, S, Hầm. Khu ô tô và khu mẫu C, D bị bỏ (dữ liệu DB vẫn giữ). */
export function khuXeMayTrenSoDo(ds: KhuCho[] | undefined): KhuCho[] {
  const m = ghepKhuXeMay(ds);
  return KHU_XE_MAY_TREN_SO_DO.map((c) => m.get(c.ma)).filter((k): k is KhuCho => !!k);
}

/** Ghép danh sách khu từ API với 3 chấm xe máy trên ảnh (mỗi chấm lấy khu đầu tiên khớp). */
export function ghepKhuXeMay(ds: KhuCho[] | undefined): Map<MaSoDo, KhuCho> {
  const m = new Map<MaSoDo, KhuCho>();
  ds?.forEach((k) => { const ma = maSoDoCua(k); if (ma && !m.has(ma)) m.set(ma, k); });
  return m;
}
