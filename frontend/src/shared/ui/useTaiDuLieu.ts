import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * Hook dùng chung: gọi một hàm async, giữ trạng thái đang tải / lỗi / dữ liệu, cho phép tải lại.
 * chuKyMs: tự tải lại NGẦM theo chu kỳ (không hiện "Đang tải…"), dùng cho màn hình cổng và trang chờ xác nhận.
 */
export function useTaiDuLieu<T>(ham: () => Promise<T>, phuThuoc: unknown[] = [], chuKyMs?: number) {
  const [duLieu, setDuLieu] = useState<T | null>(null);
  const [loi, setLoi] = useState<string | null>(null);
  const [dangTai, setDangTai] = useState(true);

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const tai = useCallback(ham, phuThuoc);

  // Chỉ nhận kết quả của lần gọi MỚI NHẤT: gõ tìm nhanh thì phản hồi cũ về sau không đè lên kết quả mới
  const lanGoi = useRef(0);
  const taiLai = useCallback(async () => {
    const lan = ++lanGoi.current;
    setDangTai(true);
    setLoi(null);
    try { const d = await tai(); if (lan === lanGoi.current) setDuLieu(d); }
    catch (e) { if (lan === lanGoi.current) setLoi((e as Error).message); }
    finally { if (lan === lanGoi.current) setDangTai(false); }
  }, [tai]);

  useEffect(() => { void taiLai(); }, [taiLai]);

  useEffect(() => {
    if (!chuKyMs) return;
    const id = window.setInterval(() => {
      if (document.hidden) return; // tab đang ẩn thì thôi, đỡ tốn request
      const lan = lanGoi.current;
      tai().then((d) => { if (lan === lanGoi.current) { setDuLieu(d); setLoi(null); } }).catch(() => { /* lỗi mạng thoáng qua: giữ dữ liệu cũ */ });
    }, chuKyMs);
    return () => window.clearInterval(id);
  }, [tai, chuKyMs]);
  return { duLieu, loi, dangTai, taiLai };
}
