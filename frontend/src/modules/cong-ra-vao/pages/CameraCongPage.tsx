import { useCallback, useEffect, useRef, useState } from 'react';
import { useApp } from '@/app/AppContext';
import { hienThiBienSo } from '@/shared/utils/format';
import type { ChiDan } from '@/modules/tim-xe/models';
import type { KetQuaCong, LuotCong } from '../models';
import { TEN_TRANG_THAI_LUOT } from '../models';
import { ThongBao } from '@/shared/ui/ui';
import { dinhDangNgay } from '@/shared/utils/format';
import { soId, toSang, useKhiMoTuThongBao, useThamSoMotLan } from '@/shared/ui/useDichDen';
import { chupKhung, lietKeCamera, NguonFileVideo, NguonWebcam, type INguonHinh } from '../camera/nguonHinh';

/** Giá trị đã chốt trong đề cương v10: quét mỗi 1 giây, cùng một biển trong 30 giây chỉ xử lý một lần. */
const CHU_KY_MS = 1_000;
const CHONG_LAP_MS = 30_000;
const SO_DONG_VUA_QUET = 6;

export type Cong = 'vao' | 'ra';
/** ok: mời vào · ra: mời ra · cho: đang chờ xác nhận trên app · thanhtoan: chờ trả phí quá hạn · goi / dk / loi: từ chối · nghi: AI chưa chắc. */
type Loai = 'ok' | 'ra' | 'cho' | 'thanhtoan' | 'goi' | 'dk' | 'nghi' | 'loi';
interface DongQuet { luc: Date; bienSo: string; loai: Loai; nhan: string }
/** Kết quả đang hiện: kq của backend + trạng thái mới nhất của lượt (cổng ra) + ảnh lúc ra vừa chụp. */
interface HienThi { kq: KetQuaCong; loai: Loai; thongDiep: string; anhRa: string | null; nhanLuc: number }

/** Kết quả backend → kiểu khung hiện trên màn hình. Lý do từ chối lấy đúng câu backend trả (XeVaoUseCase / XeRaUseCase). */
export function phanLoai(kq: KetQuaCong): Loai {
  if (kq.loai === 'RA') {
    if (kq.choQua) return 'ra';
    if (kq.dangChoXacNhan) return 'cho';
    if (kq.choThanhToan) return 'thanhtoan';
  } else if (kq.choQua) return 'ok';
  if ((kq.ungVien?.length ?? 0) > 0 || kq.cheDoThuCong) return 'nghi';
  const td = kq.thongDiep.toLowerCase();
  if (td.includes('chưa đăng ký')) return 'dk';
  if (kq.loai === 'VAO' && (td.includes('gói') || td.includes('chưa có chỗ'))) return 'goi';
  return 'loi';
}

const NHAN: Record<Loai, string> = {
  ok: 'Đã cho vào', ra: 'Đã cho ra', cho: 'Chờ xác nhận app', thanhtoan: 'Chờ trả phí',
  goi: 'Chưa mua gói', dk: 'Chưa đăng ký', nghi: 'Chờ quản lý', loi: 'Từ chối',
};

const gioPhut = (d: Date | string) => new Date(d).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
/** "2 giờ 15 phút" / "3 ngày 4 giờ": thời gian đỗ hiện ở cổng ra. */
export function thoiGianDo(tu: string, den = Date.now()): string {
  const phut = Math.max(0, Math.floor((den - new Date(tu).getTime()) / 60_000));
  const ngay = Math.floor(phut / 1440), gio = Math.floor((phut % 1440) / 60), p = phut % 60;
  if (ngay > 0) return `${ngay} ngày ${gio} giờ`;
  if (gio > 0) return `${gio} giờ ${p} phút`;
  return `${p} phút`;
}

/**
 * Màn hình camera ở cổng (module Hành trình xe). Tự chụp khung hình mỗi giây, AI (Model A + PaddleOCR) đọc biển.
 * Cổng vào: có gói thì mở lượt gửi + ảnh đối chứng, barie mở, chỉ khu. Cổng ra: tìm lượt gửi, đối chiếu ảnh vào / ảnh ra,
 * thời gian đỗ, chờ người dùng xác nhận trên app (60 giây) rồi barie mở.
 */
