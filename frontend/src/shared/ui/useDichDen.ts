import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useApp } from '@/app/AppContext';

// Trang đích của thông báo: đọc id trên URL, tô sáng đúng dòng / thẻ, hoặc báo "Không mở được nội dung này".

export const LOI_KHONG_MO_DUOC = 'khong-mo-duoc';

/**
 * Đọc các tham số trên URL MỘT lần (giữ lại trong state) rồi xoá khỏi URL bằng replace,
 * để tải lại trang không tô sáng lại. Bấm thông báo khác khi đang ở cùng trang: URL đổi → đọc giá trị mới.
 */
export function useThamSoMotLan<K extends string>(...ten: K[]): [Partial<Record<K, string>>, () => void] {
  const [params, setParams] = useSearchParams();
  const doc = useCallback(() => {
    const kq: Partial<Record<K, string>> = {};
    for (const t of ten) { const v = params.get(t); if (v !== null) kq[t] = v; }
    return kq;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [params, ten.join('|')]);
  const [giaTri, setGiaTri] = useState<Partial<Record<K, string>>>({});

  useEffect(() => {
    const moi = doc();
    if (Object.keys(moi).length === 0) return;
    setGiaTri(moi);
    const con = new URLSearchParams(params);
    for (const t of ten) con.delete(t);
    setParams(con, { replace: true });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [doc]);

  return [giaTri, useCallback(() => setGiaTri({}), [])];
}

/**
 * Chạy fn đúng MỘT lần cho mỗi lần đọc tham số mới (bấm lại cùng thông báo cũng chạy lại), khi dữ liệu trang đã sẵn sàng.
 * Không có tham số thì không chạy.
 */
export function useKhiMoTuThongBao<T extends object>(thamSo: T, sanSang: boolean, fn: (t: T) => void) {
  const daChay = useRef<T | null>(null);
  const f = useRef(fn);
  f.current = fn;
  useEffect(() => {
    if (!sanSang || daChay.current === thamSo || Object.keys(thamSo).length === 0) return;
    daChay.current = thamSo;
    f.current(thamSo);
  }, [thamSo, sanSang]);
}

/** Số nguyên dương từ tham số URL, sai thì null. */
export function soId(v: string | undefined | null): number | null {
  const n = Number(v);
  return v && Number.isInteger(n) && n > 0 ? n : null;
}

/** Cuộn tới phần tử [data-dich="khoa"] và tô sáng 2,5 giây. Trả về false nếu không thấy phần tử. */
export function toSang(khoa: string): boolean {
  const el = document.querySelector<HTMLElement>(`[data-dich="${CSS.escape(khoa)}"]`);
  if (!el) return false;
  el.scrollIntoView({ behavior: 'smooth', block: 'center' });
  el.classList.remove('to-sang');
  void el.offsetWidth; // chạy lại hiệu ứng nếu bấm 2 lần liên tiếp
  el.classList.add('to-sang');
  window.setTimeout(() => el.classList.remove('to-sang'), 2_600);
  return true;
}

/** Không có quyền / không còn đối tượng: về trang Thông báo, báo "Không mở được nội dung này". */
export function useVeKhongMoDuoc() {
  const nav = useNavigate();
  const { phien } = useApp();
  const trang = phien?.vaiTro === 'ADMIN' ? '/admin/thong-bao' : '/thong-bao';
  return useCallback(() => nav(`${trang}?loi=${LOI_KHONG_MO_DUOC}`, { replace: true }), [nav, trang]);
}
