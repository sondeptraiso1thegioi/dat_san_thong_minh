import { useEffect, useState } from 'react';
import { AnhGiayDangKy } from '../components/AnhGiayDangKy';
import { KhungChiTietXe, NhanGoi } from '../components/KhungChiTietXe';
import { ChoConBanThe } from '../components/GoiChoUi';
import { DangTai, ThongBao } from '@/shared/ui/ui';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';
import { useTreHoan } from '@/shared/ui/useTreHoan';
import { useApp } from '@/app/AppContext';
import { soId, toSang, useKhiMoTuThongBao, useThamSoMotLan } from '@/shared/ui/useDichDen';
import type { LocXeDaDuyet, XeQuanLy } from '../models';
import { timChoCon } from '../utils/choCon';
import { chuanHoaBienSo, dinhDangNgay, hienThiBienSo, tenLoaiXe } from '@/shared/utils/format';

type Tab = 'cho-duyet' | 'da-duyet';

/** Trang Admin: chỉ điều phối 2 tab, mỗi tab là một component riêng (SRP). */
export function AdminDuyetXePage() {
  const [tab, setTab] = useState<Tab>('cho-duyet');
  // Mở từ thông báo: ?tab=cho-duyet|da-duyet&xe=45 → chọn tab, tô sáng / mở đúng xe
  const [thamSo] = useThamSoMotLan('tab', 'xe');
  const [xeMo, setXeMo] = useState<XeCanMo | null>(null);
  useKhiMoTuThongBao(thamSo, true, (t) => {
    if (t.tab === 'cho-duyet' || t.tab === 'da-duyet') setTab(t.tab);
    const id = soId(t.xe);
    setXeMo(id ? { xeId: id } : null);
  });
  const doiTab = (t: Tab) => { setTab(t); setXeMo(null); };
  return (
    <>
      <h1>Quản lý xe</h1>
      <div className="tab" role="tablist">
        <button className={tab === 'cho-duyet' ? 'chon' : ''} onClick={() => doiTab('cho-duyet')}>Chờ duyệt</button>
        <button className={tab === 'da-duyet' ? 'chon' : ''} onClick={() => doiTab('da-duyet')}>Xe đã duyệt</button>
      </div>
      {tab === 'cho-duyet'
        ? <BangChoDuyet xeMo={xeMo} xongMo={() => setXeMo(null)} sangDaDuyet={(m) => { setTab('da-duyet'); setXeMo(m); }} />
        : <BangXeDaDuyet xeMo={xeMo} xongMo={() => setXeMo(null)} />}
    </>
  );
}

/** Xe cần mở khi vào trang từ thông báo; tin: câu giải thích (ví dụ "đã được duyệt bởi …"). */
interface XeCanMo { xeId: number; tin?: string }

function ONhanCho({ c }: { c?: { soChoConBan: number; sucChua: number } }) {
  if (!c) return <span className="mo">…</span>;
  return c.soChoConBan <= 0
    ? <span className="nhan nhan-loi">Hết chỗ (0 / {c.sucChua})</span>
    : <span className="nhan nhan-ok">Còn {c.soChoConBan} / {c.sucChua}</span>;
}

/**
 * Hàng chờ duyệt. Giúp Admin duyệt nhanh:
 * - server đã sắp: loại xe còn chỗ lên trước, rồi xe gửi lâu nhất;
 * - bấm "Xem" để mở ảnh giấy đăng ký cạnh biển số khách nhập, duyệt xong tự chuyển sang xe kế tiếp;
 * - chọn nhiều xe rồi duyệt / từ chối một lần (từ chối dùng chung một lý do).
 */
