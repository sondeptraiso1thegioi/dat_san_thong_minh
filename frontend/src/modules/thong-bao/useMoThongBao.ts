import { useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useApp } from '@/app/AppContext';
import { LOI_KHONG_MO_DUOC } from '@/shared/ui/useDichDen';
import { duongDanCho, type ThongBaoDich } from './dichDen';

/**
 * Bấm một thông báo (dòng trong hộp thư hoặc thẻ nổi): đánh dấu đã đọc → chuyển tới trang đích.
 * Có hành động mà không mở được (không có quyền) → về hộp thư, báo "Không mở được nội dung này".
 * Trả về true nếu đã chuyển trang.
 */
export function useMoThongBao() {
  const { services, phien } = useApp();
  const nav = useNavigate();
  const laAdmin = phien?.vaiTro === 'ADMIN';

  return useCallback(async (tb: ThongBaoDich & { id?: number | null; daDoc?: boolean }) => {
    if (tb.id && !tb.daDoc) {
      try { await services.thongBao.danhDauDaDoc(tb.id); } catch { /* lỗi mạng: vẫn mở trang, lần tải sau đánh dấu lại */ }
    }
    const dich = duongDanCho(tb, laAdmin);
    if (dich) { nav(dich); return true; }
    if (tb.hanhDong) { nav(`${laAdmin ? '/admin/thong-bao' : '/thong-bao'}?loi=${LOI_KHONG_MO_DUOC}`); return true; }
    return false;
  }, [services, nav, laAdmin]);
}
