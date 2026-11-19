import { useEffect, useRef } from 'react';
import { useApp } from '@/app/AppContext';
import type { SuKienThoiGianThuc } from './thoiGianThuc';

/** Gọi fn mỗi khi server đẩy một thông báo (giữ fn mới nhất, không đăng ký lại mỗi lần render). */
export function useNgheThongBao(fn: (s: SuKienThoiGianThuc) => void) {
  const { services } = useApp();
  const ref = useRef(fn);
  ref.current = fn;
  useEffect(() => services.thoiGianThuc.nghe((s) => ref.current(s)), [services]);
}
