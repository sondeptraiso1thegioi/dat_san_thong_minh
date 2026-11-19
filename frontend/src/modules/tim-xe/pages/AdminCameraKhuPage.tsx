import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useApp } from '@/app/AppContext';
import { ThongBao } from '@/shared/ui/ui';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';
import { hienThiBienSo } from '@/shared/utils/format';
import { chupKhung, lietKeCamera, NguonFileVideo, NguonWebcam, type INguonHinh } from '@/modules/cong-ra-vao/camera/nguonHinh';
import type { KetQuaCameraKhu } from '../models';
import { TEN_MUC } from '../models';

/** Giống màn hình cổng: quét mỗi 1 giây, cùng một biển trong 30 giây chỉ xử lý một lần. */
const CHU_KY_MS = 1_000;
const CHONG_LAP_MS = 30_000;
const SO_DONG_VUA_QUET = 8;

type Huong = 'vao' | 'ra';
/** vao: đã ghi xe vào khu · ra: đã ghi xe ra khỏi khu · bo: đọc lặp / không cần ghi · la: biển không thuộc xe nào trong bãi · nghi: AI chưa chắc. */
type Loai = 'vao' | 'ra' | 'bo' | 'la' | 'nghi' | 'day';
interface DongQuet { luc: Date; bienSo: string; loai: Loai; nhan: string }

const NHAN: Record<Loai, string> = { vao: 'Vào khu +1', ra: 'Ra khu −1', bo: 'Bỏ qua', la: 'Không khớp', nghi: 'Chờ chọn biển', day: 'Khu đầy, không +1' };
const LOP: Record<Loai, string> = { vao: 'ok', ra: 'ra', bo: 'cho', la: 'loi', nghi: 'nghi', day: 'loi' };

function phanLoai(kq: KetQuaCameraKhu): Loai {
  if (kq.ketQua === 'DA_GHI') return kq.huong === 'RA' ? 'ra' : 'vao';
  if (kq.ketQua === 'BO_QUA') return 'bo';
  if (kq.ketQua === 'KHU_DAY') return 'day';
  return (kq.ungVien?.length ?? 0) > 0 && !kq.bienSo ? 'nghi' : 'la';
}

const gioPhut = (d: Date) => d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });

/**
 * Màn hình camera khu (module Hành trình xe), cùng cách làm với camera cổng vào / cổng ra.
 * Mỗi khu có 2 camera: một camera soi hướng VÀO khu, một camera soi hướng RA khỏi khu.
 * Tự chụp khung hình mỗi giây, AI (Model A + PaddleOCR) đọc biển, khớp với xe đang gửi trong bãi rồi ghi vào database:
 * camera hướng vào → "xe X vào khu A" (số xe khu A +1), camera hướng ra → "xe X ra khỏi khu A" (số xe khu A −1).
 * Số xe từng khu hiện ngay ở trang Quản lý bãi và Sơ đồ chỗ trống.
 */
