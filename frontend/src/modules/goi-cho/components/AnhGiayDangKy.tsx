import { useEffect, useState } from 'react';
import { useApp } from '@/app/AppContext';
import { ANH_TOI_DA_MB, KIEU_ANH } from '../utils/anhGiayDangKy';

/** Hiện ảnh giấy đăng ký. Ảnh lấy qua API có JWT (chỉ chủ xe / Admin xem được), rồi tạo URL tạm trong trình duyệt. */
export function AnhGiayDangKy({ xeId, phienBan = 0 }: { xeId: number; phienBan?: number }) {
  const { services } = useApp();
  const [url, setUrl] = useState<string | null>(null);
  const [loi, setLoi] = useState<string | null>(null);

  useEffect(() => {
    let huy = false, tam: string | null = null;
    setUrl(null); setLoi(null);
    services.xe.layAnhGiayDangKy(xeId)
      .then((b) => { if (!huy) { tam = URL.createObjectURL(b); setUrl(tam); } })
      .catch((e) => { if (!huy) setLoi((e as Error).message); });
    return () => { huy = true; if (tam) URL.revokeObjectURL(tam); };
  }, [services, xeId, phienBan]);

  const [phongTo, setPhongTo] = useState(false);

  if (loi) return <div className="anh-gdk anh-gdk-trong">{loi}</div>;
  if (!url) return <div className="anh-gdk anh-gdk-trong">Đang tải ảnh…</div>;
  return (
    <>
      <button type="button" className="anh-gdk-nut" onClick={() => setPhongTo(true)} title="Bấm để phóng to">
        <img className="anh-gdk" src={url} alt="Ảnh giấy đăng ký xe" />
      </button>
      {phongTo && <XemAnhPhongTo url={url} onDong={() => setPhongTo(false)} />}
    </>
  );
}

/**
 * Xem ảnh phóng to NGAY TRÊN TRANG (lớp phủ), không mở tab mới.
 * Đóng: nút ✕, phím Esc, hoặc bấm ra vùng tối ngoài ảnh. Có xoay 90° (ảnh chụp điện thoại hay bị nằm ngang)
 * và phóng to thêm để đọc số máy / biển số nhỏ.
 */
function XemAnhPhongTo({ url, onDong }: { url: string; onDong: () => void }) {
  const [xoay, setXoay] = useState(0);
  const [zoom, setZoom] = useState(1);
  useEffect(() => {
    const phim = (e: KeyboardEvent) => { if (e.key === 'Escape') onDong(); };
    window.addEventListener('keydown', phim);
    const cu = document.body.style.overflow; document.body.style.overflow = 'hidden'; // không cuộn trang phía sau
    return () => { window.removeEventListener('keydown', phim); document.body.style.overflow = cu; };
  }, [onDong]);
  return (
    <div className="lop-phu" role="dialog" aria-modal="true" aria-label="Ảnh giấy đăng ký phóng to" onClick={onDong}>
      <div className="lop-phu-cong-cu" onClick={(e) => e.stopPropagation()}>
        <button type="button" className="nut nut-phu nut-nho" onClick={() => setZoom((z) => Math.max(1, z - 0.5))} disabled={zoom <= 1}>−</button>
        <span>{Math.round(zoom * 100)}%</span>
        <button type="button" className="nut nut-phu nut-nho" onClick={() => setZoom((z) => Math.min(3, z + 0.5))} disabled={zoom >= 3}>+</button>
        <button type="button" className="nut nut-phu nut-nho" onClick={() => setXoay((r) => (r + 90) % 360)}>Xoay ↻</button>
        <button type="button" className="nut nut-nho" onClick={onDong} aria-label="Đóng">✕ Đóng</button>
      </div>
      <div className="lop-phu-anh" onClick={(e) => e.stopPropagation()}>
        <img src={url} alt="Ảnh giấy đăng ký xe" style={{ transform: `rotate(${xoay}deg) scale(${zoom})` }} />
      </div>
      <div className="lop-phu-goi-y">Esc hoặc bấm ra ngoài ảnh để đóng</div>
    </div>
  );
}

/** Ô chọn ảnh có xem trước. */
export function ChonAnh({ tep, onChon }: { tep: File | null; onChon: (f: File | null) => void }) {
  const [xemTruoc, setXemTruoc] = useState<string | null>(null);
  useEffect(() => {
    if (!tep) { setXemTruoc(null); return; }
    const u = URL.createObjectURL(tep); setXemTruoc(u);
    return () => URL.revokeObjectURL(u);
  }, [tep]);
  return (
    <div className="chon-anh">
      <input type="file" accept={KIEU_ANH} onChange={(e) => onChon(e.target.files?.[0] ?? null)} />
      <span className="mo">JPG hoặc PNG, tối đa {ANH_TOI_DA_MB} MB. Chụp rõ biển số và tên chủ xe.</span>
      {xemTruoc && <img className="anh-gdk anh-gdk-nho" src={xemTruoc} alt="Xem trước ảnh giấy đăng ký" />}
    </div>
  );
}
