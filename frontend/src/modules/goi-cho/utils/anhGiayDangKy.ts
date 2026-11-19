/** Kiểm tra sơ bộ ở trình duyệt cho người dùng biết sớm. Kiểm tra THẬT (đọc nội dung tệp) nằm ở server. */
export const ANH_TOI_DA_MB = 5;
export const KIEU_ANH = 'image/jpeg,image/png';

export function kiemTraAnh(f: File | null): string | null {
  if (!f) return 'Chưa chọn ảnh giấy đăng ký xe.';
  if (!['image/jpeg', 'image/png'].includes(f.type)) return 'Chỉ nhận ảnh JPG hoặc PNG.';
  if (f.size > ANH_TOI_DA_MB * 1024 * 1024) return `Ảnh tối đa ${ANH_TOI_DA_MB} MB.`;
  return null;
}
