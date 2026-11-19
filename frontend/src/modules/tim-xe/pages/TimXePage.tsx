import { useState } from 'react';
import { DangTai, ThongBao } from '@/shared/ui/ui';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';
import { useNgheThongBao } from '@/shared/realtime/useNgheThongBao';
import { Link } from 'react-router-dom';
import { useApp } from '@/app/AppContext';
import { soId, toSang, useKhiMoTuThongBao, useThamSoMotLan, useVeKhongMoDuoc } from '@/shared/ui/useDichDen';
import { dinhDangNgay, hienThiBienSo } from '@/shared/utils/format';
import type { KhuCho, XeTimXe } from '../models';
import { TEN_NGUON } from '../models';
import { MucChoKhu } from '../components/MucChoKhu';
import { BanDoBai } from '../components/BanDoBai';
import { DemNguocGiuCho } from '../components/DemNguocGiuCho';
import { moManHinhDanDuong } from '../components/DieuHuongToanCuc';

/**
 * Trang khách "Tìm xe" (xe máy, mức khu): sau khi qua cổng vào thấy "Mời vào khu X" kèm đường đi và thời gian giữ chỗ;
 * khi camera lối vào khu ghi nhận thì thấy xe đang ở khu nào, từ lúc nào. "Đã đỗ xe" là xác nhận phụ:
 * camera đã ghi khu khác thì giữ theo camera và báo quản lý. Tải lại khi có thông báo và mỗi 10 giây.
 */
export function TimXePage() {
  const { services } = useApp();
  const tx = useTaiDuLieu(() => services.timXe.layTimXe(), [], 10_000);
  useNgheThongBao((s) => { if (['VAO', 'RA', 'KHU'].includes(s.loai)) void tx.taiLai(); });
  const trongBai = tx.duLieu?.xe.filter((x) => x.trongBai) ?? [];
  const ngoaiBai = tx.duLieu?.xe.filter((x) => !x.trongBai) ?? [];

  // Mở từ thông báo: ?xe=45 (xe đang đỗ ở khu nào) hoặc ?danDuong=<lượt gửi> (mở màn hình chỉ đường)
  const [thamSo] = useThamSoMotLan('xe', 'danDuong');
  const [tinMo, setTinMo] = useState<{ loai: 'tin' | 'canh-bao' | 'loi'; noiDung: string; soDo?: boolean } | null>(null);
  const veKhongMoDuoc = useVeKhongMoDuoc();
  useKhiMoTuThongBao(thamSo, !!tx.duLieu, (t) => {
    setTinMo(null);
    const luot = soId(t.danDuong);
    if (luot !== null) { void moDanDuong(luot); return; }
    const xeId = soId(t.xe);
    if (xeId === null) return;
    const x = tx.duLieu?.xe.find((v) => v.xeId === xeId);
    if (!x) { veKhongMoDuoc(); return; }
    if (!x.trongBai) { setTinMo({ loai: 'tin', noiDung: `Xe ${hienThiBienSo(x.bienSo)} đã ra khỏi bãi.` }); return; }
    toSang(`xe-${xeId}`);
  });

  /** Phiên của đúng lượt gửi đó còn dẫn đường thì mở bản đồ; đã tới / hết giờ / xe đã ra thì nói rõ, không mở. */
  async function moDanDuong(luot: number) {
    try {
      const p = (await services.dieuHuong.layCuaToi()).phien;
      if (p && p.luotGuiId === luot) {
        toSang(`xe-${p.xeId}`);
        if (p.trangThai === 'DANG_DAN') { moManHinhDanDuong(); return; }
        setTinMo(p.trangThai === 'HET_HAN'
          ? { loai: 'canh-bao', noiDung: `Phiên chỉ đường tới ${p.khuDich.ten} đã hết giờ giữ chỗ. Xem Sơ đồ chỗ trống để chọn khu còn chỗ.`, soDo: true }
          : p.khuThucTe
            ? { loai: 'tin', noiDung: `Phiên chỉ đường đã kết thúc: xe ${hienThiBienSo(p.bienSo)} đã tới ${p.khuThucTe.ten}.` }
            : { loai: 'tin', noiDung: 'Phiên chỉ đường đã kết thúc.', soDo: true });
        return;
      }
      setTinMo({ loai: 'tin', noiDung: 'Phiên chỉ đường này đã kết thúc (xe đã ra khỏi bãi hoặc đã có lượt gửi mới).', soDo: true });
    } catch (e) { setTinMo({ loai: 'loi', noiDung: (e as Error).message }); }
  }

  return (
    <>
      <div className="tieu-de-trang">
        <h1>Tìm xe</h1>
        <button className="nut nut-phu nut-nho" onClick={() => tx.taiLai()} disabled={tx.dangTai}>Tải lại</button>
      </div>
      {tinMo && <ThongBao loai={tinMo.loai}>{tinMo.noiDung}{tinMo.soDo && <> <Link to="/so-do">Mở Sơ đồ chỗ trống</Link></>}</ThongBao>}
      {tx.loi && <ThongBao loai="loi">{tx.loi}</ThongBao>}
      {tx.dangTai && !tx.duLieu && <DangTai />}
      {tx.duLieu && (
        <>
          {trongBai.length === 0
            ? <p className="mo">Không có xe nào của bạn đang ở trong bãi.</p>
            : <div className="luoi-xe">{trongBai.map((x) => <TheTimXe key={x.xeId} x={x} bayGio={tx.duLieu!.bayGio} khu={tx.duLieu!.khuXeMay} khiXong={() => tx.taiLai()} />)}</div>}

          <h2>Mức chỗ các khu xe máy</h2>
          <MucChoKhu khu={tx.duLieu.khuXeMay} />

          {ngoaiBai.length > 0 && (
            <>
              <h2>Xe đang ở ngoài bãi</h2>
              <div className="chip-xe">{ngoaiBai.map((x) => <span key={x.xeId} className="chip">{hienThiBienSo(x.bienSo)}</span>)}</div>
            </>
          )}
        </>
      )}
    </>
  );
}

