import { useState } from 'react';
import { DangTai, ThongBao } from '@/shared/ui/ui';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';
import { useApp } from '@/app/AppContext';
import { dinhDangTien, tenLoaiXe } from '@/shared/utils/format';
import type { GoiAdmin, SucChuaLoaiXe } from '../models';
import { kiemTraGia, phanTramDoi } from '../utils/gia';

/**
 * Admin quản lý gói của module GoiCho: đổi giá từng gói và đổi sức chứa từng loại xe.
 * Giá mới chỉ áp cho lần mua sau; khách mở trang Mua gói là thấy giá mới.
 */
export function AdminGoiGiaPage() {
  const { services } = useApp();
  const bang = useTaiDuLieu(() => services.adminGoiCho.layBangGia());
  const [thongBao, setThongBao] = useState<string | null>(null);

  return (
    <>
      <div className="xe-dau">
        <h1>Gói &amp; giá</h1>
        <button className="nut nut-phu nut-nho" onClick={() => bang.taiLai()} disabled={bang.dangTai}>Tải lại</button>
      </div>
      <ThongBao loai="tin">
        Giá mới chỉ áp cho <strong>lần mua sau</strong>. Gói khách đã mua giữ nguyên số tiền đã trả, nếu huỷ thì hoàn theo số tiền đã trả.
        Khách đang mở trang Mua gói với giá cũ sẽ được báo giá đã đổi và phải xác nhận lại, không bị trừ tiền theo giá mới mà chưa thấy.
      </ThongBao>
      {thongBao && <ThongBao loai="ok">{thongBao}</ThongBao>}
      {bang.loi && <ThongBao loai="loi">{bang.loi}</ThongBao>}
      {bang.dangTai && !bang.duLieu && <DangTai />}
      {bang.duLieu && bang.duLieu.sucChua.map((s) => (
        <section key={s.loaiXeId} className="the">
          <SucChuaPhan s={s} khiDoi={(tb) => { setThongBao(tb); void bang.taiLai(); }} />
          <h3 style={{ marginTop: 20 }}>Các gói {tenLoaiXe(s.loaiXe).toLowerCase()}</h3>
          <div className="bang-cuon">
            <table className="bang">
              <thead><tr><th>Gói</th><th>Số ngày</th><th>Giá hiện tại</th><th>Đang bán</th><th></th></tr></thead>
              <tbody>
                {bang.duLieu!.goi.filter((g) => g.loaiXeId === s.loaiXeId).map((g) => (
                  <DongGoi key={g.id} g={g} khiDoi={(tb) => { setThongBao(tb); void bang.taiLai(); }} />
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ))}
    </>
  );
}

function DongGoi({ g, khiDoi }: { g: GoiAdmin; khiDoi: (thongBao: string) => void }) {
  const { services } = useApp();
  const [sua, setSua] = useState(false);
  const [giaTri, setGiaTri] = useState(String(g.gia));
  const [loi, setLoi] = useState<string | null>(null);
  const [dangLuu, setDangLuu] = useState(false);
  const giaMoi = Number(giaTri);
  const loiNhap = kiemTraGia(giaMoi);
  const doi = phanTramDoi(g.gia, giaMoi);

  async function luu() {
    setLoi(null); setDangLuu(true);
    try {
      const kq = await services.adminGoiCho.doiGia(g.id, giaMoi);
      setSua(false);
      khiDoi(`Đã đổi giá gói ${tenLoaiXe(kq.loaiXeId).toLowerCase()} ${kq.ten}: ${dinhDangTien(g.gia)} → ${dinhDangTien(kq.gia)}. Khách thấy giá mới ngay.`);
    } catch (e) { setLoi((e as Error).message); }
    finally { setDangLuu(false); }
  }

  return (
    <tr>
      <td>{g.ten}</td>
      <td>{g.soNgay}</td>
      <td>
        {sua ? (
          <div className="o-gia">
            <input type="number" min={1000} step={1000} value={giaTri} autoFocus
                   onChange={(e) => setGiaTri(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && !loiNhap && luu()} />
            <span className="mo">
              {loiNhap ?? (giaMoi === g.gia ? 'Chưa đổi' : `${dinhDangTien(g.gia)} → ${dinhDangTien(giaMoi)} (${doi})`)}
            </span>
            {loi && <span className="chu-loi">{loi}</span>}
          </div>
        ) : <strong>{dinhDangTien(g.gia)}</strong>}
      </td>
      <td>{g.dangBan ? 'Có' : 'Ngừng bán'}</td>
      <td>
        {sua ? (
          <div className="hang-nut">
            <button className="nut nut-nho" onClick={luu} disabled={dangLuu || !!loiNhap || giaMoi === g.gia}>{dangLuu ? 'Đang lưu…' : 'Lưu'}</button>
            <button className="nut nut-phu nut-nho" onClick={() => { setSua(false); setLoi(null); setGiaTri(String(g.gia)); }}>Thôi</button>
          </div>
        ) : (
          <button className="nut nut-phu nut-nho" onClick={() => { setGiaTri(String(g.gia)); setSua(true); }}>Đổi giá</button>
        )}
      </td>
    </tr>
  );
}

function SucChuaPhan({ s, khiDoi }: { s: SucChuaLoaiXe; khiDoi: (thongBao: string) => void }) {
  const { services } = useApp();
  const [sua, setSua] = useState(false);
  const [giaTri, setGiaTri] = useState(String(s.sucChua));
  const [loi, setLoi] = useState<string | null>(null);
  const phanTram = s.sucChua ? Math.min(100, Math.round((s.dangChiem / s.sucChua) * 100)) : 100;

  async function luu() {
    setLoi(null);
    try {
      const kq = await services.adminGoiCho.doiSucChua(s.loaiXeId, Number(giaTri));
      setSua(false);
      khiDoi(`Đã đổi sức chứa ${tenLoaiXe(kq.loaiXe).toLowerCase()} thành ${kq.sucChua} chỗ (còn bán ${kq.soChoConBan}).`);
    } catch (e) { setLoi((e as Error).message); }
  }

  return (
    <>
      <div className="xe-dau">
        <h2>{tenLoaiXe(s.loaiXe)}</h2>
        <span className={`nhan ${s.hetCho ? 'nhan-loi' : 'nhan-ok'}`}>{s.hetCho ? 'Hết chỗ' : `Còn bán ${s.soChoConBan} chỗ`}</span>
      </div>
      <div className="thanh"><div className="thanh-day" style={{ width: `${phanTram}%` }} /></div>
      <dl className="so-lieu">
        <div><dt>Sức chứa</dt><dd>{s.sucChua}</dd></div>
        <div><dt>Giữ suất (gói còn hạn)</dt><dd>{s.soXeGiuSuat}</dd></div>
        <div><dt>Quá hạn còn trong bãi</dt><dd>{s.soXeQuaHanTrongBai}</dd></div>
      </dl>
      {loi && <ThongBao loai="loi">{loi}</ThongBao>}
      {sua ? (
        <div className="o-ly-do">
          <input type="number" min={0} value={giaTri} onChange={(e) => setGiaTri(e.target.value)} style={{ minWidth: 90, width: 90 }} />
          <button className="nut nut-nho" onClick={luu}>Lưu</button>
          <button className="nut nut-phu nut-nho" onClick={() => { setSua(false); setLoi(null); setGiaTri(String(s.sucChua)); }}>Thôi</button>
        </div>
      ) : (
        <button className="nut nut-phu nut-nho" onClick={() => setSua(true)}>Đổi sức chứa</button>
      )}
    </>
  );
}
