// Kiểm tra giá phía giao diện, cùng quy tắc với GoiCho.DoiGia ở backend (server vẫn kiểm tra lại).
export const GIA_TOI_DA = 1_000_000_000;

export function kiemTraGia(gia: number): string | null {
  if (!Number.isFinite(gia) || gia <= 0) return 'Giá phải lớn hơn 0.';
  if (gia > GIA_TOI_DA) return 'Giá không quá 1.000.000.000 đ.';
  if (gia % 1000 !== 0) return 'Giá phải tròn nghìn đồng.';
  return null;
}

/** 300000 -> 350000: "+16,7%" */
export function phanTramDoi(cu: number, moi: number): string {
  if (!cu) return '';
  const p = ((moi - cu) / cu) * 100;
  return `${p > 0 ? '+' : ''}${p.toLocaleString('vi-VN', { maximumFractionDigits: 1 })}%`;
}
