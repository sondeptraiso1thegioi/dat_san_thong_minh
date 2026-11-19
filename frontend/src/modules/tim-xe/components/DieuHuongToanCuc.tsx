import { useCallback, useEffect, useRef, useState } from 'react';
import { useApp } from '@/app/AppContext';
import { useNgheThongBao } from '@/shared/realtime/useNgheThongBao';
import type { DieuHuongCuaToi, KhuCho } from '../models';
import { GiaDieuHuongService } from '../services/dieuHuongGia';
import { DemNguocGiuCho } from './DemNguocGiuCho';
import { ManHinhDanDuong, ThoiGianDaDo } from './ManHinhDanDuong';

/** Đang dẫn đường: hỏi lại mỗi 3 giây (dự phòng khi tín hiệu SignalR phát ở bản API khác). Không dẫn: 15 giây. */
const CHU_KY_DAN = 3_000, CHU_KY_NGHI = 15_000;
/** Kết quả cũ hơn chừng này thì không tự bật màn hình (ví dụ vừa đăng nhập lại). */
const MOI_TRONG_MS = 30 * 60_000;

const daXem = (khoa: string) => { try { return sessionStorage.getItem(`dh:${khoa}`) === '1'; } catch { return false; } };
const ghiDaXem = (khoa: string) => { try { sessionStorage.setItem(`dh:${khoa}`, '1'); } catch { /* chế độ riêng tư: bỏ qua */ } };

const SU_KIEN_MO = 'nx:mo-dan-duong';
/** Trang khác (Tìm xe, khi bấm thông báo "Mời vào khu …") yêu cầu bật màn hình chỉ đường. */
export function moManHinhDanDuong() { window.dispatchEvent(new Event(SU_KIEN_MO)); }

/** Khoá "sự kiện" để tự bật màn hình đúng một lần: phiên mới, đổi trạng thái, hoặc báo hết chỗ. */
function khoaSuKien(d: DieuHuongCuaToi): { khoa: string; luc: string } | null {
  if (d.phien) return { khoa: `${d.phien.id}:${d.phien.trangThai}`, luc: d.phien.khuThucTe?.vaoLuc ?? d.phien.batDauLuc };
  if (d.hetCho) return { khoa: `het:${d.hetCho.luc}`, luc: d.hetCho.luc };
  return null;
}

/**
 * Gắn ở Layout của người dùng: xe vừa qua cổng là màn hình chỉ đường tự bật, ở bất kỳ trang nào.
 * Nghe SignalR (/hub/thong-bao) để cập nhật ngay; hỏi lại định kỳ để không lỡ tín hiệu.
 */
