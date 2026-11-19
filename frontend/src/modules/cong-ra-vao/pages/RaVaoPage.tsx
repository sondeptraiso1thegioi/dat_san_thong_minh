import { useState } from 'react';
import { Link } from 'react-router-dom';
import { DangTai, ThongBao } from '@/shared/ui/ui';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';
import { useNgheThongBao } from '@/shared/realtime/useNgheThongBao';
import { useApp } from '@/app/AppContext';
import { soId, toSang, useKhiMoTuThongBao, useThamSoMotLan } from '@/shared/ui/useDichDen';
import { dinhDangNgay, dinhDangTien, hienThiBienSo } from '@/shared/utils/format';
import type { LuotGui, YeuCauXacNhan } from '../models';
import { TEN_TRANG_THAI_LUOT } from '../models';
import { DemNguoc } from '../components/DemNguoc';

/**
 * Trang khách "Ra vào": xe ở cổng ra thì bấm "Đúng là tôi" (giống 2FA) hoặc "Không phải tôi" trong 60 giây.
 * Mọi người được dùng xe đều thấy; ai bấm trước thì tính, người sau thấy "Đã được [tên] xác nhận".
 * Còn phí quá hạn thì trả trên app, trả xong barrier tự mở. Tải lại khi có thông báo và mỗi 3 giây.
 */
