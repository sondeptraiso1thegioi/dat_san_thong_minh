import { useEffect, useRef, useState } from 'react';
import { hienThiBienSo } from '@/shared/utils/format';
import type { DieuHuongCuaToi, KhuCho, PhienDieuHuong } from '../models';
import { BanDoBai } from './BanDoBai';
import { DemNguocGiuCho } from './DemNguocGiuCho';
import { TheKhu } from './TheKhu';

const gioPhut = (iso: string) => new Date(iso).toLocaleTimeString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit' });

/** "đã đỗ 25 phút" / "1 giờ 05 phút". Chỉ để hiển thị, KHÔNG tính tiền (dự án chỉ có gói tháng / năm). */
export function ThoiGianDaDo({ tu }: { tu: string }) {
  const [bay, setBay] = useState(Date.now());
  useEffect(() => { const id = window.setInterval(() => setBay(Date.now()), 30_000); return () => window.clearInterval(id); }, []);
  const phut = Math.max(0, Math.floor((bay - new Date(tu).getTime()) / 60_000));
  return <>{phut < 1 ? 'vừa đỗ' : phut < 60 ? `${phut} phút` : `${Math.floor(phut / 60)} giờ ${String(phut % 60).padStart(2, '0')} phút`}</>;
}

/**
 * Màn hình dẫn đường trong bãi ("Google Maps thu nhỏ"). Không dùng GPS: vị trí bắt đầu là Cổng vào X,
 * "đã tới nơi" là lần camera hướng VÀO của khu quét được biển. Chạm chấm khu khác để đổi khu đích.
 */
export function ManHinhDanDuong({ du, khu, onDong, onDoiKhu }: {
  du: DieuHuongCuaToi;
  khu: KhuCho[] | undefined;
  onDong: () => void;
  onDoiKhu: (khuId: number) => Promise<void>;
}) {
  const nutDong = useRef<HTMLButtonElement>(null);
  const dongRef = useRef(onDong);
  dongRef.current = onDong;
  useEffect(() => {
    nutDong.current?.focus();
    const phim = (e: KeyboardEvent) => { if (e.key === 'Escape') dongRef.current(); };
    window.addEventListener('keydown', phim);
    return () => window.removeEventListener('keydown', phim);
  }, []);

  const p = du.phien;
  const tieuDe = !p ? 'Bãi xe máy' : p.trangThai === 'DANG_DAN' ? 'Đang dẫn đường' : 'Dẫn đường đã kết thúc';
  return (
    <div className="man-dan-duong-che" onClick={(e) => { if (e.target === e.currentTarget) onDong(); }}>
      <section className="man-dan-duong" role="dialog" aria-modal="true" aria-label={tieuDe}>
        <header className="mdd-dau">
          <div>
            <div className="nhan-nho">{tieuDe}</div>
            {p && <div className="bien-so nho">{hienThiBienSo(p.bienSo)}</div>}
          </div>
          <button ref={nutDong} type="button" className="nut nut-phu nut-nho" onClick={onDong}>
            {p?.trangThai === 'DANG_DAN' ? 'Thu nhỏ' : 'Đóng'}
          </button>
        </header>
        {p?.trangThai === 'DANG_DAN'
          ? <DangDan p={p} bayGio={du.bayGio} khu={khu} onDoiKhu={onDoiKhu} />
          : p ? <KetThuc p={p} khu={khu} onDong={onDong} />
          : du.hetCho ? <HetCho bienSo={du.hetCho.bienSo} khu={khu} /> : null}
      </section>
    </div>
  );
}

