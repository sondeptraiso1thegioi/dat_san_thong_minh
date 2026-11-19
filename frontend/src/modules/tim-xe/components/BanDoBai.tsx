import type { ReactNode } from 'react';
import anhSoDo from '@/assets/so-do-bai.png';
import type { DuongDiBanDo, KhuCho } from '../models';
import { TEN_MUC } from '../models';
import { CONG_VAO_X, ghepKhuXeMay, KHU_XE_MAY_TREN_SO_DO, KICH_THUOC_ANH } from '../soDoXeMay';

const { rong: W, cao: H } = KICH_THUOC_ANH;

/** Vị trí đặt mũi tên chỉ hướng dọc đường (cách nhau `buoc` đơn vị ảnh), kèm góc quay theo hướng đi. */
function viTriMuiTen(diem: { x: number; y: number }[], buoc = 46) {
  const kq: { x: number; y: number; goc: number }[] = [];
  let du = buoc / 2;
  for (let i = 1; i < diem.length; i++) {
    const ax = diem[i - 1].x * W, ay = diem[i - 1].y * H, bx = diem[i].x * W, by = diem[i].y * H;
    const dai = Math.hypot(bx - ax, by - ay);
    const goc = (Math.atan2(by - ay, bx - ax) * 180) / Math.PI;
    for (let t = du; t < dai; t += buoc) kq.push({ x: ax + ((bx - ax) * t) / dai, y: ay + ((by - ay) * t) / dai, goc });
    du = (du - dai) % buoc;
    if (du < 0) du += buoc;
  }
  return kq;
}

/**
 * Ảnh "Phương án bố trí bãi đỗ xe" + 3 chấm khu xe máy (R, S, Hầm) + đường đi vẽ bằng SVG đè lên ảnh.
 * SVG dùng viewBox đúng kích thước gốc của ảnh nên co giãn cùng ảnh, nét vẽ không méo.
 * onChamKhu có thì chấm là nút bấm được (người dùng); không có thì chỉ xem (Admin).
 */
export function BanDoBai({ khu, duong, khuDichId, khuChonId, hienViTri = false, onChamKhu, children }: {
  khu: KhuCho[] | undefined;
  duong?: DuongDiBanDo | null;
  khuDichId?: number | null;
  khuChonId?: number | null;
  /** Ghim "Bạn đang ở đây" tại Cổng vào X (vừa qua cổng, chưa có GPS). */
  hienViTri?: boolean;
  onChamKhu?: (k: KhuCho) => void;
  children?: ReactNode;
}) {
  const theoMa = ghepKhuXeMay(khu);
  const diem = duong?.diem ?? [];
  const toaDo = diem.map((d) => `${(d.x * W).toFixed(1)},${(d.y * H).toFixed(1)}`).join(' ');

  return (
    <div className="ban-do-bai">
      <img src={anhSoDo} alt="Sơ đồ bố trí bãi đỗ xe" draggable={false} />
      <svg className="ban-do-lop" viewBox={`0 0 ${W} ${H}`} aria-hidden="true">
        {diem.length > 1 && (
          <g>
            <polyline points={toaDo} className="duong-vien" />
            <polyline points={toaDo} className="duong-net" />
            {viTriMuiTen(diem).map((m, i) => (
              <path key={i} d="M -3.5 -4 L 3 0 L -3.5 4" className="mui-ten-duong" transform={`translate(${m.x.toFixed(1)} ${m.y.toFixed(1)}) rotate(${m.goc.toFixed(0)})`} />
            ))}
          </g>
        )}
        {hienViTri && (
          <g transform={`translate(${CONG_VAO_X.x * W} ${CONG_VAO_X.y * H})`}>
            <circle r="9" className="vi-tri-song" />
            <circle r="5" className="vi-tri-cham" />
          </g>
        )}
      </svg>

      {hienViTri && (
        <div className="nhan-ghim nhan-o-day" style={{ left: `${CONG_VAO_X.x * 100}%`, top: `${CONG_VAO_X.y * 100}%` }}>Bạn đang ở đây</div>
      )}

      {KHU_XE_MAY_TREN_SO_DO.map((c) => {
        const k = theoMa.get(c.ma);
        const moTa = k ? `${c.ten}: còn ${k.conLai}/${k.sucChua} chỗ (${TEN_MUC[k.muc]})` : `${c.ten}: chưa có số liệu`;
        const laDich = !!k && k.id === khuDichId;
        const lop = `diem-khu diem-xe-may muc-${k ? k.muc : 'CHUA'} ${laDich ? 'diem-dich' : ''} ${k && k.id === khuChonId ? 'diem-chon' : ''}`;
        const noiDung = <><b>{c.nhan}</b>{k && <span>{k.conLai}</span>}</>;
        const viTri = { left: `${c.x}%`, top: `${c.y}%` };
        return (
          <div key={c.ma}>
            {laDich && <div className="nhan-ghim nhan-dich" style={viTri}>Điểm đến</div>}
            {onChamKhu && k
              ? <button type="button" className={`${lop} cham-duoc`} style={viTri} title={moTa} aria-label={`${moTa}. Chạm để xem`}
                        aria-pressed={k.id === khuChonId} onClick={() => onChamKhu(k)}>{noiDung}</button>
              : <div className={lop} style={viTri} title={moTa} role="img" aria-label={moTa}>{noiDung}</div>}
          </div>
        );
      })}
      {children}
    </div>
  );
}