export function RaVaoPage() {
  const { services } = useApp();
  const rv = useTaiDuLieu(() => services.congRaVao.layRaVaoCuaToi(), [], 3_000);
  useNgheThongBao(() => void rv.taiLai());
  const choTra = rv.duLieu?.lichSu.filter((l) => l.trangThai === 'CHO_THANH_TOAN') ?? [];

  // Mở từ thông báo: ?luot=123 (&xacNhan=1 nếu là yêu cầu xác nhận ra). Đọc một lần rồi xoá khỏi URL.
  const [thamSo] = useThamSoMotLan('luot', 'xacNhan');
  const [tinMo, setTinMo] = useState<{ loai: 'tin' | 'canh-bao'; noiDung: string } | null>(null);
  const d = rv.duLieu;
  useKhiMoTuThongBao(thamSo, !!d, (t) => {
    const luotMo = soId(t.luot);
    if (!d || luotMo === null) return;
    setTinMo(null);
    if (d.dangCho.some((y) => y.luotGuiId === luotMo)) { toSang(`yc-luot-${luotMo}`); return; }
    const l = d.lichSu.find((x) => x.id === luotMo);
    if (!l) { setTinMo({ loai: 'tin', noiDung: 'Lượt gửi này đã cũ, không còn trong 30 lượt gần nhất.' }); return; }
    if (l.trangThai === 'CHO_THANH_TOAN') { toSang(`tra-luot-${l.id}`); return; }
    toSang(`luot-${l.id}`);
    if (t.xacNhan) setTinMo(tinYeuCauDaKetThuc(l));
  });

  return (
    <>
      <div className="tieu-de-trang">
        <h1>Ra vào</h1>
        <button className="nut nut-phu nut-nho" onClick={() => rv.taiLai()} disabled={rv.dangTai}>Tải lại</button>
      </div>
      {rv.loi && <ThongBao loai="loi">{rv.loi}</ThongBao>}
      {rv.dangTai && !rv.duLieu && <DangTai />}
      {tinMo && <ThongBao loai={tinMo.loai}>{tinMo.noiDung}</ThongBao>}
      {rv.duLieu && (
        <>
          {choTra.map((l) => <ChoThanhToan key={l.id} l={l} />)}

          <h2>Cần bạn xác nhận</h2>
          {rv.duLieu.dangCho.length === 0
            ? <p className="mo">Không có xe nào của bạn đang ở cổng ra.</p>
            : <div className="luoi-xe">{rv.duLieu.dangCho.map((y) => <XacNhanRa key={y.id} y={y} khiXong={() => rv.taiLai()} />)}</div>}

          <h2>Lịch sử ra vào</h2>
          {rv.duLieu.lichSu.length === 0 ? <p className="mo">Chưa có lượt gửi nào.</p> : (
            <div className="bang-cuon">
              <table className="bang">
                <thead><tr><th>Biển số</th><th>Vào lúc</th><th>Ra lúc</th><th>Trạng thái</th></tr></thead>
                <tbody>
                  {rv.duLieu.lichSu.map((l) => (
                    <tr key={l.id} data-dich={`luot-${l.id}`}>
                      <td className="bien-so nho">{hienThiBienSo(l.bienSo)}</td>
                      <td>{dinhDangNgay(l.thoiGianVao)}</td>
                      <td>{dinhDangNgay(l.thoiGianRa)}</td>
                      <td><span className={`nhan ${l.trangThai === 'NGHI_MAT' ? 'nhan-loi' : l.trangThai === 'DA_RA' ? 'nhan-ok' : 'nhan-cho'}`}>{TEN_TRANG_THAI_LUOT[l.trangThai]}</span></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </>
  );
}

/** Mở yêu cầu xác nhận từ thông báo nhưng yêu cầu không còn chờ: nói rõ trạng thái hiện tại, KHÔNG có nút làm tiếp. */
function tinYeuCauDaKetThuc(l: LuotGui): { loai: 'tin' | 'canh-bao'; noiDung: string } {
  const bien = hienThiBienSo(l.bienSo);
  const gio = l.yeuCauHetHanLuc ? new Date(l.yeuCauHetHanLuc).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) : null;
  if (l.trangThai === 'DA_RA') return { loai: 'tin', noiDung: `Yêu cầu xác nhận của xe ${bien} đã xong: xe đã ra bãi lúc ${dinhDangNgay(l.thoiGianRa)}.` };
  if (l.trangThai === 'NGHI_MAT') return { loai: 'canh-bao', noiDung: `Xe ${bien} đã được báo "Không phải tôi". Cổng không mở, Admin đang kiểm tra.` };
  if (l.yeuCauKetQua === 'HET_HAN')
    return { loai: 'canh-bao', noiDung: `Yêu cầu xác nhận của xe ${bien} đã hết hạn${gio ? ` lúc ${gio}` : ''}, cổng không mở. Admin đang xử lý.` };
  return { loai: 'tin', noiDung: `Yêu cầu xác nhận của xe ${bien} đã kết thúc. Trạng thái hiện tại: ${TEN_TRANG_THAI_LUOT[l.trangThai]}.` };
}

function ChoThanhToan({ l }: { l: LuotGui }) {
  return (
    <div className="the yeu-cau yeu-cau-khach" data-dich={`tra-luot-${l.id}`}>
      <div className="xe-dau">
        <div>
          <div className="mo">Xe đang chờ ở cổng ra</div>
          <div className="bien-so">{hienThiBienSo(l.bienSo)}</div>
        </div>
        <span className="nhan nhan-cho">Chờ thanh toán</span>
      </div>
      <p>Gói đã quá hạn{l.phiQuaHan ? `, còn nợ ${dinhDangTien(l.phiQuaHan)}` : ''}. Trả phí trên app, barrier sẽ tự mở.</p>
      <div className="hang-nut"><Link className="nut" to={`/phi-qua-han/${l.xeId}`}>Trả phí quá hạn</Link></div>
    </div>
  );
}

function XacNhanRa({ y, khiXong }: { y: YeuCauXacNhan; khiXong: () => void }) {
  const { services } = useApp();
  const [loi, setLoi] = useState<string | null>(null);
  const [dangGui, setDangGui] = useState(false);

  async function traLoi(dongY: boolean) {
    if (!dongY && !confirm('Báo KHÔNG phải bạn? Barrier sẽ không mở và quản lý bãi được báo ngay.')) return;
    setLoi(null); setDangGui(true);
    try { await services.congRaVao.nguoiDungXeTraLoi(y.id, dongY); khiXong(); }
    catch (e) { setLoi((e as Error).message); khiXong(); }
    finally { setDangGui(false); }
  }

  return (
    <div className="the yeu-cau yeu-cau-khach" data-dich={`yc-luot-${y.luotGuiId}`}>
      <div className="xe-dau">
        <div>
          <div className="mo">Xe đang ở cổng ra</div>
          <div className="bien-so">{hienThiBienSo(y.bienSo)}</div>
        </div>
        <DemNguoc soGiay={y.soGiayConLai} />
      </div>
      {y.nguoiDungXeDongY === null ? (
        <>
          <p className="mo">Có phải bạn đang lấy xe không? Quá 60 giây không ai trả lời thì barrier không mở.</p>
          <div className="hang-nut">
            <button className="nut" onClick={() => traLoi(true)} disabled={dangGui}>Đúng là tôi</button>
            <button className="nut nut-nguy" onClick={() => traLoi(false)} disabled={dangGui}>Không phải tôi</button>
          </div>
        </>
      ) : <p className="mo">Đã được {y.nguoiDungXeTraLoi ?? 'người dùng xe'} {y.nguoiDungXeDongY ? 'xác nhận' : 'báo không phải mình'}.</p>}
      {loi && <ThongBao loai="canh-bao">{loi}</ThongBao>}
    </div>
  );
}
