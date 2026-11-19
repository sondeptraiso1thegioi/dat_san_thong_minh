import { Navigate, Outlet } from 'react-router-dom';
import { useApp } from '@/app/AppContext';
import type { VaiTro } from '@/shared/models';

/** Chặn vào trang khi chưa đăng nhập hoặc sai vai trò. Server vẫn kiểm tra lại, đây chỉ là lớp giao diện. */
export function BaoVe({ vaiTro }: { vaiTro: VaiTro }) {
  const { phien } = useApp();
  if (!phien) return <Navigate to="/dang-nhap" replace />;
  if (phien.vaiTro !== vaiTro) return <Navigate to={phien.vaiTro === 'ADMIN' ? '/admin/cong' : '/tim-xe'} replace />;
  return <Outlet />;
}