export function CameraCongPage({ cong }: { cong: Cong }) {
  const { services } = useApp();
  const video = useRef<HTMLVideoElement>(null);
  const canvas = useRef<HTMLCanvasElement>(null);
  const oFile = useRef<HTMLInputElement>(null);

  const [camera, setCamera] = useState<{ id: string; ten: string }[]>([]);
  const [nguon, setNguon] = useState<INguonHinh | null>(null);
  const [tiLe, setTiLe] = useState(16 / 9);
  const [dangQuet, setDangQuet] = useState(false);
  const [hien, setHien] = useState<HienThi | null>(null);
  const [chiDan, setChiDan] = useState<ChiDan | null>(null);
  const [vuaQuet, setVuaQuet] = useState<DongQuet[]>([]);
  const [dem, setDem] = useState({ qua: 0, tuChoi: 0 });
  const [goTay, setGoTay] = useState('');
  const [loi, setLoi] = useState<string | null>(null);
  const [msAi, setMsAi] = useState<number | null>(null);
  const [gio, setGio] = useState(new Date());
  const dangGui = useRef(false);
  const daXuLy = useRef(new Map<string, number>());

  useEffect(() => { const t = setInterval(() => setGio(new Date()), 1000); return () => clearInterval(t); }, []);
  useEffect(() => { void lietKeCamera().then(setCamera).catch(() => setCamera([])); }, []);
  // Đổi cổng (vào ↔ ra) thì bắt đầu lại từ đầu
  useEffect(() => { setHien(null); setChiDan(null); setVuaQuet([]); setDem({ qua: 0, tuChoi: 0 }); daXuLy.current.clear(); }, [cong]);

  // Đổi nguồn: đóng nguồn cũ, mở nguồn mới, tự bắt đầu quét
  useEffect(() => {
    const v = video.current;
    if (!nguon || !v) return;
    let huy = false;
    setLoi(null);
    nguon.mo(v)
      .then(() => { if (!huy) { setDangQuet(true); void lietKeCamera().then(setCamera); } })
      .catch((e: Error) => { if (!huy) { setLoi(e.name === 'NotAllowedError' ? 'Chưa cho phép dùng camera: bấm biểu tượng camera trên thanh địa chỉ để cho phép.' : e.message); setDangQuet(false); } });
    return () => { huy = true; nguon.dong(v); setDangQuet(false); };
  }, [nguon]);

  const ghiDong = useCallback((bienSo: string, loai: Loai) => {
    setVuaQuet((ds) => [{ luc: new Date(), bienSo, loai, nhan: NHAN[loai] }, ...ds].slice(0, SO_DONG_VUA_QUET));
    if (loai === 'ok' || loai === 'ra') setDem((d) => ({ ...d, qua: d.qua + 1 }));
    else if (loai !== 'nghi' && loai !== 'cho' && loai !== 'thanhtoan') setDem((d) => ({ ...d, tuChoi: d.tuChoi + 1 }));
  }, []);

  const hienKetQua = useCallback(async (kq: KetQuaCong, anh: File | null) => {
    const loai = phanLoai(kq);
    setHien((cu) => { if (cu?.anhRa) URL.revokeObjectURL(cu.anhRa); return { kq, loai, thongDiep: kq.thongDiep, anhRa: anh && cong === 'ra' ? URL.createObjectURL(anh) : null, nhanLuc: Date.now() }; });
    setChiDan(null);
    ghiDong(kq.bienSo ?? kq.ungVien?.[0] ?? '—', loai);
    // Chờ quản lý chọn biển / chờ người dùng xác nhận: barie đóng, xe chưa đi được nên tạm dừng quét
    if (loai === 'nghi' || loai === 'cho' || loai === 'thanhtoan') setDangQuet(false);
    if (kq.loai === 'VAO' && kq.choQua && kq.luotGuiId) setChiDan(await services.timXe.layChiDan(kq.luotGuiId).catch(() => null));
  }, [services, cong, ghiDong]);

  // Cổng ra đang chờ xác nhận trên app: hỏi trạng thái lượt mỗi 2 giây
  const luotCho = hien && (hien.loai === 'cho' || hien.loai === 'thanhtoan') ? hien.kq.luotGuiId : null;
  useEffect(() => {
    if (!luotCho) return;
    const t = setInterval(async () => {
      const l: LuotCong | null = await services.congRaVao.layLuot(luotCho).catch(() => null);
      if (!l) return;
      const bien = hien?.kq.bienSo ?? '—';
      const doi = (loai: Loai, thongDiep: string) => {
        setHien((h) => (h ? { ...h, loai, thongDiep } : h));
        ghiDong(bien, loai);
        if (loai !== 'thanhtoan') setDangQuet(!!nguon);
      };
      if (l.trangThai === 'DA_RA') doi('ra', 'Người dùng xe đã xác nhận trên app');
      else if (l.trangThai === 'CHO_THANH_TOAN' && hien?.loai !== 'thanhtoan') doi('thanhtoan', 'Đã xác nhận nhưng còn phí quá hạn: trả trên app rồi quét lại');
      else if (l.trangThai === 'NGHI_MAT') doi('loi', 'Chủ xe báo KHÔNG PHẢI họ lấy xe: giữ xe lại, báo bảo vệ');
      else if (l.trangThai === 'DANG_GUI') doi('loi', 'Hết thời gian, không ai xác nhận: barie không mở. Quản lý kiểm tra rồi bấm "Mở barie thủ công".');
    }, 2_000);
    return () => clearInterval(t);
  }, [luotCho, services, hien?.kq.bienSo, hien?.loai, nguon, ghiDong]);

  // Vòng quét: mỗi giây chụp một khung, chỉ gửi khi lần trước đã xong
  useEffect(() => {
    if (!dangQuet) return;
    const t = setInterval(async () => {
      const v = video.current, c = canvas.current;
      if (dangGui.current || !v || !c || v.paused) return;
      dangGui.current = true;
      try {
        const anh = await chupKhung(v, c);
        if (!anh) return;
        const bd = performance.now();
        const kq = cong === 'vao'
          ? await services.congRaVao.xeVaoBangAnh(anh, undefined, true)
          : await services.congRaVao.xeRaBangAnh(anh, undefined, true);
        setMsAi(Math.round(performance.now() - bd));
        if (kq.khongThayBien || (!kq.bienSo && !kq.ungVien?.length && !kq.cheDoThuCong)) return;
        const khoa = kq.bienSo ?? kq.ungVien![0];
        const truoc = daXuLy.current.get(khoa);
        if (truoc && Date.now() - truoc < CHONG_LAP_MS) return; // xe còn đứng trước camera
        daXuLy.current.set(khoa, Date.now());
        await hienKetQua(kq, anh);
      } catch (e) { setLoi((e as Error).message); }
      finally { dangGui.current = false; }
    }, CHU_KY_MS);
    return () => clearInterval(t);
  }, [dangQuet, services, hienKetQua, cong]);

  /** Quản lý gõ tay hoặc bấm chọn biển AI đoán: gửi biển, xong thì quét tiếp. */
  async function guiTay(bienSo: string) {
    if (!bienSo.trim()) return;
    setLoi(null);
    try {
      const kq = cong === 'vao' ? await services.congRaVao.xeVao(bienSo.trim()) : await services.congRaVao.xeRa(bienSo.trim());
      if (kq.bienSo) daXuLy.current.set(kq.bienSo, Date.now());
      await hienKetQua(kq, null);
      setGoTay('');
      if (nguon && phanLoai(kq) !== 'cho') setDangQuet(true);
    } catch (e) { setLoi((e as Error).message); }
  }

  function chonNguon(gt: string) {
    if (gt === '__file') { oFile.current?.click(); return; }
    const cam = camera.find((c) => c.id === gt);
    setNguon(new NguonWebcam(gt || null, cam?.ten ?? 'Camera mặc định'));
  }

  const kb = hien?.kq.khungBien;
  // Mở từ thông báo Admin (quá 60 giây không ai xác nhận, người dùng báo "Không phải tôi"): ?luot=123
  const [thamSo] = useThamSoMotLan('luot');
  const [luotMo, setLuotMo] = useState<number | null>(null);
  useKhiMoTuThongBao(thamSo, true, (t) => setLuotMo(soId(t.luot)));

  const tenCong = cong === 'vao' ? 'Cổng vào' : 'Cổng ra';
  return (
    <div className="mc">
      <div className="mc-dau">
        <h1>Camera {tenCong.toLowerCase()}</h1>
        <span className="mc-goi-y">Tự quét mỗi 1 giây · chống đọc lặp 30 giây</span>
        <span className="mc-chip"><span className={`mc-cham ${dangQuet ? 'bat' : ''}`} />{nguon ? nguon.ten : 'Chưa chọn nguồn'}</span>
        <select className="mc-chon" value="" onChange={(e) => chonNguon(e.target.value)} aria-label="Chọn nguồn hình">
          <option value="" disabled>Nguồn ▾</option>
          {camera.length === 0 && <option value="">Camera mặc định</option>}
          {camera.map((c) => <option key={c.id} value={c.id}>{c.ten}</option>)}
          <option value="__file">File video…</option>
        </select>
        <input ref={oFile} type="file" accept="video/*" hidden onChange={(e) => { const f = e.target.files?.[0]; if (f) setNguon(new NguonFileVideo(f)); e.target.value = ''; }} />
        <button className="mc-chip" onClick={() => void (document.fullscreenElement ? document.exitFullscreen() : document.documentElement.requestFullscreen())}>Toàn màn hình ⛶</button>
      </div>

      {cong === 'ra' && luotMo !== null && <LuotCanXuLy luotGuiId={luotMo} dong={() => setLuotMo(null)} />}

      <div className="mc-khung">
        <section>
          <div className="mc-cam" style={{ aspectRatio: String(tiLe) }}>
            <video ref={video} playsInline onLoadedMetadata={(e) => { const v = e.currentTarget; if (v.videoWidth) setTiLe(v.videoWidth / v.videoHeight); }} />
            {!nguon && (
              <div className="mc-trong">
                <p>Chọn nguồn hình để bắt đầu quét</p>
                <div className="hang-nut">
                  <button className="nut" onClick={() => chonNguon(camera[0]?.id ?? '')}>Mở camera</button>
                  <button className="nut nut-phu" onClick={() => oFile.current?.click()}>Chọn file video</button>
                </div>
              </div>
            )}
            {kb && hien && (
              <div className={`mc-hop mc-${hien.loai}`} style={{ left: `${kb.x * 100}%`, top: `${kb.y * 100}%`, width: `${kb.rong * 100}%`, height: `${kb.cao * 100}%` }}>
                <span>{hienThiBienSo(hien.kq.bienSo ?? '?')}{hien.kq.doTinCay != null && ` · ${Math.round(hien.kq.doTinCay * 100)}%`}</span>
              </div>
            )}
            {nguon && (
              <div className="mc-tren">
                <span className="mc-nhan"><span className={`mc-rec ${dangQuet ? '' : 'dung'}`} />{dangQuet ? 'ĐANG QUÉT' : 'TẠM DỪNG'} · {gio.toLocaleTimeString('vi-VN')}</span>
                <span className="mc-nhan">Model A + PaddleOCR{msAi != null && ` · ${msAi} ms`}</span>
              </div>
            )}
          </div>
          <canvas ref={canvas} hidden />
          <form className="mc-duoi" onSubmit={(e) => { e.preventDefault(); void guiTay(goTay); }}>
            <input value={goTay} onChange={(e) => setGoTay(e.target.value)} placeholder="Gõ tay biển số nếu AI không đọc được, VD: 29B1-123.45" />
            <button className="nut" disabled={!goTay.trim()}>{cong === 'vao' ? 'Cho vào' : 'Xử lý xe ra'}</button>
            <button type="button" className="nut nut-phu" disabled={!nguon} onClick={() => setDangQuet((q) => !q)}>{dangQuet ? 'Tạm dừng quét' : 'Quét tiếp'}</button>
          </form>
          {loi && <p className="chu-loi mc-bao-loi">{loi}</p>}
        </section>

        <aside className="mc-ben">
          {hien ? <KhungKetQua h={hien} chiDan={chiDan} gio={gio} chon={guiTay}
                               boQua={() => { setHien(null); setDangQuet(!!nguon); }} />
            : <div className="mc-kq mc-cho-xe"><div className="mc-tt">Đang chờ xe</div><p className="mo">Xe dừng trước camera {tenCong.toLowerCase()}, biển số rõ trong khung hình. Kết quả hiện ở đây.</p></div>}
          <div className="mc-dem">
            <div><b>{dem.qua}</b><span>{cong === 'vao' ? 'Đã cho vào' : 'Đã cho ra'}</span></div>
            <div><b>{dem.tuChoi}</b><span>Từ chối</span></div>
            <div><b>{vuaQuet.filter((d) => d.loai === 'nghi' || d.loai === 'cho').length}</b><span>{cong === 'vao' ? 'Chờ quản lý' : 'Đã gửi xác nhận'}</span></div>
          </div>
          <div className="the">
            <h3>Vừa quét</h3>
            {vuaQuet.length === 0 && <p className="mo">Chưa có xe nào.</p>}
            {vuaQuet.map((d, i) => (
              <div key={i} className="mc-dong">
                <span className="mo">{gioPhut(d.luc)}</span>
                <code>{hienThiBienSo(d.bienSo)}</code>
                <span className={`mc-tag mc-${d.loai}`}>{d.nhan}</span>
              </div>
            ))}
          </div>
        </aside>
      </div>
    </div>
  );
}