function DangDan({ p, bayGio, khu, onDoiKhu }: { p: PhienDieuHuong; bayGio: string; khu: KhuCho[] | undefined; onDoiKhu: (id: number) => Promise<void> }) {
  const [chon, setChon] = useState<KhuCho | null>(null);
  const [dangGui, setDangGui] = useState(false);
  const [tin, setTin] = useState<{ loai: 'ok' | 'loi'; noiDung: string } | null>(null);
  const dich = khu?.find((k) => k.id === p.khuDich.id) ?? p.khuDich;
  const chonMoi = chon && (khu?.find((k) => k.id === chon.id) ?? chon);

  async function doi(k: KhuCho) {
    setDangGui(true); setTin(null);
    try {
      await onDoiKhu(k.id);
      setTin({ loai: 'ok', noiDung: `Đã đổi điểm đến sang ${k.ten}. Thời gian giữ chỗ vẫn giữ nguyên.` });
      setChon(null);
    } catch (e) { setTin({ loai: 'loi', noiDung: (e as Error).message }); }
    finally { setDangGui(false); }
  }

  return (
    <div className="mdd-than">
      <div className="mdd-huong-dan" aria-live="polite">
        <div className="mdd-cau">Đi tới <strong>{dich.ten}</strong>, còn {dich.conLai} chỗ</div>
        <div className="mo">
          {p.duong ? `Khoảng ${p.duong.tongMet} m từ Cổng vào X` : 'Chưa có đường đi trên sơ đồ'}
          {p.khuGoiYId !== p.khuDich.id && ' · khu bạn tự chọn'}
        </div>
        <div className="mdd-giu">Giữ chỗ cho bạn còn <DemNguocGiuCho hetHanLuc={p.hetHanLuc} bayGio={bayGio} /></div>
      </div>

      {p.canhBao && (
        <div className="thong-bao thong-bao-canh-bao" role="alert">
          <strong>{p.canhBao.ten} đã đầy</strong>, mời bạn sang <strong>{dich.ten}</strong> theo đường trên bản đồ.
        </div>
      )}
      {tin && <div className={`thong-bao thong-bao-${tin.loai}`} role="status">{tin.noiDung}</div>}

      <BanDoBai khu={khu} duong={p.duong} khuDichId={p.khuDich.id} khuChonId={chon?.id} hienViTri
                onChamKhu={(k) => setChon((c) => (c?.id === k.id ? null : k))} />

      {chonMoi
        ? <TheKhu khu={chonMoi} cheDo="dan-duong" laDich={chonMoi.id === p.khuDich.id} dangGui={dangGui}
                  onChiDuong={() => void doi(chonMoi)} onDong={() => setChon(null)} />
        : <p className="mo mdd-goi-y">Muốn đỗ chỗ khác? Chạm vào chấm R, S hoặc Hầm trên bản đồ.</p>}

      {p.duong && p.duong.diem.length > 1 && (
        <ol className="mdd-cac-buoc">
          {p.duong.diem.filter((d) => d.ten).map((d, i) => <li key={i}>{d.ten}</li>)}
        </ol>
      )}
    </div>
  );
}

function KetThuc({ p, khu, onDong }: { p: PhienDieuHuong; khu: KhuCho[] | undefined; onDong: () => void }) {
  const thuc = p.khuThucTe;
  return (
    <div className="mdd-than">
      {p.trangThai === 'DA_TOI' && thuc && (
        <div className="mdd-ket-qua mdd-ok">
          <div className="mdd-bieu-tuong" aria-hidden="true">✓</div>
          <h2>Đã đến bãi đỗ thành công</h2>
          <p>Đang đỗ xe tại <strong>{thuc.ten}</strong></p>
          <p className="mo">Vào khu lúc {gioPhut(thuc.vaoLuc)} · đã đỗ <ThoiGianDaDo tu={thuc.vaoLuc} /></p>
        </div>
      )}
      {p.trangThai === 'TOI_KHU_KHAC' && thuc && (
        <div className="mdd-ket-qua mdd-tin">
          <div className="mdd-bieu-tuong" aria-hidden="true">i</div>
          <h2>Bạn đã đỗ tại {thuc.ten}</h2>
          <p>Khác khu được chỉ định ({p.khuDich.ten}). Không sao, hệ thống ghi theo camera.</p>
          <p className="mo">Vào khu lúc {gioPhut(thuc.vaoLuc)} · đã đỗ <ThoiGianDaDo tu={thuc.vaoLuc} /></p>
        </div>
      )}
      {p.trangThai === 'HET_HAN' && (
        <div className="mdd-ket-qua mdd-canh">
          <div className="mdd-bieu-tuong" aria-hidden="true">!</div>
          <h2>Chưa xác nhận được vị trí xe</h2>
          <p>Hết thời gian giữ chỗ ở {p.khuDich.ten} mà camera khu chưa thấy xe. Đỗ xong, bạn có thể bấm "Đã đỗ xe" trong mục Tìm xe.</p>
        </div>
      )}
      {p.trangThai === 'DA_DONG' && (
        <div className="mdd-ket-qua mdd-tin">
          <h2>Xe đã ra khỏi bãi</h2>
          <p>Kết thúc dẫn đường, chỗ giữ ở {p.khuDich.ten} đã được trả lại.</p>
        </div>
      )}
      {thuc && <BanDoBai khu={khu} khuDichId={thuc.id} />}
      <button type="button" className="nut" onClick={onDong}>Đã hiểu</button>
    </div>
  );
}

function HetCho({ bienSo, khu }: { bienSo: string; khu: KhuCho[] | undefined }) {
  return (
    <div className="mdd-than">
      <div className="mdd-ket-qua mdd-canh">
        <div className="mdd-bieu-tuong" aria-hidden="true">!</div>
        <h2>Bãi xe máy đã hết chỗ</h2>
        <p>Xe {hienThiBienSo(bienSo)} đã qua cổng nhưng các khu R, S, Hầm đều đã đầy nên chưa chỉ đường được. Vui lòng hỏi nhân viên bãi.</p>
      </div>
      <BanDoBai khu={khu} />
    </div>
  );
}
