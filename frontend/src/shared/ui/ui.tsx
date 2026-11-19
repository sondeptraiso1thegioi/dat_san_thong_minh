import type { ReactNode } from 'react';

// Component trình bày thuần dùng chung cho mọi module: chỉ nhận props và vẽ, không gọi API (SRP).

export function ThongBao({ loai, children }: { loai: 'loi' | 'ok' | 'canh-bao' | 'tin'; children: ReactNode }) {
  return <div className={`thong-bao thong-bao-${loai}`} role={loai === 'loi' ? 'alert' : 'status'}>{children}</div>;
}

export function DangTai() {
  return <p className="mo">Đang tải…</p>;
}