const TIEU_DE: Record<Loai, { icon: string; tt: string }> = {
  ok: { icon: '✓', tt: 'Mời vào' }, ra: { icon: '✓', tt: 'Mời ra' }, cho: { icon: '…', tt: 'Đang chờ xác nhận trên app' },
  thanhtoan: { icon: '₫', tt: 'Chờ trả phí quá hạn' }, goi: { icon: '!', tt: 'Không cho vào' }, dk: { icon: '✕', tt: 'Không cho vào' },
  nghi: { icon: '?', tt: 'AI chưa chắc · chờ quản lý' }, loi: { icon: '!', tt: 'Barie không mở' },
};

function KhungKetQua({ h, chiDan, gio, chon, boQua }: {
  h: HienThi; chiDan: ChiDan | null; gio: Date; chon: (b: string) => void; boQua: () => void;
}) {
  const { kq, loai } = h;
  const t = TIEU_DE[loai];
  const tinCay = kq.doTinCay != null ? `AI đọc ${Math.round(kq.doTinCay * 100)}%` : 'Quản lý nhập tay';
  // Đếm ngược theo số giây server trả (không so giờ hai máy, tránh lệch đồng hồ / múi giờ)
  const conLai = kq.soGiayConLai != null ? Math.max(0, kq.soGiayConLai - Math.floor((gio.getTime() - h.nhanLuc) / 1000)) : null;
  return (
    <div className={`mc-kq mc-${loai}`} role="status" aria-live="assertive">
      <div className="mc-tt"><span className="mc-icon">{t.icon}</span>{t.tt}
        {(loai === 'ok' || loai === 'ra') && <span className="mc-barie">▲ Barie đang mở</span>}
      </div>
      {kq.bienSo && <div className="mc-bien">{hienThiBienSo(kq.bienSo)}</div>}
      <div className="mo">{tinCay}{kq.khopGanDung && ' · khớp gần đúng'}</div>

      {loai === 'ok' && (
        <>
          <div className="mc-chinh">Mời xe vào bãi</div>
          <div className="mc-phu">Đã mở lượt gửi lúc {kq.thoiGianVao ? gioPhut(kq.thoiGianVao) : gioPhut(gio)}{kq.coAnhVao && ', đã lưu ảnh đối chứng'}.</div>
          {chiDan && (
            <div className="mc-khu">
              <div className="mc-khu-chu">{chiDan.khu}</div>
              <div><b>Khu xe máy</b><div className="mo">Giữ chỗ tới {gioPhut(chiDan.hetHanLuc)}</div></div>
              <div className="mc-mui">➜</div>
            </div>
          )}
        </>
      )}
      {kq.loai === 'RA' && kq.thoiGianVao && (
        <>
          {loai === 'ra' && <div className="mc-chinh">Mời xe ra</div>}
          {loai === 'cho' && <div className="mc-chinh mc-nho">Đã gửi yêu cầu tới app của chủ xe{conLai != null && ` · còn ${conLai} giây`}</div>}
          <div className="mc-phu">Vào lúc {new Date(kq.thoiGianVao).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit' })} · đã gửi <b>{thoiGianDo(kq.thoiGianVao, gio.getTime())}</b></div>
          <DoiChung luotGuiId={kq.luotGuiId} coAnhVao={!!kq.coAnhVao} anhRa={h.anhRa} />
        </>
      )}
      {(loai === 'thanhtoan' || (loai === 'loi' && kq.loai === 'RA')) && <div className="mc-phu mc-nhan-manh">{h.thongDiep}</div>}
      {loai === 'loi' && kq.loai === 'RA' && kq.luotGuiId && <MoBarieTay luotGuiId={kq.luotGuiId} />}
      {loai === 'goi' && (
        <>
          <div className="mc-chinh">Xe chưa mua gói gửi xe</div>
          <div className="mc-phu">{kq.thongDiep}. Vui lòng mở app <b>Nhà xe thông minh</b> → <b>Gói chỗ</b> để mua gói tháng hoặc năm, sau đó quay lại cổng.</div>
        </>
      )}
      {loai === 'dk' && (
        <>
          <div className="mc-chinh">Xe chưa đăng ký</div>
          <div className="mc-phu">Biển số này chưa có trong hệ thống. Chủ xe cần tải app, đăng ký tài khoản, thêm xe và mua gói trước khi gửi.</div>
        </>
      )}
      {loai === 'loi' && kq.loai === 'VAO' && <div className="mc-chinh mc-nho">{kq.thongDiep}</div>}
      {loai === 'loi' && kq.loai === 'RA' && !kq.thoiGianVao && <div className="mc-chinh mc-nho">{kq.thongDiep}</div>}
      {loai === 'nghi' && (
        <>
          <div className="mc-phu">{kq.cheDoThuCong ? kq.thongDiep : 'Chọn biển đúng hoặc gõ tay ở ô dưới camera:'}</div>
          <div className="mc-ung">
            {kq.ungVien?.map((b, i) => <button key={b} className={i === 0 ? 'chon' : ''} onClick={() => chon(b)}>{hienThiBienSo(b)}</button>)}
            <button className="bo" onClick={boQua}>Bỏ qua</button>
          </div>
        </>
      )}
    </div>
  );
}

/** Mở barie thủ công (UC-A10) ngay tại màn hình cổng ra: hết 60 giây không ai xác nhận, nghi mất đã kiểm tra, app / AI lỗi. Bắt buộc lý do. */
/**
 * Khung "Lượt cần xử lý" (mở từ thông báo): biển số, lý do, ảnh đối chứng, nút mở barie tay.
 * Lượt đã kết thúc (xe đã ra) thì chỉ hiện trạng thái, không có nút.
 */
function LuotCanXuLy({ luotGuiId, dong }: { luotGuiId: number; dong: () => void }) {
  const { services } = useApp();
  const [luot, setLuot] = useState<LuotCong | null>(null);
  const [tt, setTt] = useState<{ bienSo: string | null; lyDo: string | null }>({ bienSo: null, lyDo: null });
  const [anhRa, setAnhRa] = useState<string | null>(null);
  const [loi, setLoi] = useState<string | null>(null);

  useEffect(() => {
    let huy = false, url: string | null = null;
    setLoi(null); setLuot(null); setAnhRa(null);
    Promise.all([services.congRaVao.layLuot(luotGuiId), services.congRaVao.layManHinhCong().catch(() => null)])
      .then(([l, mh]) => {
        if (huy) return;
        const c = mh?.canXuLy?.find((x) => x.luotGuiId === luotGuiId);
        setLuot(l);
        setTt({ bienSo: c?.bienSo ?? mh?.xe.find((x) => x.xeId === l.xeId)?.bienSo ?? null, lyDo: c?.lyDo ?? null });
        requestAnimationFrame(() => toSang('luot-can-xu-ly'));
        if (l.coAnhRa) services.congRaVao.layAnhDoiChung(luotGuiId, 'ra')
          .then((b) => { if (!huy) { url = URL.createObjectURL(b); setAnhRa(url); } }).catch(() => { /* không có ảnh ra */ });
      })
      .catch((e) => { if (!huy) setLoi((e as Error).message); });
    return () => { huy = true; if (url) URL.revokeObjectURL(url); };
  }, [luotGuiId, services]);

  const xong = luot?.trangThai === 'DA_RA';
  return (
    <section className="the mc-xu-ly" data-dich="luot-can-xu-ly" aria-label="Lượt cần xử lý">
      <div className="xe-dau">
        <div>
          <div className="mo">Lượt cần xử lý · mở từ thông báo</div>
          <div className="bien-so">{tt.bienSo ? hienThiBienSo(tt.bienSo) : `Lượt #${luotGuiId}`}</div>
        </div>
        <span className="hang-nut" style={{ marginTop: 0 }}>
          {luot && <span className={`nhan ${xong ? 'nhan-ok' : luot.trangThai === 'NGHI_MAT' ? 'nhan-loi' : 'nhan-cho'}`}>{TEN_TRANG_THAI_LUOT[luot.trangThai]}</span>}
          <button type="button" className="nut nut-phu nut-nho" onClick={dong}>Đóng</button>
        </span>
      </div>
      {loi && <ThongBao loai="loi">Không mở được lượt này: {loi}</ThongBao>}
      {luot && (
        <>
          {tt.lyDo && <p>{tt.lyDo}</p>}
          <p className="mo">Vào bãi lúc {dinhDangNgay(luot.thoiGianVao)}</p>
          {xong
            ? <ThongBao loai="tin">Lượt này đã kết thúc: xe đã ra lúc {dinhDangNgay(luot.thoiGianRa)}. Không cần xử lý nữa.</ThongBao>
            : <><DoiChung luotGuiId={luotGuiId} coAnhVao={luot.coAnhVao} anhRa={anhRa} /><MoBarieTay luotGuiId={luotGuiId} /></>}
        </>
      )}
    </section>
  );
}

function MoBarieTay({ luotGuiId }: { luotGuiId: number }) {
  const { services } = useApp();
  const [lyDo, setLyDo] = useState('');
  const [tin, setTin] = useState<{ ok: boolean; noiDung: string } | null>(null);
  const [dangGui, setDangGui] = useState(false);
  if (tin?.ok) return <div className="mc-phu"><b>{tin.noiDung}</b></div>;
  return (
    <form className="mc-duoi" onSubmit={async (e) => {
      e.preventDefault();
      setDangGui(true);
      try { const kq = await services.congRaVao.moTay(luotGuiId, lyDo.trim()); setTin({ ok: true, noiDung: kq.thongDiep || 'Đã mở barie thủ công.' }); }
      catch (er) { setTin({ ok: false, noiDung: (er as Error).message }); }
      finally { setDangGui(false); }
    }}>
      <input value={lyDo} onChange={(e) => setLyDo(e.target.value)} placeholder="Lý do mở tay, VD: đã kiểm tra giấy tờ" required />
      <button className="nut" disabled={dangGui || !lyDo.trim()}>Mở barie thủ công</button>
      {tin && !tin.ok && <span className="chu-loi">{tin.noiDung}</span>}
    </form>
  );
}

/** Ảnh đối chứng ở cổng ra: ảnh chụp lúc vào (từ server) và ảnh vừa chụp lúc ra, đặt cạnh nhau để quản lý đối chiếu. */
function DoiChung({ luotGuiId, coAnhVao, anhRa }: { luotGuiId: number | null; coAnhVao: boolean; anhRa: string | null }) {
  const { services } = useApp();
  const [anhVao, setAnhVao] = useState<string | null>(null);
  useEffect(() => {
    if (!luotGuiId || !coAnhVao) { setAnhVao(null); return; }
    let url: string | null = null, huy = false;
    services.congRaVao.layAnhDoiChung(luotGuiId, 'vao')
      .then((b) => { if (!huy) { url = URL.createObjectURL(b); setAnhVao(url); } })
      .catch(() => setAnhVao(null));
    return () => { huy = true; if (url) URL.revokeObjectURL(url); };
  }, [luotGuiId, coAnhVao, services]);
  return (
    <div className="mc-doi-chung">
      <figure>{anhVao ? <img src={anhVao} alt="Ảnh lúc vào" /> : <div className="mc-khong-anh">Không có ảnh lúc vào</div>}<figcaption>Lúc vào</figcaption></figure>
      <figure>{anhRa ? <img src={anhRa} alt="Ảnh lúc ra" /> : <div className="mc-khong-anh">Nhập tay, không có ảnh</div>}<figcaption>Lúc ra</figcaption></figure>
    </div>
  );
}
