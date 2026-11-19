import { useMemo, useState } from 'react';
import { soId, toSang, useKhiMoTuThongBao, useThamSoMotLan } from '@/shared/ui/useDichDen';
import { DangTai, ThongBao } from '@/shared/ui/ui';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';
import { useApp } from '@/app/AppContext';
import { dinhDangNgay, hienThiBienSo, tenLoaiXe } from '@/shared/utils/format';
import type { CameraBai, CauHinhBai, KhuQuanLy, OQuanLy, TrangThaiO, VaiTroCamera } from '../models';
import { TEN_VAI_TRO_CAMERA } from '../models';
import { TEN_TRANG_THAI_O } from '../models';
import { khuXeMayTrenSoDo, MucChoKhu } from '@/modules/tim-xe';
import type { KhuCho } from '@/modules/tim-xe/models';

type Tab = 'khu' | 'o' | 'camera';

/**
 * Trang Admin "Quản lý bãi": thêm / sửa / xoá khu, ô, camera và đặt tay trạng thái ô.
 * Mọi quy tắc (tên trùng, mã ô sai, camera khác khu, xoá thứ đang dùng) do backend kiểm; trang chỉ hiện lỗi trả về.
 */
export function AdminQuanLyBaiPage() {
  const { services } = useApp();
  const ch = useTaiDuLieu(() => services.baiXe.layCauHinh());
  // Số xe trong từng khu: camera khu hướng vào +1, hướng ra −1. Tự tải lại mỗi 5 giây.
  const soXe = useTaiDuLieu(() => services.timXe.laySoXeKhu(), [], 5_000);
  const [tab, setTab] = useState<Tab>('khu');
  // Mở từ thông báo đối soát: ?diSai=<id> (tô sáng lần đi sai khu) hoặc ?muc=so-xe|di-sai
  const [thamSo] = useThamSoMotLan('diSai', 'muc');
  const diSaiMo = soId(thamSo.diSai);
  useKhiMoTuThongBao(thamSo, !!soXe.duLieu, (t) => {
    if (t.muc === 'so-xe') requestAnimationFrame(() => toSang('so-xe-khu'));
    else if (t.muc === 'di-sai' && !t.diSai) requestAnimationFrame(() => toSang('di-sai-khu'));
  });
  const [tin, setTin] = useState<{ loai: 'ok' | 'loi'; noiDung: string } | null>(null);
  // Mặc định chỉ hiện khu đang dùng (cùng ô đỗ, camera của chúng). Bật ô này để thấy khu tạm tắt (VD Khu A, B ô tô) và bật lại.
  const [hienTat, setHienTat] = useState(false);
  const soKhuTat = ch.duLieu?.khu.filter((k) => !k.dangDung).length ?? 0;
  const chHien = useMemo(() => (ch.duLieu ? locKhuDangDung(ch.duLieu, hienTat) : null), [ch.duLieu, hienTat]);

  /** Chạy một thao tác ghi: báo lỗi nếu có, xong thì tải lại cấu hình. */
  async function lam(viec: () => Promise<unknown>, thanhCong: string) {
    setTin(null);
    try { await viec(); setTin({ loai: 'ok', noiDung: thanhCong }); await ch.taiLai(); return true; }
    catch (e) { setTin({ loai: 'loi', noiDung: (e as Error).message }); return false; }
  }

  return (
    <>
      <div className="tieu-de-trang">
        <h1>Quản lý bãi</h1>
        <button className="nut nut-phu nut-nho" onClick={() => { void ch.taiLai(); void soXe.taiLai(); }} disabled={ch.dangTai}>Tải lại</button>
      </div>
      <h2 data-dich="so-xe-khu">Số xe trong các khu</h2>
      <p className="mo">Camera khu hướng vào nhận diện xe → cộng 1 vào khu; camera hướng ra nhận diện xe → trừ 1. Tự cập nhật mỗi 5 giây.</p>
      {soXe.loi && <ThongBao loai="loi">{soXe.loi}</ThongBao>}
      {/* Chỉ 3 khu xe máy trên bản đồ (R, S, Hầm). Khu ô tô và khu mẫu C, D không hiện (dữ liệu DB vẫn giữ). */}
      {soXe.duLieu && <MucChoKhu khu={khuXeMayTrenSoDo(soXe.duLieu.khu)} chiTiet />}
      <DiSaiKhuBang moId={diSaiMo} moThamSo={thamSo} />
      <h2>Cấu hình bãi</h2>
      <p className="mo">Khai báo khu, ô đỗ và camera. Mỗi khu nên có 2 camera: một camera soi hướng vào, một camera soi hướng ra.</p>
      <div className="tab">
        <button className={tab === 'khu' ? 'chon' : ''} onClick={() => setTab('khu')}>Khu</button>
        <button className={tab === 'o' ? 'chon' : ''} onClick={() => setTab('o')}>Ô đỗ</button>
        <button className={tab === 'camera' ? 'chon' : ''} onClick={() => setTab('camera')}>Camera</button>
      </div>
      {soKhuTat > 0 && (
        <label className="hang-chon hien-khu-tat">
          <input type="checkbox" checked={hienTat} onChange={(e) => setHienTat(e.target.checked)} />
          Hiện khu tạm tắt ({soKhuTat} khu) để bật lại
        </label>
      )}
      {tin && <ThongBao loai={tin.loai}>{tin.noiDung}</ThongBao>}
      {ch.loi && <ThongBao loai="loi">{ch.loi}</ThongBao>}
      {ch.dangTai && !ch.duLieu && <DangTai />}
      {chHien && tab === 'khu' && <QuanLyKhu ch={chHien} lam={lam} soXe={soXe.duLieu?.khu ?? []} />}
      {chHien && tab === 'o' && <QuanLyO ch={chHien} lam={lam} />}
      {chHien && tab === 'camera' && <QuanLyCamera ch={chHien} lam={lam} />}
    </>
  );
}

