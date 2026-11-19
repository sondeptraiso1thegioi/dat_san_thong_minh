import { useEffect, useState } from 'react';
import { useApp } from '@/app/AppContext';
import type { DangKyCho, XemHuyGoi, XeQuanLy } from '../models';
import { dinhDangNgay, dinhDangTien, hienThiBienSo } from '@/shared/utils/format';
import { DangTai, ThongBao } from '@/shared/ui/ui';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';

/**
 * Admin xem các gói của một xe và huỷ gói cuối còn hiệu lực, bắt buộc ghi lý do.
 * Lý do này hiện thành hộp đỏ trên thẻ xe của khách hàng. Số tiền hoàn do API tính theo chính sách Admin chọn.
 */
export function GoiCuaXePanel({ xe, khiDong, khiDaHuy }: { xe: XeQuanLy; khiDong: () => void; khiDaHuy: () => void }) {
  const { services } = useApp();
  const ds = useTaiDuLieu(() => services.adminGoiCho.layLichSuGoi(xe.xeId), [xe.xeId]);
  const chinhSach = useTaiDuLieu(() => services.adminGoiCho.layChinhSachHoan());
  const [dangHuy, setDangHuy] = useState<DangKyCho | null>(null);
  const [lyDo, setLyDo] = useState('');
  const [cs, setCs] = useState('KHONG_HOAN');
  const [xemTruoc, setXemTruoc] = useState<XemHuyGoi | null>(null);
  const [dangGui, setDangGui] = useState(false);
  const [thongBao, setThongBao] = useState<{ loai: 'ok' | 'loi'; noiDung: string } | null>(null);

  // gói cuối còn hiệu lực = gói duy nhất được huỷ (huỷ gói giữa sẽ làm hở ngày)
  const bayGio = Date.now();
  const goiHuyDuoc = (ds.duLieu ?? [])
    .filter((d) => d.trangThai === 'CON_HAN' && new Date(d.ngayKetThuc).getTime() > bayGio)
    .sort((a, b) => b.ngayBatDau.localeCompare(a.ngayBatDau))[0];

  useEffect(() => {
    if (!dangHuy) { setXemTruoc(null); return; }
    services.adminGoiCho.xemHuyGoi(dangHuy.id, cs).then(setXemTruoc).catch((e) => setThongBao({ loai: 'loi', noiDung: (e as Error).message }));
  }, [dangHuy, cs, services]);

  async function huy() {
    if (!dangHuy) return;
    if (lyDo.trim().length < 5) { setThongBao({ loai: 'loi', noiDung: 'Nhập lý do huỷ (ít nhất 5 ký tự). Khách hàng sẽ đọc lý do này.' }); return; }
    setDangGui(true); setThongBao(null);
    try {
      const kq = await services.adminGoiCho.huyGoi(dangHuy.id, lyDo.trim(), cs);
      setThongBao({ loai: 'ok', noiDung: `Đã huỷ gói ${kq.tenGoi}. ${kq.soTienHoan > 0 ? `Hoàn ${dinhDangTien(kq.soTienHoan)} cho chủ xe.` : 'Không hoàn tiền.'} Chủ xe đã nhận thông báo kèm lý do.` });
      setDangHuy(null); setLyDo('');
      await ds.taiLai();
      khiDaHuy();
    } catch (e) { setThongBao({ loai: 'loi', noiDung: (e as Error).message }); }
    finally { setDangGui(false); }
  }

  return (
    <div className="the xac-nhan">
      <div className="xe-dau">
        <h3>Gói của xe {hienThiBienSo(xe.bienSo)} ({xe.tenChuXe})</h3>
        <button className="nut nut-phu nut-nho" onClick={khiDong}>Đóng</button>
      </div>
      {thongBao && <ThongBao loai={thongBao.loai}>{thongBao.noiDung}</ThongBao>}
      {ds.loi && <ThongBao loai="loi">{ds.loi}</ThongBao>}
      {ds.dangTai && !ds.duLieu && <DangTai />}
      {ds.duLieu && ds.duLieu.length === 0 && <p className="mo">Xe chưa mua gói nào.</p>}
      {ds.duLieu && ds.duLieu.length > 0 && (
        <div className="bang-cuon">
          <table className="bang">
            <thead><tr><th>Gói</th><th>Từ</th><th>Đến</th><th>Số tiền</th><th>Trạng thái</th><th></th></tr></thead>
            <tbody>
              {ds.duLieu.map((d) => (
                <tr key={d.id}>
                  <td>{d.tenGoi}</td>
                  <td>{dinhDangNgay(d.ngayBatDau)}</td>
                  <td>{dinhDangNgay(d.ngayKetThuc)}</td>
                  <td>{dinhDangTien(d.soTien)}</td>
                  <td>{d.trangThai === 'HUY' ? (d.huyBoiAdmin ? `Admin huỷ: ${d.lyDoHuy}` : `Chủ xe tự huỷ${d.lyDoHuy ? `: ${d.lyDoHuy}` : ''}${d.gopY ? `. Góp ý: “${d.gopY}”` : ''}`) : new Date(d.ngayKetThuc).getTime() <= bayGio ? 'Hết hạn' : 'Còn hạn'}</td>
                  <td>{goiHuyDuoc?.id === d.id && (
                    <button className="nut nut-nguy nut-nho" onClick={() => { setDangHuy(d); setLyDo(''); setThongBao(null); }}>Huỷ gói</button>
                  )}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {dangHuy && (
        <div className="xac-nhan-nguy the">
          <h3>Huỷ gói {dangHuy.tenGoi}?</h3>
          <p>Suất được trả lại cho bãi. Nếu xe đang trong bãi, xe vẫn ở lại nhưng sẽ bị tính phí quá hạn theo ngày.</p>
          <div className="o-ly-do" style={{ marginBottom: 10 }}>
            <span>Hoàn tiền:</span>
            <select style={{ width: "auto" }} value={cs} onChange={(e) => setCs(e.target.value)}>
              {(chinhSach.duLieu ?? []).map((c) => <option key={c.ma} value={c.ma}>{c.ten}</option>)}
            </select>
            {xemTruoc && <strong>{xemTruoc.soTienHoan > 0 ? `Hoàn ${dinhDangTien(xemTruoc.soTienHoan)} (${xemTruoc.soNgayConLai} ngày chưa dùng)` : 'Không hoàn tiền'}</strong>}
          </div>
          <div className="o-ly-do">
            <input value={lyDo} onChange={(e) => setLyDo(e.target.value)} placeholder="Lý do khách hàng sẽ thấy, VD: quá hạn thanh toán" autoFocus />
            <button className="nut nut-nguy" onClick={huy} disabled={dangGui}>{dangGui ? 'Đang huỷ…' : 'Xác nhận huỷ'}</button>
            <button className="nut nut-phu" onClick={() => setDangHuy(null)}>Thôi</button>
          </div>
        </div>
      )}
    </div>
  );
}