export function AdminCameraKhuPage() {
  const { services } = useApp();
  const ch = useTaiDuLieu(() => services.baiXe.layCauHinh());
  const soXe = useTaiDuLieu(() => services.timXe.laySoXeKhu(), [], 5_000);
  const taiLaiSoXe = soXe.taiLai;

  const video = useRef<HTMLVideoElement>(null);
  const canvas = useRef<HTMLCanvasElement>(null);
  const oFile = useRef<HTMLInputElement>(null);
  const [thietBi, setThietBi] = useState<{ id: string; ten: string }[]>([]);
  const [nguon, setNguon] = useState<INguonHinh | null>(null);
  const [tiLe, setTiLe] = useState(16 / 9);
  const [dangQuet, setDangQuet] = useState(false);
  const [khuId, setKhuId] = useState(0);
  const [huong, setHuong] = useState<Huong>('vao');
  const [hien, setHien] = useState<{ kq: KetQuaCameraKhu; loai: Loai } | null>(null);
  const [vuaQuet, setVuaQuet] = useState<DongQuet[]>([]);
  const [dem, setDem] = useState({ vao: 0, ra: 0 });
  const [goTay, setGoTay] = useState('');
  const [loi, setLoi] = useState<string | null>(null);
  const [msAi, setMsAi] = useState<number | null>(null);
  const [gio, setGio] = useState(new Date());
  const dangGui = useRef(false);
  const daXuLy = useRef(new Map<string, number>());

  // Khu có camera khu (hướng vào / hướng ra / kiểu cũ hai hướng)
  const cameraKhu = useMemo(() => ch.duLieu?.camera.filter((c) => c.vaiTro === 'KHU_VAO' || c.vaiTro === 'KHU_RA' || c.vaiTro === 'LOI_VAO_KHU') ?? [], [ch.duLieu]);
  // Chỉ khu đang dùng (khu tạm tắt như Khu A, B ô tô không hiện; bật lại ở Quản lý bãi)
  const dsKhu = useMemo(() => (ch.duLieu?.khu ?? []).filter((k) => k.dangDung && cameraKhu.some((c) => c.khuId === k.id)), [ch.duLieu, cameraKhu]);
  useEffect(() => { if (!khuId && dsKhu.length) setKhuId(dsKhu[0].id); }, [khuId, dsKhu]);
  const cam = cameraKhu.find((c) => c.khuId === khuId && c.vaiTro === (huong === 'vao' ? 'KHU_VAO' : 'KHU_RA'))
    ?? cameraKhu.find((c) => c.khuId === khuId && c.vaiTro === 'LOI_VAO_KHU');
  const khuSo = soXe.duLieu?.khu.find((k) => k.id === khuId);
  const tenKhu = dsKhu.find((k) => k.id === khuId)?.ten ?? '';

  useEffect(() => { const t = setInterval(() => setGio(new Date()), 1000); return () => clearInterval(t); }, []);
  useEffect(() => { void lietKeCamera().then(setThietBi).catch(() => setThietBi([])); }, []);
  // Đổi khu / hướng: bắt đầu lại phần kết quả
  useEffect(() => { setHien(null); daXuLy.current.clear(); }, [khuId, huong]);

  useEffect(() => {
    const v = video.current;
    if (!nguon || !v) return;
    let huy = false;
    setLoi(null);
    nguon.mo(v)
      .then(() => { if (!huy) { setDangQuet(true); void lietKeCamera().then(setThietBi); } })
      .catch((e: Error) => { if (!huy) { setLoi(e.name === 'NotAllowedError' ? 'Chưa cho phép dùng camera: bấm biểu tượng camera trên thanh địa chỉ để cho phép.' : e.message); setDangQuet(false); } });
    return () => { huy = true; nguon.dong(v); setDangQuet(false); };
  }, [nguon]);

  const hienKetQua = useCallback((kq: KetQuaCameraKhu) => {
    const loai = phanLoai(kq);
    setHien({ kq, loai });
    setVuaQuet((ds) => [{ luc: new Date(), bienSo: kq.bienSo ?? kq.ungVien?.[0] ?? '—', loai, nhan: NHAN[loai] }, ...ds].slice(0, SO_DONG_VUA_QUET));
    if (loai === 'vao') setDem((d) => ({ ...d, vao: d.vao + 1 }));
    if (loai === 'ra') setDem((d) => ({ ...d, ra: d.ra + 1 }));
    if (loai === 'vao' || loai === 'ra') void taiLaiSoXe();
    if (loai === 'nghi') setDangQuet(false); // chờ quản lý chọn biển
  }, [taiLaiSoXe]);

  // Vòng quét: mỗi giây chụp một khung, chỉ gửi khi lần trước đã xong
  useEffect(() => {
    if (!dangQuet || !cam) return;
    const t = setInterval(async () => {
      const v = video.current, c = canvas.current;
      if (dangGui.current || !v || !c || v.paused) return;
      dangGui.current = true;
      try {
        const anh = await chupKhung(v, c);
        if (!anh) return;
        const bd = performance.now();
        const kq = await services.timXe.cameraKhuAnh(cam.id, anh, undefined, true);
        setMsAi(Math.round(performance.now() - bd));
        if (kq.ketQua === 'KHONG_THAY_BIEN') return;
        const khoa = kq.bienSo ?? kq.ungVien?.[0];
        if (!khoa) return;
        const truoc = daXuLy.current.get(khoa);
        if (truoc && Date.now() - truoc < CHONG_LAP_MS) return; // xe còn đứng trước camera
        daXuLy.current.set(khoa, Date.now());
        hienKetQua(kq);
      } catch (e) { setLoi((e as Error).message); }
      finally { dangGui.current = false; }
    }, CHU_KY_MS);
    return () => clearInterval(t);
  }, [dangQuet, cam, services, hienKetQua]);

  /** Quản lý gõ tay hoặc bấm chọn biển AI đoán. */
  async function guiTay(bienSo: string) {
    if (!bienSo.trim() || !cam) return;
    setLoi(null);
    try {
      const kq = await services.timXe.cameraKhuBienSo(cam.id, bienSo.trim());
      if (kq.bienSo) daXuLy.current.set(kq.bienSo, Date.now());
      hienKetQua(kq);
      setGoTay('');
      if (nguon) setDangQuet(true);
    } catch (e) { setLoi((e as Error).message); }
  }

  function chonNguon(gt: string) {
    if (gt === '__file') { oFile.current?.click(); return; }
    const tb = thietBi.find((c) => c.id === gt);
    setNguon(new NguonWebcam(gt || null, tb?.ten ?? 'Camera mặc định'));
  }

  const kb = hien?.kq.khungBien;
  return (
    <div className="mc">
      <div className="mc-dau">
        <h1>Camera khu</h1>
        <select className="mc-chon" value={khuId} onChange={(e) => setKhuId(Number(e.target.value))} aria-label="Chọn khu">
          {dsKhu.map((k) => <option key={k.id} value={k.id}>{k.ten}</option>)}
        </select>
        <div className="mc-huong" role="group" aria-label="Hướng camera">
          <button className={huong === 'vao' ? 'chon' : ''} onClick={() => setHuong('vao')}>⇥ Hướng vào khu</button>
          <button className={huong === 'ra' ? 'chon' : ''} onClick={() => setHuong('ra')}>⇤ Hướng ra khỏi khu</button>
        </div>
        <span className="mc-chip"><span className={`mc-cham ${dangQuet ? 'bat' : ''}`} />{nguon ? nguon.ten : 'Chưa chọn nguồn'}</span>
        <select className="mc-chon" value="" onChange={(e) => chonNguon(e.target.value)} aria-label="Chọn nguồn hình">
          <option value="" disabled>Nguồn ▾</option>
          {thietBi.length === 0 && <option value="">Camera mặc định</option>}
          {thietBi.map((c) => <option key={c.id} value={c.id}>{c.ten}</option>)}
          <option value="__file">File video…</option>
        </select>
        <input ref={oFile} type="file" accept="video/*" hidden onChange={(e) => { const f = e.target.files?.[0]; if (f) setNguon(new NguonFileVideo(f)); e.target.value = ''; }} />
        <button className="mc-chip" onClick={() => void (document.fullscreenElement ? document.exitFullscreen() : document.documentElement.requestFullscreen())}>Toàn màn hình ⛶</button>
      </div>

      {ch.loi && <ThongBao loai="loi">{ch.loi}</ThongBao>}
      {ch.duLieu && dsKhu.length === 0 && <ThongBao loai="canh-bao">Chưa có camera khu nào. Thêm camera "hướng vào" và "hướng ra" cho từng khu ở trang Quản lý bãi → Camera.</ThongBao>}
      {ch.duLieu && dsKhu.length > 0 && !cam && (
        <ThongBao loai="canh-bao">{tenKhu} chưa có camera {huong === 'vao' ? 'hướng vào' : 'hướng ra'}. Thêm ở trang Quản lý bãi → Camera.</ThongBao>
      )}

      <div className="mc-khung">
        <section>
          <div className="mc-cam" style={{ aspectRatio: String(tiLe) }}>
            <video ref={video} playsInline onLoadedMetadata={(e) => { const v = e.currentTarget; if (v.videoWidth) setTiLe(v.videoWidth / v.videoHeight); }} />
            {!nguon && (
              <div className="mc-trong">
                <p>Chọn nguồn hình để camera {huong === 'vao' ? 'hướng vào' : 'hướng ra'} {tenKhu} bắt đầu quét</p>
                <div className="hang-nut">
                  <button className="nut" onClick={() => chonNguon(thietBi[0]?.id ?? '')}>Mở camera</button>
                  <button className="nut nut-phu" onClick={() => oFile.current?.click()}>Chọn file video</button>
                </div>
              </div>
            )}
            {kb && hien && (
              <div className={`mc-hop mc-${LOP[hien.loai]}`} style={{ left: `${kb.x * 100}%`, top: `${kb.y * 100}%`, width: `${kb.rong * 100}%`, height: `${kb.cao * 100}%` }}>
                <span>{hienThiBienSo(hien.kq.bienSo ?? '?')}{hien.kq.doTinCay != null && ` · ${Math.round(hien.kq.doTinCay * 100)}%`}</span>
              </div>
            )}
            {nguon && (
              <div className="mc-tren">
                <span className="mc-nhan"><span className={`mc-rec ${dangQuet ? '' : 'dung'}`} />{dangQuet ? 'ĐANG QUÉT' : 'TẠM DỪNG'} · {cam?.ten ?? '—'} · {gio.toLocaleTimeString('vi-VN')}</span>
                <span className="mc-nhan">Model A + PaddleOCR{msAi != null && ` · ${msAi} ms`}</span>
              </div>
            )}
          </div>
          <canvas ref={canvas} hidden />
          <form className="mc-duoi" onSubmit={(e) => { e.preventDefault(); void guiTay(goTay); }}>
            <input value={goTay} onChange={(e) => setGoTay(e.target.value)} placeholder="Gõ tay biển số nếu AI không đọc được, VD: 29B1-123.45" />
            <button className="nut" disabled={!goTay.trim() || !cam}>{huong === 'vao' ? 'Ghi vào khu' : 'Ghi ra khỏi khu'}</button>
            <button type="button" className="nut nut-phu" disabled={!nguon} onClick={() => setDangQuet((q) => !q)}>{dangQuet ? 'Tạm dừng quét' : 'Quét tiếp'}</button>
          </form>
          {loi && <p className="chu-loi mc-bao-loi">{loi}</p>}
        </section>

        <aside className="mc-ben">
          {hien ? <KhungKetQua kq={hien.kq} loai={hien.loai} chon={guiTay} boQua={() => { setHien(null); setDangQuet(!!nguon); }} />
            : <div className="mc-kq mc-cho-xe"><div className="mc-tt">Đang chờ xe</div>
                <p className="mo">Xe đi qua camera {huong === 'vao' ? 'hướng vào' : 'hướng ra'} {tenKhu}: AI đọc biển, ghi xe {huong === 'vao' ? 'vào' : 'ra khỏi'} khu và {huong === 'vao' ? 'cộng' : 'trừ'} số xe của khu.</p></div>}
          {khuSo && (
            <div className={`the khu-cho muc-${khuSo.muc}`}>
              <div className="khu-cho-dau"><strong>{khuSo.ten}</strong><span className={`nhan nhan-muc-${khuSo.muc}`}>{TEN_MUC[khuSo.muc]}</span></div>
              <div className="khu-cho-so"><span className="lon">{khuSo.soXe}</span> <span className="mo">xe đang trong khu · sức chứa {khuSo.sucChua} · còn {khuSo.conLai} chỗ</span></div>
              <div className="thanh"><div className="thanh-day" style={{ width: `${Math.min(100, khuSo.sucChua ? ((khuSo.soXe + khuSo.soGiu) / khuSo.sucChua) * 100 : 100)}%` }} /></div>
            </div>
          )}
          <div className="mc-dem">
            <div><b>{dem.vao}</b><span>Đã ghi vào khu</span></div>
            <div><b>{dem.ra}</b><span>Đã ghi ra khu</span></div>
          </div>
          <div className="the">
            <h3>Vừa quét</h3>
            {vuaQuet.length === 0 && <p className="mo">Chưa có xe nào.</p>}
            {vuaQuet.map((d, i) => (
              <div key={i} className="mc-dong">
                <span className="mo">{gioPhut(d.luc)}</span>
                <code>{hienThiBienSo(d.bienSo)}</code>
                <span className={`mc-tag mc-${LOP[d.loai]}`}>{d.nhan}</span>
              </div>
            ))}
          </div>
        </aside>
      </div>
    </div>
  );
}