/** Chỉ giữ khu đang dùng cùng ô đỗ và camera của chúng (hienTat = true thì giữ tất cả). Khu đang dùng xếp trước. */
function locKhuDangDung(ch: CauHinhBai, hienTat: boolean): CauHinhBai {
  const khu = (hienTat ? ch.khu : ch.khu.filter((k) => k.dangDung))
    .slice().sort((a, b) => Number(b.dangDung) - Number(a.dangDung));
  if (hienTat) return { ...ch, khu };
  const id = new Set(khu.map((k) => k.id));
  return { ...ch, khu, o: ch.o.filter((o) => id.has(o.khuId)), camera: ch.camera.filter((c) => id.has(c.khuId)) };
}

type Lam = (viec: () => Promise<unknown>, thanhCong: string) => Promise<boolean>;
const tenKhu = (ch: CauHinhBai, id: number) => ch.khu.find((k) => k.id === id)?.ten ?? `#${id}`;
/** Khu xe máy tối đa 80 xe (backend cũng kiểm tra). Khu ô tô không giới hạn ở đây. */
const SUC_CHUA_TOI_DA_XE_MAY = 80;
const toiDa = (ch: CauHinhBai, loaiXeId: number) =>
  ch.loaiXe.find((l) => l.id === loaiXeId)?.ten === 'XE_MAY' ? SUC_CHUA_TOI_DA_XE_MAY : undefined;

// ---------------- Đi sai khu ----------------

const gioNgay = (iso: string) => new Date(iso).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit' });