function BangChoDuyet({ xeMo, xongMo, sangDaDuyet }: { xeMo: XeCanMo | null; xongMo: () => void; sangDaDuyet: (m: XeCanMo) => void }) {
  const { services } = useApp();
  const ds = useTaiDuLieu(() => services.adminGoiCho.layXeChoDuyet());
  const bangGia = useTaiDuLieu(() => services.goiCho.layBangGia());
  const choCon = bangGia.duLieu?.choConBan;
  const coXeHetCho = ds.duLieu?.some((x) => x.loaiXeHetCho) ?? false;
  const [chon, setChon] = useState<Set<number>>(new Set());
  const [xemId, setXemId] = useState<number | null>(null);
  const [tuChoi, setTuChoi] = useState<number[] | null>(null);
  const [lyDo, setLyDo] = useState('');
  const [thongBao, setThongBao] = useState<{ loai: 'ok' | 'loi' | 'canh-bao'; noiDung: string } | null>(null);
  const [dangXuLy, setDangXuLy] = useState(false);

  const [tim, setTim] = useState('');
  const tatCa = ds.duLieu ?? [];
  // Hàng chờ đã tải hết nên lọc ngay trên máy, cùng quy tắc chuẩn hoá với tab "Xe đã duyệt"
  const dsXe = tim.trim() ? tatCa.filter((x) => x.bienSo.includes(chuanHoaBienSo(tim))) : tatCa;
  const bien = (id: number) => hienThiBienSo(tatCa.find((x) => x.xeId === id)?.bienSo ?? String(id));
  const xeDangXem = dsXe.find((x) => x.xeId === xemId) ?? null;

  // Mở từ thông báo "có xe chờ duyệt": còn trong hàng chờ thì mở ảnh + tô sáng; đã duyệt thì sang tab Xe đã duyệt
  useEffect(() => {
    if (!xeMo || !ds.duLieu) return;
    const id = xeMo.xeId;
    xongMo();
    const x = ds.duLieu.find((v) => v.xeId === id);
    if (x) {
      setTim('');
      if (x.coAnh) setXemId(id);
      requestAnimationFrame(() => toSang(`xe-${id}`));
      return;
    }
    services.adminGoiCho.layChiTietXe(id).then((ct) => {
      const b = hienThiBienSo(ct.xe.bienSo);
      if (ct.xe.trangThaiDuyet === 'DA_DUYET')
        sangDaDuyet({ xeId: id, tin: `Xe ${b} đã được duyệt${ct.xe.tenAdminDuyet ? ` bởi ${ct.xe.tenAdminDuyet}` : ''}${ct.xe.duyetLuc ? ` lúc ${dinhDangNgay(ct.xe.duyetLuc)}` : ''}.` });
      else if (ct.xe.trangThaiDuyet === 'TU_CHOI')
        setThongBao({ loai: 'canh-bao', noiDung: `Xe ${b} đã bị từ chối, đang chờ chủ xe gửi lại ảnh giấy đăng ký.` });
      else if (ct.xe.trangThaiDuyet === 'BI_GO')
        setThongBao({ loai: 'canh-bao', noiDung: `Xe ${b} đã bị gỡ khỏi hệ thống.` });
      else setThongBao({ loai: 'canh-bao', noiDung: `Xe ${b} chưa nằm trong hàng chờ (chưa có ảnh giấy đăng ký).` });
    }).catch(() => setThongBao({ loai: 'loi', noiDung: 'Không mở được nội dung này: không tìm thấy xe.' }));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [xeMo, ds.duLieu]);

  function batTat(id: number) {
    setChon((c) => { const m = new Set(c); if (m.has(id)) m.delete(id); else m.add(id); return m; });
  }
  const chonHet = dsXe.length > 0 && dsXe.every((x) => chon.has(x.xeId));

  async function xuLy(ids: number[], dongY: boolean, lyDoTuChoi?: string) {
    setDangXuLy(true); setThongBao(null);
    try {
      const kq = await services.adminGoiCho.duyetHangLoat(ids, dongY, lyDoTuChoi);
      const viec = dongY ? 'duyệt' : 'từ chối';
      const hetChoDuyet = dongY ? kq.thanhCong.filter((id) => dsXe.find((x) => x.xeId === id)?.loaiXeHetCho).length : 0;
      let noiDung = kq.thanhCong.length ? `Đã ${viec} ${kq.thanhCong.length} xe: ${kq.thanhCong.map(bien).join(', ')}.` : `Không ${viec} được xe nào.`;
      if (hetChoDuyet) noiDung += ` ${hetChoDuyet} xe thuộc loại đang hết chỗ nên khách chưa mua gói được.`;
      if (kq.loi.length) noiDung += ' Bỏ qua: ' + kq.loi.map((l) => `${bien(l.xeId)} (${l.thongDiep})`).join('; ');
      setThongBao({ loai: kq.loi.length ? (kq.thanhCong.length ? 'canh-bao' : 'loi') : 'ok', noiDung });
      // xem ảnh từng xe: xử lý xong tự mở xe kế tiếp trong hàng chờ
      if (xemId !== null && kq.thanhCong.includes(xemId)) {
        const i = dsXe.findIndex((x) => x.xeId === xemId);
        const keTiep = dsXe.slice(i + 1).find((x) => !kq.thanhCong.includes(x.xeId));
        setXemId(keTiep?.xeId ?? null);
      }
      setChon(new Set()); setTuChoi(null); setLyDo('');
      await ds.taiLai();
    } catch (e) { setThongBao({ loai: 'loi', noiDung: (e as Error).message }); }
    finally { setDangXuLy(false); }
  }

  return (
    <>
      {choCon && <section className="luoi-cho">{choCon.map((c) => <ChoConBanThe key={c.loaiXeId} c={c} />)}</section>}
      {coXeHetCho && (
        <ThongBao loai="canh-bao">
          Có xe chờ duyệt thuộc loại đang hết chỗ (xếp cuối danh sách). Duyệt xe chỉ xác minh xe, không giữ chỗ: duyệt xong, khách chưa mua gói được cho tới khi có chỗ trống.
        </ThongBao>
      )}
      {thongBao && <ThongBao loai={thongBao.loai}>{thongBao.noiDung}</ThongBao>}
      {ds.loi && <ThongBao loai="loi">{ds.loi}</ThongBao>}
      {ds.dangTai && !ds.duLieu && <DangTai />}
      {tatCa.length > 0 && <OTimBienSo giaTri={tim} doi={setTim} />}
      {ds.duLieu && tatCa.length === 0 && <ThongBao loai="tin">Không có xe nào chờ duyệt.</ThongBao>}
      {tatCa.length > 0 && dsXe.length === 0 && <ThongBao loai="tin">Không tìm thấy xe chờ duyệt có biển số “{tim.trim()}”.</ThongBao>}

      {xeDangXem && (
        <section className="the xem-gdk">
          <div className="xem-gdk-anh"><AnhGiayDangKy xeId={xeDangXem.xeId} /></div>
          <div className="xem-gdk-tt">
            <div className="mo">Biển số khách nhập</div>
            <div className="bien-so">{hienThiBienSo(xeDangXem.bienSo)}</div>
            <dl className="xe-tt">
              <div><dt>Loại</dt><dd>{tenLoaiXe(xeDangXem.loaiXeId)}</dd></div>
              <div><dt>Chủ xe</dt><dd>{xeDangXem.tenChuXe}</dd></div>
              <div><dt>Gửi lúc</dt><dd>{dinhDangNgay(xeDangXem.taoLuc)}</dd></div>
            </dl>
            <p className="mo">Đối chiếu biển số, loại xe và tên chủ xe trên giấy đăng ký.</p>
            <div className="hang-nut">
              <button className="nut" disabled={dangXuLy || !xeDangXem.coAnh} onClick={() => xuLy([xeDangXem.xeId], true)}>Khớp, duyệt</button>
              <button className="nut nut-phu" disabled={dangXuLy} onClick={() => setTuChoi([xeDangXem.xeId])}>Không khớp, từ chối</button>
              <button className="nut nut-phu" onClick={() => setXemId(null)}>Đóng</button>
            </div>
          </div>
        </section>
      )}

      {tuChoi && (
        <section className="the form">
          <h3>Từ chối {tuChoi.length} xe: {tuChoi.map(bien).join(', ')}</h3>
          <label>Lý do (chủ xe sẽ thấy, ít nhất 5 ký tự)
            <input value={lyDo} onChange={(e) => setLyDo(e.target.value)} placeholder="VD: Ảnh mờ, không đọc được biển số" autoFocus />
          </label>
          <div className="hang-nut">
            {['Ảnh mờ, không đọc được', 'Biển số không khớp giấy đăng ký', 'Tên chủ xe không khớp tài khoản'].map((g) => (
              <button key={g} type="button" className="nut nut-phu nut-nho" onClick={() => setLyDo(g)}>{g}</button>
            ))}
          </div>
          <div className="hang-nut">
            <button className="nut nut-nguy" disabled={dangXuLy || lyDo.trim().length < 5} onClick={() => xuLy(tuChoi, false, lyDo)}>Xác nhận từ chối</button>
            <button className="nut nut-phu" onClick={() => { setTuChoi(null); setLyDo(''); }}>Huỷ</button>
          </div>
        </section>
      )}

      {dsXe.length > 0 && (
        <>
          <div className="thanh-chon">
            <span>Đã chọn <strong>{chon.size}</strong> / {dsXe.length} xe</span>
            <button className="nut nut-nho" disabled={dangXuLy || chon.size === 0} onClick={() => xuLy([...chon], true)}>Duyệt các xe đã chọn</button>
            <button className="nut nut-phu nut-nho" disabled={dangXuLy || chon.size === 0} onClick={() => setTuChoi([...chon])}>Từ chối các xe đã chọn</button>
          </div>
          <div className="bang-cuon">
            <table className="bang">
              <thead><tr>
                <th><input type="checkbox" aria-label="Chọn tất cả" checked={chonHet} onChange={() => setChon(chonHet ? new Set() : new Set(dsXe.map((x) => x.xeId)))} /></th>
                <th>Biển số</th><th>Loại</th><th>Chỗ còn</th><th>Chủ xe</th><th>Gửi lúc</th><th>Giấy đăng ký</th><th></th>
              </tr></thead>
              <tbody>
                {dsXe.map((x) => (
                  <tr key={x.xeId} className={x.xeId === xemId ? 'dong-chon' : ''} data-dich={`xe-${x.xeId}`}>
                    <td><input type="checkbox" aria-label={`Chọn ${x.bienSo}`} checked={chon.has(x.xeId)} onChange={() => batTat(x.xeId)} /></td>
                    <td className="bien-so nho">{hienThiBienSo(x.bienSo)}</td>
                    <td>{tenLoaiXe(x.loaiXeId)}</td>
                    <td><ONhanCho c={timChoCon(choCon, x.loaiXeId)} /></td>
                    <td>{x.tenChuXe}</td>
                    <td>{dinhDangNgay(x.taoLuc)}</td>
                    <td>{x.coAnh
                      ? <button className="nut nut-phu nut-nho" onClick={() => setXemId(x.xeId)}>Xem ảnh</button>
                      : <span className="nhan nhan-loi">Chưa có ảnh</span>}</td>
                    <td className="hang-nut">
                      <button className="nut nut-nho" disabled={dangXuLy || !x.coAnh} title={x.coAnh ? '' : 'Chưa có ảnh giấy đăng ký'} onClick={() => xuLy([x.xeId], true)}>Duyệt</button>
                      <button className="nut nut-phu nut-nho" disabled={dangXuLy} onClick={() => setTuChoi([x.xeId])}>Từ chối</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </>
  );
}

/** Ô tìm biển số dùng chung cho 2 tab. Không phân biệt chấm, gạch, khoảng trắng, hoa thường. */
function OTimBienSo({ giaTri, doi }: { giaTri: string; doi: (v: string) => void }) {
  return (
    <div className="o-tim">
      <input type="search" value={giaTri} onChange={(e) => doi(e.target.value)} placeholder="Tìm biển số…"
             aria-label="Tìm biển số" autoComplete="off" spellCheck={false} />
      {giaTri && <button type="button" className="nut nut-phu nut-nho" onClick={() => doi('')}>Xoá</button>}
    </div>
  );
}

const LOC: { ma: LocXeDaDuyet; ten: string }[] = [
  { ma: 'TAT_CA', ten: 'Tất cả' },
  { ma: 'CON_HAN', ten: 'Còn hạn' },
  { ma: 'SAP_HET', ten: 'Sắp hết hạn (≤ 72 giờ)' },
  { ma: 'CHUA_CO', ten: 'Hết hạn / chưa mua gói' },
  { ma: 'TRONG_BAI', ten: 'Đang trong bãi' },
];
const SO_DONG = 20;

/**
 * Tab "Xe đã duyệt": tìm biển số ở server (chờ 300 ms sau lần gõ cuối), lọc nhanh, phân trang 20 xe/trang.
 * Bấm cả dòng để mở khung chi tiết; nút "Gỡ xe" trên dòng chặn sự kiện lan ra dòng.
 */
function BangXeDaDuyet({ xeMo, xongMo }: { xeMo: XeCanMo | null; xongMo: () => void }) {
  const { services } = useApp();
  const [tim, setTim] = useState('');
  const [loc, setLoc] = useState<LocXeDaDuyet>('TAT_CA');
  const timTre = useTreHoan(tim.trim(), 300);
  // Đổi chuỗi tìm hoặc bộ lọc thì về trang 1 ngay trong lần render đó (không gọi API 2 lần)
  const khoa = `${timTre}|${loc}`;
  const [theoKhoa, setTheoKhoa] = useState({ khoa, trang: 1 });
  const trang = theoKhoa.khoa === khoa ? theoKhoa.trang : 1;
  const setTrang = (f: (t: number) => number) => setTheoKhoa({ khoa, trang: f(trang) });
  const ds = useTaiDuLieu(() => services.adminGoiCho.layXeDaDuyet(timTre, loc, trang, SO_DONG), [timTre, loc, trang]);

  const [xemXe, setXemXe] = useState<XeQuanLy | null>(null);
  const [dangGo, setDangGo] = useState<XeQuanLy | null>(null);
  const [lyDo, setLyDo] = useState('');
  const [dangGui, setDangGui] = useState(false);
  const [thongBao, setThongBao] = useState<{ loai: 'ok' | 'loi'; noiDung: string } | null>(null);

  async function go() {
    if (!dangGo) return;
    if (lyDo.trim().length < 5) { setThongBao({ loai: 'loi', noiDung: 'Nhập lý do gỡ xe (ít nhất 5 ký tự).' }); return; }
    setDangGui(true); setThongBao(null);
    try {
      await services.adminGoiCho.goXe(dangGo.xeId, lyDo.trim());
      setThongBao({ loai: 'ok', noiDung: `Đã gỡ xe ${hienThiBienSo(dangGo.bienSo)}. Gói còn hạn (nếu có) đã bị huỷ, không hoàn tiền.` });
      setDangGo(null); setLyDo('');
      await ds.taiLai();
    } catch (e) { setThongBao({ loai: 'loi', noiDung: (e as Error).message }); }
    finally { setDangGui(false); }
  }

  // Mở từ thông báo (khách huỷ gói, xe vừa được duyệt): tìm theo biển số của xe rồi mở khung chi tiết
  const [tinMo, setTinMo] = useState<string | null>(null);
  const [choMo, setChoMo] = useState<{ xeId: number; bienSo: string } | null>(null);
  useEffect(() => {
    if (!xeMo) return;
    xongMo();
    setTinMo(xeMo.tin ?? null);
    services.adminGoiCho.layChiTietXe(xeMo.xeId).then((ct) => {
      setLoc('TAT_CA'); setTim(ct.xe.bienSo); setChoMo({ xeId: ct.xe.xeId, bienSo: ct.xe.bienSo });
    }).catch(() => setThongBao({ loai: 'loi', noiDung: 'Không mở được nội dung này: không tìm thấy xe.' }));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [xeMo]);
  useEffect(() => {
    if (!choMo || !ds.duLieu || ds.dangTai) return;
    const r = ds.duLieu.ds.find((x) => x.xeId === choMo.xeId);
    if (r) { setChoMo(null); setXemXe(r); requestAnimationFrame(() => toSang(`xe-${r.xeId}`)); return; }
    if (timTre === choMo.bienSo) { // đã tìm đúng biển mà không có: xe không còn trong danh sách đã duyệt (bị gỡ)
      setChoMo(null);
      setThongBao({ loai: 'loi', noiDung: `Xe ${hienThiBienSo(choMo.bienSo)} không còn trong danh sách xe đã duyệt (có thể đã bị gỡ).` });
    }
  }, [choMo, ds.duLieu, ds.dangTai, timTre]);

  function moGo(x: XeQuanLy) { setXemXe(null); setDangGo(x); setLyDo(''); setThongBao(null); }

  const kq = ds.duLieu;
  const soTrang = kq ? Math.max(1, Math.ceil(kq.tong / SO_DONG)) : 1;
  const dangLoc = timTre !== '' || loc !== 'TAT_CA';

  return (
    <>
      <div className="thanh-loc">
        <OTimBienSo giaTri={tim} doi={setTim} />
        <div className="chip-loc" role="group" aria-label="Lọc theo trạng thái gói">
          {LOC.map((l) => (
            <button key={l.ma} type="button" className={loc === l.ma ? 'chon' : ''} aria-pressed={loc === l.ma} onClick={() => setLoc(l.ma)}>{l.ten}</button>
          ))}
        </div>
      </div>

      {tinMo && <ThongBao loai="tin">{tinMo}</ThongBao>}
      {thongBao && <ThongBao loai={thongBao.loai}>{thongBao.noiDung}</ThongBao>}
      {ds.loi && <ThongBao loai="loi">{ds.loi}</ThongBao>}
      {ds.dangTai && !kq && <DangTai />}

      {dangGo && (
        <div className="the xac-nhan xac-nhan-nguy">
          <h3>Gỡ xe {hienThiBienSo(dangGo.bienSo)} của {dangGo.tenChuXe}?</h3>
          <p>Xe sẽ không vào bãi được nữa. Gói còn hạn bị huỷ và không hoàn tiền. Chủ xe nhận được thông báo kèm lý do.</p>
          {dangGo.dangTrongBai && <ThongBao loai="canh-bao">Xe đang trong bãi: phải cho xe ra trước mới gỡ được.</ThongBao>}
          <div className="o-ly-do">
            <input value={lyDo} onChange={(e) => setLyDo(e.target.value)} placeholder="Lý do, VD: dùng biển số giả" autoFocus />
            <button className="nut nut-nguy" onClick={go} disabled={dangGui}>{dangGui ? 'Đang gỡ…' : 'Gỡ xe'}</button>
            <button className="nut nut-phu" onClick={() => { setDangGo(null); setLyDo(''); }}>Huỷ</button>
          </div>
        </div>
      )}

      {kq && kq.ds.length === 0 && (
        <ThongBao loai="tin">
          {timTre ? <>Không tìm thấy xe có biển số “{timTre}”{loc !== 'TAT_CA' ? ' trong bộ lọc đang chọn' : ''}.</>
            : dangLoc ? 'Không có xe nào khớp bộ lọc.' : 'Chưa có xe nào đã duyệt.'}
        </ThongBao>
      )}
      {kq && kq.ds.length > 0 && (
        <>
          <div className={`bang-cuon ${ds.dangTai ? 'dang-tai-lai' : ''}`}>
            <table className="bang bang-bam">
              <thead><tr><th>Biển số</th><th>Loại xe</th><th>Chủ xe</th><th>Gói hiện tại</th><th>Hết hạn</th><th>Trạng thái gói</th><th>Trong bãi</th><th></th></tr></thead>
              <tbody>
                {kq.ds.map((x) => (
                  <tr key={x.xeId} className={xemXe?.xeId === x.xeId ? 'dong-chon' : ''} tabIndex={0} data-dich={`xe-${x.xeId}`}
                      onClick={() => setXemXe(x)}
                      onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); setXemXe(x); } }}
                      aria-label={`Xem chi tiết xe ${hienThiBienSo(x.bienSo)}`}>
                    <td className="bien-so nho">{hienThiBienSo(x.bienSo)}</td>
                    <td>{tenLoaiXe(x.loaiXeId)}</td>
                    <td>{x.tenChuXe}</td>
                    <td>{x.tenGoiHienTai ?? <span className="mo">—</span>}</td>
                    <td>{dinhDangNgay(x.hetHanLuc)}</td>
                    <td><NhanGoi tt={x.trangThaiGoi} /></td>
                    <td>{x.dangTrongBai ? 'Đang gửi' : 'Không'}</td>
                    <td className="hang-nut">
                      <button className="nut nut-nguy nut-nho" onClick={(e) => { e.stopPropagation(); moGo(x); }}
                              onKeyDown={(e) => e.stopPropagation()}>Gỡ xe</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="phan-trang">
            <span className="mo">{kq.tong} xe · trang {kq.trang} / {soTrang}</span>
            <button className="nut nut-phu nut-nho" disabled={trang <= 1 || ds.dangTai} onClick={() => setTrang((t) => t - 1)}>‹ Trước</button>
            <button className="nut nut-phu nut-nho" disabled={trang >= soTrang || ds.dangTai} onClick={() => setTrang((t) => t + 1)}>Sau ›</button>
          </div>
        </>
      )}

      {xemXe && (
        <KhungChiTietXe dong={xemXe} khiDong={() => setXemXe(null)} khiGoXe={moGo} khiDaHuyGoi={() => void ds.taiLai()} />
      )}
    </>
  );
}
