import type { ReactNode } from 'react';

/**
 * Khung chung của Đăng nhập / Đăng ký: vạch sơn ô đỗ phía trên (màu viền navy của logo), ô giữa tô hồng cam,
 * hai ô bên tô nhạt bạc hà / vàng theo logo; logo nằm giữa ô; dưới là nhãn nhỏ, tiêu đề, mô tả rồi tới form.
 * Màu nền chuyển dần phải trùng --nen trong styles.css (#fbf6ef).
 */
export function KhungXacThuc({ nhan, tieuDe, moTa, children, chan }: { nhan: string; tieuDe: string; moTa: string; children: ReactNode; chan?: ReactNode }) {
  return (
    <div className="trang-xac-thuc">
      <div className="xt-hero" aria-hidden="true">
        <svg viewBox="0 0 390 220" preserveAspectRatio="xMidYMax slice">
          <defs>
            <linearGradient id="xt-mo" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0.5" stopColor="#fbf6ef" stopOpacity="0" />
              <stop offset="1" stopColor="#fbf6ef" stopOpacity="1" />
            </linearGradient>
          </defs>
          <rect x="79" y="79" width="76" height="150" fill="#97d4c8" fillOpacity="0.22" />
          <rect x="235" y="79" width="76" height="150" fill="#f9dc9e" fillOpacity="0.35" />
          <path d="M-10 34 H400" stroke="#3a5b76" strokeOpacity="0.14" strokeWidth="2" strokeDasharray="22 16" />
          <path d="M-10 78 H400" stroke="#3a5b76" strokeOpacity="0.2" strokeWidth="2" />
          <path d="M0 78 V220 M78 78 V220 M156 78 V220 M234 78 V220 M312 78 V220 M390 78 V220" stroke="#3a5b76" strokeOpacity="0.2" strokeWidth="2" />
          <rect x="157" y="79" width="76" height="150" fill="#ee8a76" fillOpacity="0.14" />
          <path d="M156 78 V220 M234 78 V220 M156 78 H234" stroke="#ee8a76" strokeWidth="3" />
          <rect x="0" y="0" width="390" height="220" fill="url(#xt-mo)" />
        </svg>
        <div className="xt-logo"><img src="/logo.png" alt="Logo nhà xe" /></div>
      </div>
      <div className="xt-than">
        <span className="nhan-nho">{nhan}</span>
        <h1 className="xt-tieu-de">{tieuDe}</h1>
        <p className="xt-mo-ta">{moTa}</p>
        {children}
      </div>
      {chan && <div className="xt-chan">{chan}</div>}
    </div>
  );
}
