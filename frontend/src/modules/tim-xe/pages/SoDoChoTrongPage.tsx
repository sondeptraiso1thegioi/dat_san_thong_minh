import { useState } from 'react';
import { DangTai, ThongBao } from '@/shared/ui/ui';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';
import { useApp } from '@/app/AppContext';
import { BanDoBai } from '../components/BanDoBai';
import { MucChoKhu } from '../components/MucChoKhu';
import { TheKhu } from '../components/TheKhu';
import type { DuongDiBanDo } from '../models';
import { TEN_MUC } from '../models';
import { khuXeMayTrenSoDo } from '../soDoXeMay';

/**
 * Sơ đồ chỗ trống (Admin và người dùng cùng xem). Hiện chỉ làm XE MÁY: 3 chấm R, S, Hầm B1/B2.
 * Chấm khu ô tô A–Q được ẩn trên giao diện, dữ liệu trong DB vẫn giữ.
 * Số trên chấm là chỗ còn trống (đã trừ suất đang giữ cho xe vừa qua cổng). Tự cập nhật mỗi 10 giây.
 * Người dùng: chạm chấm khu → thẻ khu → "Xem đường đi" từ Cổng vào X (chỉ để xem: không tạo phiên, không giữ suất).
 * Admin: chạm chấm chỉ xem thông tin khu, không có nút chỉ đường.
 */
export function SoDoChoTrongPage() {
  const { services, phien } = useApp();
  const laAdmin = phien?.vaiTro === 'ADMIN';
  const ds = useTaiDuLieu(() => services.timXe.laySoXeKhu(), [], 10_000);
  const khuXeMay = khuXeMayTrenSoDo(ds.duLieu?.khu);
  const tongCon = khuXeMay.reduce((s, k) => s + k.conLai, 0);
  const tongSucChua = khuXeMay.reduce((s, k) => s + k.sucChua, 0);

  const [chonId, setChonId] = useState<number | null>(null);
  const [duong, setDuong] = useState<DuongDiBanDo | null>(null);
  const [loiDuong, setLoiDuong] = useState<string | null>(null);
  const chon = khuXeMay.find((k) => k.id === chonId) ?? null;

  async function xemDuong(khuId: number) {
    setLoiDuong(null);
    try { setDuong(await services.dieuHuong.layDuongDi(khuId)); }
    catch (e) { setLoiDuong((e as Error).message); }
  }

  return (
    <>
      <div className="tieu-de-trang">
        <h1>Sơ đồ chỗ trống</h1>
        <button className="nut nut-phu nut-nho" onClick={() => ds.taiLai()} disabled={ds.dangTai}>Tải lại</button>
      </div>

      <div className="the tong-cho-xe-may" aria-live="polite">
        <span className="tong-cho-so">{ds.duLieu ? tongCon.toLocaleString('vi-VN') : '–'}</span>
        <div>
          <div className="tong-cho-nhan">chỗ xe máy còn trống</div>
          <div className="mo">
            {khuXeMay.length > 0
              ? `trên ${tongSucChua.toLocaleString('vi-VN')} chỗ ở ${khuXeMay.length} khu · cập nhật ${gio(ds.duLieu?.bayGio)}`
              : ds.duLieu ? 'Chưa có số liệu khu xe máy' : 'Đang tải…'}
          </div>
        </div>
      </div>

      <div className="chu-thich-so-do">
        <span><i className="cham muc-CON_NHIEU" /> {TEN_MUC.CON_NHIEU} (trên 20%)</span>
        <span><i className="cham muc-SAP_DAY" /> {TEN_MUC.SAP_DAY} (20% trở xuống)</span>
        <span><i className="cham muc-DAY" /> {TEN_MUC.DAY}</span>
        <span><i className="cham muc-CHUA" /> Chưa có số liệu</span>
      </div>
      {ds.loi && <ThongBao loai="loi">{ds.loi}</ThongBao>}

      <div className="so-do-khung">
        <BanDoBai khu={ds.duLieu?.khu} duong={duong} khuDichId={duong?.denKhuId} khuChonId={chonId} hienViTri={!!duong}
                  onChamKhu={(k) => setChonId((c) => (c === k.id ? null : k.id))} />
        {chon
          ? <TheKhu khu={chon} cheDo={laAdmin ? 'admin' : 'xem'} dangHienDuong={duong?.denKhuId === chon.id}
                    onXemDuong={() => void xemDuong(chon.id)} onAnDuong={() => setDuong(null)} onDong={() => setChonId(null)} />
          : <p className="mo">{laAdmin ? 'Chạm vào chấm khu để xem số liệu.' : 'Chạm vào chấm R, S hoặc Hầm để xem chỗ trống và đường đi từ Cổng vào X.'}</p>}
        {loiDuong && <ThongBao loai="loi">{loiDuong}</ThongBao>}
        {duong && !chon && (
          <button className="nut nut-phu nut-nho" onClick={() => setDuong(null)}>Ẩn đường</button>
        )}
      </div>

      <h2>Chi tiết khu xe máy</h2>
      {ds.dangTai && !ds.duLieu && <DangTai />}
      {ds.duLieu && <MucChoKhu khu={khuXeMay} chiTiet />}
    </>
  );
}

function gio(iso?: string) {
  return iso ? new Date(iso).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit', second: '2-digit' }) : '';
}