export function DieuHuongToanCuc() {
  const { services } = useApp();
  const [du, setDu] = useState<DieuHuongCuaToi | null>(null);
  const [khu, setKhu] = useState<KhuCho[] | undefined>();
  const [mo, setMo] = useState(false);
  const [anThanh, setAnThanh] = useState<string | null>(null);
  const lanGoi = useRef(0);

  const tai = useCallback(async () => {
    const lan = ++lanGoi.current;
    try {
      const d = await services.dieuHuong.layCuaToi();
      if (lan !== lanGoi.current) return;
      setDu(d);
      if (d.phien || d.hetCho) services.timXe.laySoXeKhu().then((s) => setKhu(s.khu)).catch(() => { /* giữ số cũ */ });
      const sk = khoaSuKien(d);
      if (sk && !daXem(sk.khoa)) {
        ghiDaXem(sk.khoa);
        if (Date.now() - new Date(sk.luc).getTime() < MOI_TRONG_MS) setMo(true);
      }
    } catch { /* chưa có API hoặc lỗi mạng: thử lại ở chu kỳ sau */ }
  }, [services]);

  const dangDan = du?.phien?.trangThai === 'DANG_DAN';
  useEffect(() => {
    void tai();
    const id = window.setInterval(() => { if (!document.hidden) void tai(); }, dangDan ? CHU_KY_DAN : CHU_KY_NGHI);
    return () => window.clearInterval(id);
  }, [tai, dangDan]);
  useNgheThongBao((s) => { if (['KHU', 'VAO', 'RA', 'DIEU_HUONG'].includes(s.loai)) void tai(); });
  useEffect(() => {
    const mo = () => { void tai().then(() => setMo(true)); };
    window.addEventListener(SU_KIEN_MO, mo);
    return () => window.removeEventListener(SU_KIEN_MO, mo);
  }, [tai]);

  async function doiKhu(khuId: number) {
    await services.dieuHuong.doiKhuDich(khuId);
    await tai();
  }

  const p = du?.phien;
  const daDo = p && (p.trangThai === 'DA_TOI' || p.trangThai === 'TOI_KHU_KHAC') && p.khuThucTe;
  const khoaThanh = p ? `thanh:${p.id}:${p.trangThai}` : null;
  const hienThanh = !mo && p && (dangDan || daDo) && anThanh !== khoaThanh;
  const giaLap = services.dieuHuong instanceof GiaDieuHuongService ? services.dieuHuong.giaLap : null;

  return (
    <>
      {hienThanh && (
        <div className={`thanh-dan-duong ${dangDan ? '' : 'thanh-da-do'}`} role="status">
          {dangDan && p ? (
            <>
              <span className="thanh-icon" aria-hidden="true">➜</span>
              <span>Đang dẫn đường tới <strong>{p.khuDich.ten}</strong> · giữ chỗ {du && <DemNguocGiuCho hetHanLuc={p.hetHanLuc} bayGio={du.bayGio} />}</span>
              <button type="button" className="nut nut-nho" onClick={() => setMo(true)}>Mở bản đồ</button>
            </>
          ) : daDo ? (
            <>
              <span className="thanh-icon" aria-hidden="true">P</span>
              <span>Đang đỗ xe tại <strong>{daDo.ten}</strong> · <ThoiGianDaDo tu={daDo.vaoLuc} /></span>
              <button type="button" className="nut nut-phu nut-nho" onClick={() => setMo(true)}>Xem</button>
              <button type="button" className="nut-x" aria-label="Ẩn" onClick={() => setAnThanh(khoaThanh)}>✕</button>
            </>
          ) : null}
        </div>
      )}

      {mo && du && (du.phien || du.hetCho) && (
        <ManHinhDanDuong du={du} khu={khu} onDong={() => setMo(false)} onDoiKhu={doiKhu} />
      )}

      {giaLap && <BangGiaLap giaLap={giaLap} sauKhi={async () => { await tai(); setMo(true); }} />}
    </>
  );
}

/** Chỉ có khi chạy dữ liệu giả: bấm để giả lập camera cổng, camera khu, hết giờ... */
function BangGiaLap({ giaLap, sauKhi }: { giaLap: GiaDieuHuongService['giaLap']; sauKhi: () => Promise<void> }) {
  const [mo, setMo] = useState(false);
  const nut = (ten: string, f: () => Promise<void>) => (
    <button type="button" className="nut nut-phu nut-nho" onClick={async () => { await f(); await sauKhi(); }}>{ten}</button>
  );
  return (
    <div className={`bang-gia-lap ${mo ? 'mo' : ''}`}>
      <button type="button" className="bang-gia-lap-dau" onClick={() => setMo((m) => !m)} aria-expanded={mo}>Giả lập (dữ liệu giả) {mo ? '▾' : '▸'}</button>
      {mo && (
        <div className="bang-gia-lap-than">
          {nut('1. Xe máy vào Cổng X', () => giaLap.vaoCong())}
          {nut('Camera VÀO khu đích thấy xe', giaLap.toiDung)}
          {nut('Camera VÀO khu khác (còn chỗ)', giaLap.toiKhuKhac)}
          {nut('Tới khu đã đầy (bị chặn)', giaLap.toiKhuDay)}
          {nut('Còn 20 giây giữ chỗ', giaLap.sapHetGio)}
          {nut('Hết 5 phút giữ chỗ', giaLap.hetHan)}
          {nut('Xe ra Cổng Y', giaLap.raCong)}
          {nut('Vào cổng khi mọi khu đầy', () => giaLap.vaoCong(true))}
          {nut('Xoá phiên', giaLap.xoa)}
        </div>
      )}
    </div>
  );
}
