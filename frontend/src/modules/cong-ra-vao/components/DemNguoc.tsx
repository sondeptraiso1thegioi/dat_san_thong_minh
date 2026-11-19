import { useEffect, useState } from 'react';

/**
 * Đếm ngược từ số giây server trả về (không dùng đồng hồ máy khách so với hetHanLuc,
 * vì giờ máy khách có thể lệch giờ server). Mỗi lần server trả số mới thì đếm lại từ đó.
 */
export function DemNguoc({ soGiay, tong = 60 }: { soGiay: number; tong?: number }) {
  const [con, setCon] = useState(soGiay);
  useEffect(() => {
    setCon(soGiay);
    const batDau = Date.now();
    const id = window.setInterval(() => setCon(Math.max(0, soGiay - Math.floor((Date.now() - batDau) / 1000))), 250);
    return () => window.clearInterval(id);
  }, [soGiay]);
  const phanTram = Math.max(0, Math.min(100, (con / tong) * 100));
  return (
    <div className={`dem-nguoc ${con <= 10 ? 'dem-nguoc-gap' : ''}`}>
      <span className="dem-nguoc-so">{con}s</span>
      <div className="thanh"><div className="thanh-day" style={{ width: `${phanTram}%` }} /></div>
    </div>
  );
}

/** Nhãn trạng thái trả lời của một bên. */
export function NhanTraLoi({ ten, dongY, ai }: { ten: string; dongY: boolean | null; ai: string | null }) {
  if (dongY === null) return <span className="nhan nhan-cho">{ten}: chờ</span>;
  return <span className={`nhan ${dongY ? 'nhan-ok' : 'nhan-loi'}`}>{ten}: {dongY ? 'đồng ý' : 'từ chối'}{ai ? ` (${ai})` : ''}</span>;
}
