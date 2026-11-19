import { useEffect, useState } from 'react';

/**
 * Đếm ngược suất giữ chỗ. Lấy số giây còn lại theo GIỜ SERVER (hetHanLuc − bayGio của cùng một lần tải),
 * rồi đếm bằng đồng hồ máy khách, để máy khách lệch giờ cũng không sai.
 */
export function DemNguocGiuCho({ hetHanLuc, bayGio }: { hetHanLuc: string; bayGio: string }) {
  const goc = Math.max(0, Math.round((new Date(hetHanLuc).getTime() - new Date(bayGio).getTime()) / 1000));
  const [con, setCon] = useState(goc);
  useEffect(() => {
    const batDau = Date.now();
    setCon(goc);
    const id = window.setInterval(() => setCon(Math.max(0, goc - Math.floor((Date.now() - batDau) / 1000))), 500);
    return () => window.clearInterval(id);
  }, [goc]);
  const p = Math.floor(con / 60), g = con % 60;
  return <span className={`dem-giu ${con <= 60 ? 'dem-giu-gap' : ''}`}>{con > 0 ? `${p}:${String(g).padStart(2, '0')}` : 'hết giờ giữ'}</span>;
}
