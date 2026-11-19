import { useState } from 'react';
import { Link } from 'react-router-dom';
import { DangTai, ThongBao } from '@/shared/ui/ui';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';
import { LOI_KHONG_MO_DUOC, useThamSoMotLan } from '@/shared/ui/useDichDen';
import { useApp } from '@/app/AppContext';
import type { ThongBaoApp } from '../models';
import { DanhSachThongBao } from '../components/DanhSachThongBao';
import { hanhDongCua } from '../dichDen';
import { useMoThongBao } from '../useMoThongBao';

/**
 * Hộp thư của khách: xe vào / ra, chờ xác nhận ra, sắp hết hạn gói, nợ quá hạn, nghi mất xe. Tự tải lại mỗi 15 giây.
 * Bấm thông báo: đánh dấu đã đọc và mở đúng chỗ cần làm (dichDen.ts).
 */
export function ThongBaoPage() {
  const { services } = useApp();
  const hop = useTaiDuLieu(() => services.thongBao.layHopThu(), [], 15_000);
  const [loi, setLoi] = useState<string | null>(null);
  const [{ loi: loiUrl }, boLoiUrl] = useThamSoMotLan('loi');
  const mo = useMoThongBao();

  async function lam(viec: () => Promise<unknown>) {
    setLoi(null);
    try { await viec(); await hop.taiLai(); } catch (e) { setLoi((e as Error).message); }
  }
  async function bam(t: ThongBaoApp) {
    boLoiUrl();
    if (!(await mo(t))) await hop.taiLai(); // không có trang đích: chỉ đánh dấu đã đọc
  }
  const coYeuCauRa = hop.duLieu?.thongBao.some((t) => !t.daDoc && hanhDongCua(t, false) === 'XAC_NHAN_RA');

  return (
    <>
      <div className="tieu-de-trang">
        <h1>Thông báo</h1>
        <div className="hang-nut" style={{ marginTop: 0 }}>
          <button className="nut nut-phu nut-nho" onClick={() => lam(() => services.thongBao.docHet())}
                  disabled={!hop.duLieu || hop.duLieu.soChuaDoc === 0}>Đánh dấu đã đọc hết</button>
          <button className="nut nut-phu nut-nho" onClick={() => hop.taiLai()} disabled={hop.dangTai}>Tải lại</button>
        </div>
      </div>
      {loiUrl === LOI_KHONG_MO_DUOC && <ThongBao loai="loi">Không mở được nội dung này (không còn tồn tại hoặc bạn không có quyền xem).</ThongBao>}
      {coYeuCauRa && <ThongBao loai="canh-bao">Có xe đang chờ bạn xác nhận ở cổng ra. <Link to="/ra-vao">Mở trang Ra vào</Link></ThongBao>}
      {loi && <ThongBao loai="loi">{loi}</ThongBao>}
      {hop.loi && <ThongBao loai="loi">{hop.loi}</ThongBao>}
      {hop.dangTai && !hop.duLieu && <DangTai />}
      {hop.duLieu && hop.duLieu.thongBao.length === 0 && <ThongBao loai="tin">Chưa có thông báo nào.</ThongBao>}
      {hop.duLieu && hop.duLieu.thongBao.length > 0 && (
        <>
          <p><strong>{hop.duLieu.soChuaDoc}</strong> thông báo chưa đọc</p>
          <DanhSachThongBao ds={hop.duLieu.thongBao} laAdmin={false} khiBam={(t) => void bam(t)} />
        </>
      )}
    </>
  );
}