/** Cảnh báo đi sai khu (xe không theo chỉ dẫn) và ai hay đi sai. Chỉ để Admin theo dõi, chưa phạt tự động. Tự tải lại mỗi 15 giây. */
function DiSaiKhuBang({ moId, moThamSo }: { moId: number | null; moThamSo: object }) {
  const { services } = useApp();
  const ds = useTaiDuLieu(() => services.timXe.layDiSaiKhu(), [], 15_000);
  const d = ds.duLieu;
  const day = d?.ganDay.filter((x) => x.khuDay).length ?? 0;
  const [mo, setMo] = useState(false);
  const [tinMo, setTinMo] = useState<string | null>(null);
  useKhiMoTuThongBao(moThamSo, !!d, () => {
    if (moId === null) return;
    setMo(true); setTinMo(null);
    if (!d?.ganDay.some((x) => x.id === moId)) { setTinMo('Lần đi sai khu này đã cũ, không còn trong danh sách gần đây.'); return; }
    requestAnimationFrame(() => toSang(`di-sai-${moId}`));
  });
  return (
    <details className="the di-sai-khu" open={day > 0 || mo} data-dich="di-sai-khu"
             onToggle={(e) => setMo((e.target as HTMLDetailsElement).open)}>
      <summary>
        <strong>Đi sai khu</strong>
        {d && <span className="mo"> · {d.ganDay.length} lần gần đây</span>}
        {day > 0 && <span className="nhan nhan-loi">{day} lần tới khu đã đầy</span>}
      </summary>
      {tinMo && <ThongBao loai="tin">{tinMo}</ThongBao>}
      {ds.loi && <ThongBao loai="loi">{ds.loi}</ThongBao>}
      {d && d.ganDay.length === 0 && <p className="mo">Chưa có xe nào đi sai khu.</p>}
      {d && d.ganDay.length > 0 && (
        <div className="luoi-di-sai">
          <div className="bang-cuon">
            <table className="bang bang-gon">
              <thead><tr><th>Thời gian</th><th>Biển số</th><th>Chủ xe</th><th>Khu được chỉ</th><th>Khu thực tế</th><th></th></tr></thead>
              <tbody>
                {d.ganDay.map((x) => (
                  <tr key={x.id} data-dich={`di-sai-${x.id}`}>
                    <td>{gioNgay(x.luc)}</td><td className="bien-so nho">{hienThiBienSo(x.bienSo)}</td><td>{x.tenChuXe}</td>
                    <td>{x.khuChiDinh ?? <span className="mo">không có</span>}</td><td>{x.khuThucTe}</td>
                    <td>{x.khuDay ? <span className="nhan nhan-loi">Khu đầy, bị chặn</span> : <span className="nhan">Đỗ khu khác</span>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="bang-cuon">
            <table className="bang bang-gon">
              <thead><tr><th>Ai hay đi sai</th><th>Số lần</th><th>Tới khu đầy</th><th>Lần cuối</th></tr></thead>
              <tbody>
                {d.theoChuXe.map((x) => (
                  <tr key={x.chuXeId}><td>{x.tenChuXe}<div className="mo">{x.sdt}</div></td><td><strong>{x.soLan}</strong></td><td>{x.soLanKhuDay}</td><td>{gioNgay(x.lanCuoi)}</td></tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </details>
  );
}

// ---------------- Khu ----------------

function QuanLyKhu({ ch, lam, soXe }: { ch: CauHinhBai; lam: Lam; soXe: KhuCho[] }) {
  const { services } = useApp();
  const dangCo = (id: number) => soXe.find((k) => k.id === id);
  const [moi, setMoi] = useState({ ten: '', loaiXeId: ch.loaiXe[0]?.id ?? 1, sucChua: 0 });
  const [sua, setSua] = useState<KhuQuanLy | null>(null);

  return (
    <>
      <form className="the hang-form" onSubmit={async (e) => {
        e.preventDefault();
        if (await lam(() => services.baiXe.themKhu(moi), `Đã thêm khu ${moi.ten}.`)) setMoi({ ...moi, ten: '' });
      }}>
        <input value={moi.ten} onChange={(e) => setMoi({ ...moi, ten: e.target.value })} placeholder="Tên khu, VD: Khu D" required />
        <ChonLoaiXe ch={ch} value={moi.loaiXeId} onChange={(v) => setMoi({ ...moi, loaiXeId: v })} />
        <input type="number" min={0} max={toiDa(ch, moi.loaiXeId)} value={moi.sucChua} onChange={(e) => setMoi({ ...moi, sucChua: Number(e.target.value) })}
               aria-label="Sức chứa" placeholder="Sức chứa"
               title={toiDa(ch, moi.loaiXeId) ? `Sức chứa, khu xe máy tối đa ${SUC_CHUA_TOI_DA_XE_MAY} xe` : 'Sức chứa (số xe tối đa của khu)'} />
        <button className="nut">Thêm khu</button>
      </form>
      <div className="bang-cuon">
        <table className="bang">
          <thead><tr><th>Tên khu</th><th>Loại xe</th><th>Sức chứa</th><th>Đang có xe</th><th>Còn trống</th><th>Số ô</th><th>Số camera</th><th>Trạng thái</th><th></th></tr></thead>
          <tbody>
            {ch.khu.map((k) => sua?.id === k.id ? (
              <tr key={k.id}>
                <td><input value={sua.ten} onChange={(e) => setSua({ ...sua, ten: e.target.value })} /></td>
                <td><ChonLoaiXe ch={ch} value={sua.loaiXeId} onChange={(v) => setSua({ ...sua, loaiXeId: v })} /></td>
                <td><input type="number" min={0} max={toiDa(ch, sua.loaiXeId)} value={sua.sucChua} onChange={(e) => setSua({ ...sua, sucChua: Number(e.target.value) })} aria-label="Sức chứa" /></td>
                <td>{dangCo(k.id)?.soXe ?? '—'}</td><td>{dangCo(k.id)?.conLai ?? '—'}</td>
                <td>{k.soO}</td><td>{k.soCamera}</td>
                <td><label className="hang-chon"><input type="checkbox" checked={sua.dangDung} onChange={(e) => setSua({ ...sua, dangDung: e.target.checked })} /> Đang dùng</label></td>
                <td className="hang-nut">
                  <button className="nut nut-nho" onClick={async () => {
                    if (await lam(() => services.baiXe.suaKhu(k.id, { ten: sua.ten, loaiXeId: sua.loaiXeId, sucChua: sua.sucChua, dangDung: sua.dangDung }), 'Đã lưu khu.')) setSua(null);
                  }}>Lưu</button>
                  <button className="nut nut-phu nut-nho" onClick={() => setSua(null)}>Huỷ</button>
                </td>
              </tr>
            ) : (
              <tr key={k.id}>
                <td><strong>{k.ten}</strong>{k.maSoDo && <span className="nhan nhan-ma-so-do" title="Mã chấm trên ảnh sơ đồ">{k.maSoDo === 'HAM' ? 'Hầm' : k.maSoDo}</span>}</td>
                <td>{tenLoaiXe(ch.loaiXe.find((l) => l.id === k.loaiXeId)?.ten ?? k.loaiXeId)}</td>
                <td>{k.sucChua}</td>
                <td><strong>{dangCo(k.id)?.soXe ?? '—'}</strong></td><td>{dangCo(k.id)?.conLai ?? '—'}</td>
                <td>{k.soO}</td><td>{k.soCamera}</td>
                <td>{k.dangDung ? <span className="nhan nhan-ok">Đang dùng</span> : <span className="nhan nhan-loi" title="Không chỉ dẫn xe vào, người dùng không thấy">Tạm tắt</span>}</td>
                <td className="hang-nut">
                  <button className="nut nut-phu nut-nho" onClick={() => setSua(k)}>Sửa</button>
                  <button className="nut nut-nguy nut-nho" onClick={() => confirm(`Xoá ${k.ten}?`) && lam(() => services.baiXe.xoaKhu(k.id), `Đã xoá ${k.ten}.`)}>Xoá</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function ChonLoaiXe({ ch, value, onChange }: { ch: CauHinhBai; value: number; onChange: (v: number) => void }) {
  return (
    <select value={value} onChange={(e) => onChange(Number(e.target.value))}>
      {ch.loaiXe.map((l) => <option key={l.id} value={l.id}>{tenLoaiXe(l.ten)}</option>)}
    </select>
  );
}

// ---------------- Ô đỗ ----------------

function QuanLyO({ ch, lam }: { ch: CauHinhBai; lam: Lam }) {
  const { services } = useApp();
  const [locKhu, setLocKhu] = useState<number | 0>(0);
  const [moi, setMoi] = useState({ khuId: ch.khu[0]?.id ?? 0, maO: '', cameraId: null as number | null });
  const ds = ch.o.filter((o) => !locKhu || o.khuId === locKhu);
  const cameraCuaKhu = (khuId: number) => ch.camera.filter((c) => c.khuId === khuId);

  return (
    <>
      <form className="the hang-form" onSubmit={async (e) => {
        e.preventDefault();
        if (await lam(() => services.baiXe.themO(moi), `Đã thêm ô ${moi.maO.toUpperCase()}.`)) setMoi({ ...moi, maO: '' });
      }}>
        <select value={moi.khuId} onChange={(e) => setMoi({ ...moi, khuId: Number(e.target.value), cameraId: null })}>
          {ch.khu.map((k) => <option key={k.id} value={k.id}>{k.ten}</option>)}
        </select>
        <input value={moi.maO} onChange={(e) => setMoi({ ...moi, maO: e.target.value })} placeholder="Mã ô, VD: A05" required />
        <ChonCamera ds={cameraCuaKhu(moi.khuId)} value={moi.cameraId} onChange={(v) => setMoi({ ...moi, cameraId: v })} />
        <button className="nut">Thêm ô</button>
      </form>
      <div className="thanh-chon">
        <span className="mo">Lọc theo khu</span>
        <select value={locKhu} onChange={(e) => setLocKhu(Number(e.target.value))}>
          <option value={0}>Tất cả</option>
          {ch.khu.map((k) => <option key={k.id} value={k.id}>{k.ten}</option>)}
        </select>
      </div>
      {ds.length === 0 ? <ThongBao loai="tin">Chưa có ô nào{locKhu ? ' trong khu này' : ''}. Khu không chia ô (VD xe máy) thì không cần thêm.</ThongBao> : (
        <div className="bang-cuon">
          <table className="bang">
            <thead><tr><th>Mã ô</th><th>Khu</th><th>Mã QR</th><th>Camera</th><th>Trạng thái</th><th>Cập nhật</th><th></th></tr></thead>
            <tbody>{ds.map((o) => <DongO key={o.id} o={o} ch={ch} cameras={cameraCuaKhu(o.khuId)} lam={lam} />)}</tbody>
          </table>
        </div>
      )}
    </>
  );
}

function DongO({ o, ch, cameras, lam }: { o: OQuanLy; ch: CauHinhBai; cameras: CameraBai[]; lam: Lam }) {
  const { services } = useApp();
  const [sua, setSua] = useState<{ maO: string; cameraId: number | null } | null>(null);
  return (
    <tr>
      <td>{sua ? <input value={sua.maO} onChange={(e) => setSua({ ...sua, maO: e.target.value })} size={6} /> : <strong>{o.maO}</strong>}</td>
      <td>{tenKhu(ch, o.khuId)}</td>
      <td className="mo">{o.maQr}</td>
      <td>{sua ? <ChonCamera ds={cameras} value={sua.cameraId} onChange={(v) => setSua({ ...sua, cameraId: v })} />
               : ch.camera.find((c) => c.id === o.cameraId)?.ten ?? '—'}</td>
      <td>
        <select className={`chon-tt tt-o-${o.trangThai}`} value={o.trangThai}
                onChange={(e) => lam(() => services.baiXe.datTrangThaiO(o.id, e.target.value as TrangThaiO), `Ô ${o.maO}: ${TEN_TRANG_THAI_O[e.target.value as TrangThaiO]}.`)}>
          {(Object.keys(TEN_TRANG_THAI_O) as TrangThaiO[]).map((t) => <option key={t} value={t}>{TEN_TRANG_THAI_O[t]}</option>)}
        </select>
      </td>
      <td className="mo">{o.capNhatLuc ? `${dinhDangNgay(o.capNhatLuc)} · ${o.nguon === 'AI' ? 'AI' : 'Admin'}` : '—'}</td>
      <td className="hang-nut">
        {sua ? (
          <>
            <button className="nut nut-nho" onClick={async () => {
              if (await lam(() => services.baiXe.suaO(o.id, { khuId: o.khuId, maO: sua.maO, cameraId: sua.cameraId }), 'Đã lưu ô.')) setSua(null);
            }}>Lưu</button>
            <button className="nut nut-phu nut-nho" onClick={() => setSua(null)}>Huỷ</button>
          </>
        ) : (
          <>
            <button className="nut nut-phu nut-nho" onClick={() => setSua({ maO: o.maO, cameraId: o.cameraId })}>Sửa</button>
            <button className="nut nut-nguy nut-nho" onClick={() => confirm(`Xoá ô ${o.maO}?`) && lam(() => services.baiXe.xoaO(o.id), `Đã xoá ô ${o.maO}.`)}>Xoá</button>
          </>
        )}
      </td>
    </tr>
  );
}

function ChonCamera({ ds, value, onChange }: { ds: CameraBai[]; value: number | null; onChange: (v: number | null) => void }) {
  return (
    <select value={value ?? ''} onChange={(e) => onChange(e.target.value ? Number(e.target.value) : null)}>
      <option value="">Không gắn camera</option>
      {ds.map((c) => <option key={c.id} value={c.id}>{c.ten}</option>)}
    </select>
  );
}

// ---------------- Camera ----------------

function QuanLyCamera({ ch, lam }: { ch: CauHinhBai; lam: Lam }) {
  const { services } = useApp();
  const trong = { khuId: ch.khu[0]?.id ?? 0, ten: '', nguonAnh: '', dangBat: true, vaiTro: 'KHU_VAO' as VaiTroCamera };
  const [moi, setMoi] = useState(trong);
  const [sua, setSua] = useState<CameraBai | null>(null);

  return (
    <>
      <form className="the hang-form" onSubmit={async (e) => {
        e.preventDefault();
        if (await lam(() => services.baiXe.themCamera(moi), `Đã thêm camera ${moi.ten}.`)) setMoi({ ...trong, khuId: moi.khuId });
      }}>
        <select value={moi.khuId} onChange={(e) => setMoi({ ...moi, khuId: Number(e.target.value) })}>
          {ch.khu.map((k) => <option key={k.id} value={k.id}>{k.ten}</option>)}
        </select>
        <input value={moi.ten} onChange={(e) => setMoi({ ...moi, ten: e.target.value })} placeholder="Tên camera" required />
        <input value={moi.nguonAnh} onChange={(e) => setMoi({ ...moi, nguonAnh: e.target.value })} placeholder="rtsp://… hoặc /anh-mau/cam.jpg" required />
        <ChonVaiTro value={moi.vaiTro} onChange={(v) => setMoi({ ...moi, vaiTro: v })} />
        <button className="nut">Thêm camera</button>
      </form>
      <div className="bang-cuon">
        <table className="bang">
          <thead><tr><th>Tên</th><th>Khu</th><th>Vai trò</th><th>Nguồn ảnh</th><th>Đang bật</th><th></th></tr></thead>
          <tbody>
            {ch.camera.map((c) => sua?.id === c.id ? (
              <tr key={c.id}>
                <td><input value={sua.ten} onChange={(e) => setSua({ ...sua, ten: e.target.value })} /></td>
                <td>{tenKhu(ch, c.khuId)}</td>
                <td><ChonVaiTro value={sua.vaiTro} onChange={(v) => setSua({ ...sua, vaiTro: v })} /></td>
                <td><input value={sua.nguonAnh} onChange={(e) => setSua({ ...sua, nguonAnh: e.target.value })} /></td>
                <td><input type="checkbox" checked={sua.dangBat} onChange={(e) => setSua({ ...sua, dangBat: e.target.checked })} /></td>
                <td className="hang-nut">
                  <button className="nut nut-nho" onClick={async () => {
                    if (await lam(() => services.baiXe.suaCamera(c.id, sua), 'Đã lưu camera.')) setSua(null);
                  }}>Lưu</button>
                  <button className="nut nut-phu nut-nho" onClick={() => setSua(null)}>Huỷ</button>
                </td>
              </tr>
            ) : (
              <tr key={c.id}>
                <td><strong>{c.ten}</strong></td>
                <td>{tenKhu(ch, c.khuId)}</td>
                <td>{TEN_VAI_TRO_CAMERA[c.vaiTro] ?? c.vaiTro}</td>
                <td className="mo">{c.nguonAnh}</td>
                <td>{c.dangBat ? <span className="nhan nhan-ok">Bật</span> : <span className="nhan nhan-cho">Tắt</span>}</td>
                <td className="hang-nut">
                  <button className="nut nut-phu nut-nho" onClick={() => setSua(c)}>Sửa</button>
                  <button className="nut nut-nguy nut-nho" onClick={() => confirm(`Xoá ${c.ten}?`) && lam(() => services.baiXe.xoaCamera(c.id), `Đã xoá ${c.ten}.`)}>Xoá</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function ChonVaiTro({ value, onChange }: { value: VaiTroCamera; onChange: (v: VaiTroCamera) => void }) {
  return (
    <select value={value} onChange={(e) => onChange(e.target.value as VaiTroCamera)} aria-label="Vai trò camera">
      {(Object.keys(TEN_VAI_TRO_CAMERA) as VaiTroCamera[]).map((v) => <option key={v} value={v}>{TEN_VAI_TRO_CAMERA[v]}</option>)}
    </select>
  );
}