function TheTimXe({ x, bayGio, khu, khiXong }: { x: XeTimXe; bayGio: string; khu: KhuCho[]; khiXong: () => void }) {
  const { services } = useApp();
  const macDinh = x.khuId ?? x.moiVao?.khuId ?? khu[0]?.id ?? 0;
  const [khuChon, setKhuChon] = useState(macDinh);
  const [moChon, setMoChon] = useState(false);
  const [kq, setKq] = useState<string | null>(null);
  const [loi, setLoi] = useState<string | null>(null);
  const [dangGui, setDangGui] = useState(false);

  async function daDo() {
    setLoi(null); setKq(null); setDangGui(true);
    try { setKq(await services.timXe.daDoXe(x.xeId, khuChon)); setMoChon(false); khiXong(); }
    catch (e) { setLoi((e as Error).message); }
    finally { setDangGui(false); }
  }

  const dangGiu = x.moiVao?.trangThai === 'DANG_GIU' && !x.khuId;
  return (
    <div className="the the-tim-xe" data-dich={`xe-${x.xeId}`}>
      <div className="xe-dau">
        <div>
          <div className="bien-so">{hienThiBienSo(x.bienSo)}</div>
          <div className="mo">Vào bãi {dinhDangNgay(x.thoiGianVao)}</div>
        </div>
        {!x.hoTroDinhVi ? <span className="nhan">Ô tô</span>
          : x.khuId ? <span className="nhan nhan-ok">Đã định vị</span>
          : dangGiu ? <span className="nhan nhan-cho">Đang giữ chỗ</span>
          : <span className="nhan nhan-loi">Chưa rõ khu</span>}
      </div>

      {!x.hoTroDinhVi && <p className="mo">Định vị trong khu ô tô chưa hỗ trợ ở phiên bản này.</p>}

      {x.hoTroDinhVi && x.khuId && (
        <div className="vi-tri-xe">
          <div className="nhan-nho">Xe của bạn ở</div>
          <div className="vi-tri-khu">{x.khu}</div>
          <div className="mo">Từ {dinhDangNgay(x.vaoKhuLuc)}{x.nguonKhu ? `, ghi nhận bởi ${TEN_NGUON[x.nguonKhu]}` : ''}</div>
        </div>
      )}

      {x.hoTroDinhVi && dangGiu && x.moiVao && (
        <div className="vi-tri-xe">
          <div className="nhan-nho">Mời vào</div>
          <div className="vi-tri-khu">{x.moiVao.khu}</div>
          <div className="mo">Chỗ được giữ thêm <DemNguocGiuCho hetHanLuc={x.moiVao.hetHanLuc} bayGio={bayGio} /></div>
        </div>
      )}

      {x.hoTroDinhVi && !x.khuId && !dangGiu && (
        <p className="mo">Chưa có camera lối vào khu nào ghi nhận xe. Đỗ xong, bấm "Đã đỗ xe" và chọn khu.</p>
      )}

      {x.duong && (
        <BanDoBai khu={khu} khuDichId={x.duong.khuId} hienViTri={!x.khuId}
                  duong={x.khuId ? null : { denKhuId: x.duong.khuId, tongMet: x.duong.tongMet, diem: x.duong.diem }} />
      )}

      {x.hoTroDinhVi && (
        moChon ? (
          <div className="hang-form">
            <select value={khuChon} onChange={(e) => setKhuChon(Number(e.target.value))} aria-label="Chọn khu đã đỗ">
              {khu.map((k) => <option key={k.id} value={k.id}>{k.ten}</option>)}
            </select>
            <button className="nut" onClick={() => void daDo()} disabled={dangGui || !khuChon}>Xác nhận</button>
            <button className="nut nut-phu" onClick={() => setMoChon(false)}>Thôi</button>
          </div>
        ) : (
          <div className="hang-nut">
            <button className="nut nut-phu" onClick={() => { setKhuChon(macDinh); setMoChon(true); }}>Đã đỗ xe</button>
          </div>
        )
      )}
      {kq && <ThongBao loai="ok">{kq}</ThongBao>}
      {loi && <ThongBao loai="loi">{loi}</ThongBao>}
    </div>
  );
}
