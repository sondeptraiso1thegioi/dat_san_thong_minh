import type { ThongBaoApp } from '../models';
import { NHAN_LOAI } from '../models';
import { duongDanCho, nhanHanhDong } from '../dichDen';
import { dinhDangNgay } from '@/shared/utils/format';

/**
 * Danh sách thông báo (component trình bày thuần, dùng chung cho hộp thư khách và hộp thư Admin).
 * Bấm cả dòng = đánh dấu đã đọc và mở trang liên quan. Thông báo cần làm gì thì có nhãn bên phải ("Xác nhận ra →").
 */
export function DanhSachThongBao({ ds, laAdmin, khiBam }: { ds: ThongBaoApp[]; laAdmin: boolean; khiBam: (t: ThongBaoApp) => void }) {
  return (
    <ul className="ds-thong-bao">
      {ds.map((t) => {
        const nhan = NHAN_LOAI[t.loai] ?? { ten: t.loai, mau: 'tin' as const };
        const viec = nhanHanhDong(t, laAdmin);
        const coTrang = duongDanCho(t, laAdmin) !== null;
        return (
          <li key={t.id}>
            <button className={`the tb ${t.daDoc ? 'tb-da-doc' : ''}`} onClick={() => khiBam(t)}
                    title={coTrang ? 'Mở trang liên quan' : 'Đánh dấu đã đọc'}>
              <div className="xe-dau">
                <span>
                  <span className={`nhan nhan-${nhan.mau}`}>{nhan.ten}</span>{' '}
                  {!t.daDoc && <span className="nhan nhan-loi">Mới</span>}
                </span>
                <span className="mo">{dinhDangNgay(t.guiLuc)}</span>
              </div>
              <div className="tb-than">
                <p>{t.noiDung}</p>
                {viec && <span className="tb-viec">{viec} →</span>}
              </div>
            </button>
          </li>
        );
      })}
    </ul>
  );
}
