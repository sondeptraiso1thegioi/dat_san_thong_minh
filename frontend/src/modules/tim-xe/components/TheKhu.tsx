import { useApp } from '@/app/AppContext';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';
import type { KhuCho } from '../models';
import { TEN_MUC } from '../models';

/**
 * Thẻ nhỏ khi chạm một chấm khu: tên, chỗ còn, mức, độ dài đường đi từ Cổng vào X và nút thao tác.
 * cheDo "dan-duong": nút "Chỉ đường tới đây" (đổi khu đích) · "xem": "Xem đường đi" / "Ẩn đường" · "admin": chỉ xem.
 * Khu đầy (chỗ còn đã trừ suất đang giữ ≤ 0) thì nút khoá, ghi "Khu đã đầy".
 */
export function TheKhu({ khu, cheDo, laDich = false, dangHienDuong = false, dangGui = false, onChiDuong, onXemDuong, onAnDuong, onDong }: {
  khu: KhuCho;
  cheDo: 'dan-duong' | 'xem' | 'admin';
  laDich?: boolean;
  dangHienDuong?: boolean;
  dangGui?: boolean;
  onChiDuong?: () => void;
  onXemDuong?: () => void;
  onAnDuong?: () => void;
  onDong: () => void;
}) {
  const { services } = useApp();
  const duong = useTaiDuLieu(
    () => (cheDo === 'admin' ? Promise.resolve(null) : services.dieuHuong.layDuongDi(khu.id)), [khu.id, cheDo]);
  const day = khu.conLai <= 0;

  return (
    <div className="the the-khu" role="region" aria-label={`Thông tin ${khu.ten}`}>
      <div className="the-khu-dau">
        <strong>{khu.ten}</strong>
        <span className={`nhan nhan-muc-${khu.muc}`}>{TEN_MUC[khu.muc]}</span>
        <button type="button" className="nut-x" onClick={onDong} aria-label="Đóng thẻ">✕</button>
      </div>
      <div className="the-khu-so">
        <span className="lon">{khu.conLai}</span><span className="mo"> / {khu.sucChua} chỗ còn trống</span>
      </div>
      {cheDo !== 'admin' && (
        <div className="mo">
          {duong.duLieu ? `Đường đi khoảng ${duong.duLieu.tongMet} m từ Cổng vào X` : duong.loi ? 'Chưa có đường đi tới khu này' : 'Đang tính đường đi…'}
        </div>
      )}
      {cheDo === 'dan-duong' && (
        <button type="button" className="nut the-khu-nut" disabled={day || laDich || dangGui} onClick={onChiDuong}>
          {laDich ? 'Đang chỉ đường tới khu này' : day ? 'Khu đã đầy' : dangGui ? 'Đang đổi…' : 'Chỉ đường tới đây'}
        </button>
      )}
      {cheDo === 'xem' && (
        dangHienDuong
          ? <button type="button" className="nut nut-phu the-khu-nut" onClick={onAnDuong}>Ẩn đường</button>
          : <button type="button" className="nut the-khu-nut" disabled={!duong.duLieu} onClick={onXemDuong}>Xem đường đi</button>
      )}
    </div>
  );
}
