import type { ChoConBan, TrangThaiDuyet } from '../models';
import { tenLoaiXe } from '@/shared/utils/format';

// Component trình bày riêng của module GoiCho: trạng thái duyệt xe, thanh số chỗ còn bán.

export const tenTrangThaiDuyet: Record<TrangThaiDuyet, string> = {
  CHO_DUYET: 'Chờ duyệt',
  DA_DUYET: 'Đã duyệt',
  TU_CHOI: 'Bị từ chối',
  BI_GO: 'Bị gỡ',
};

export function NhanTrangThai({ tt }: { tt: TrangThaiDuyet }) {
  const lop = tt === 'DA_DUYET' ? 'nhan-ok' : tt === 'CHO_DUYET' ? 'nhan-cho' : 'nhan-loi';
  return <span className={`nhan ${lop}`}>{tenTrangThaiDuyet[tt]}</span>;
}

export function ChoConBanThe({ c }: { c: ChoConBan }) {
  const daDung = c.sucChua - c.soChoConBan;
  const phanTram = c.sucChua ? Math.round((daDung / c.sucChua) * 100) : 100;
  return (
    <div className={`cho-con ${c.soChoConBan === 0 ? 'cho-het' : ''}`}>
      <div className="cho-con-dau">
        <span>{tenLoaiXe(c.loaiXe)}</span>
        <strong>{c.soChoConBan === 0 ? 'Hết chỗ' : `Còn ${c.soChoConBan} / ${c.sucChua} chỗ`}</strong>
      </div>
      <div className="thanh"><div className="thanh-day" style={{ width: `${phanTram}%` }} /></div>
    </div>
  );
}
