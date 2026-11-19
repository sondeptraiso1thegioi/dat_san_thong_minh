import type { ChoConBan } from '../models';
import { tenLoaiXe } from '@/shared/utils/format';

/**
 * Phương án A (bãi đầy vẫn đăng ký / duyệt xe được):
 * duyệt xe chỉ xác minh xe, không giữ chỗ; chỗ chỉ bị trừ khi mua gói.
 * Hàm thuần, dùng chung cho form thêm xe, thông báo sau khi gửi và trang duyệt của Admin.
 */
export const timChoCon = (ds: ChoConBan[] | undefined, loaiXeId: number) => ds?.find((c) => c.loaiXeId === loaiXeId);

export const hetCho = (c: ChoConBan | undefined) => !!c && c.soChoConBan <= 0;

export function canhBaoChoKhach(c: ChoConBan | undefined): string | null {
  if (!c) return null;
  const ten = tenLoaiXe(c.loaiXeId);
  return hetCho(c)
    ? `${ten} hiện đã hết chỗ (0 / ${c.sucChua}). Bạn vẫn đăng ký được để Admin duyệt xe, nhưng chỉ mua được gói khi có người trả chỗ.`
    : `${ten} còn ${c.soChoConBan} / ${c.sucChua} chỗ. Duyệt xe không giữ chỗ: chỗ chỉ được trừ khi bạn mua gói.`;
}