const TIEU_DE: Record<Loai, { icon: string; tt: string }> = {
  vao: { icon: '✓', tt: 'Đã ghi xe vào khu' }, ra: { icon: '✓', tt: 'Đã ghi xe ra khỏi khu' },
  bo: { icon: '…', tt: 'Bỏ qua' }, la: { icon: '!', tt: 'Không khớp xe trong bãi' }, nghi: { icon: '?', tt: 'AI chưa chắc · chọn biển' },
  day: { icon: '■', tt: 'Khu đã đầy · không cho vào, không +1' },
};

function KhungKetQua({ kq, loai, chon, boQua }: { kq: KetQuaCameraKhu; loai: Loai; chon: (b: string) => void; boQua: () => void }) {
  const t = TIEU_DE[loai];
  return (
    <div className={`mc-kq mc-${LOP[loai]}`} role="status" aria-live="assertive">
      <div className="mc-tt"><span className="mc-icon">{t.icon}</span>{t.tt}</div>
      {kq.bienSo && <div className="mc-bien">{hienThiBienSo(kq.bienSo)}</div>}
      <div className="mo">{kq.doTinCay != null && kq.doTinCay < 1 ? `AI đọc ${Math.round(kq.doTinCay * 100)}%` : 'Quản lý nhập tay'}</div>
      {(loai === 'vao' || loai === 'ra') && (
        <div className="mc-khu">
          <div className="mc-khu-chu">{loai === 'vao' ? '+1' : '−1'}</div>
          <div><b>{kq.khu}</b><div className="mo">{kq.soXeTrongKhu != null ? `Khu hiện có ${kq.soXeTrongKhu} xe` : ''}{kq.sucChua != null ? ` / sức chứa ${kq.sucChua}` : ''}</div></div>
        </div>
      )}
      <div className="mc-phu">{kq.thongDiep}</div>
      {loai === 'nghi' && (
        <div className="mc-ung">
          {kq.ungVien?.map((b, i) => <button key={b} className={i === 0 ? 'chon' : ''} onClick={() => chon(b)}>{hienThiBienSo(b)}</button>)}
          <button className="bo" onClick={boQua}>Bỏ qua</button>
        </div>
      )}
    </div>
  );
}
