import { useEffect, useRef, useState } from 'react';
import { useApp } from '@/app/AppContext';
import { DangTai, ThongBao } from '@/shared/ui/ui';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';
import { dinhDangTien, hienThiBienSo, tenLoaiXe } from '@/shared/utils/format';
import type { ChiTietXeAdmin, DangKyCho, TrangThaiGoiXe, XeQuanLy } from '../models';
import { AnhGiayDangKy } from './AnhGiayDangKy';
import { GoiCuaXePanel } from './GoiCuaXePanel';

/** Ngày giờ theo giờ Việt Nam, dù máy Admin đặt múi giờ khác. */
const ngayGio = (iso: string | null) =>
  iso ? new Date(iso).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '—';
const ngay = (iso: string | null) =>
  iso ? new Date(iso).toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric' }) : '—';

/** Nhãn màu trạng thái gói: Còn hạn xanh, Sắp hết vàng, Hết hạn / chưa có gói xám. */
export function NhanGoi({ tt }: { tt?: TrangThaiGoiXe }) {
  if (tt === 'CON_HAN') return <span className="nhan nhan-ok">Còn hạn</span>;
  if (tt === 'SAP_HET') return <span className="nhan nhan-cho">Sắp hết hạn</span>;
  return <span className="nhan">Hết hạn / chưa có gói</span>;
}

function trangThaiDong(d: DangKyCho) {
  if (d.trangThai === 'HUY') return <span className="nhan">Đã huỷ</span>;
  return new Date(d.ngayKetThuc).getTime() > Date.now() ? <span className="nhan nhan-ok">Còn hạn</span> : <span className="nhan">Hết hạn</span>;
}

/**
 * Khung chi tiết một xe đã duyệt (panel trượt từ bên phải). Đóng: nút ✕, phím Esc, hoặc bấm vùng tối bên ngoài.
 * "Huỷ gói" dùng lại GoiCuaXePanel (HuyGoiUseCase), "Gỡ xe" trả về trang để dùng lại hộp xác nhận gỡ đã có.
 */
export function KhungChiTietXe({ dong, khiDong, khiGoXe, khiDaHuyGoi }: {
  dong: XeQuanLy; khiDong: () => void; khiGoXe: (x: XeQuanLy) => void; khiDaHuyGoi: () => void;
}) {
  const { services } = useApp();
  const ct = useTaiDuLieu(() => services.adminGoiCho.layChiTietXe(dong.xeId), [dong.xeId]);
  const [moHuyGoi, setMoHuyGoi] = useState(false);
  const nutDong = useRef<HTMLButtonElement>(null);
  const dongRef = useRef(khiDong);
  dongRef.current = khiDong;

  useEffect(() => {
    // mở khung: đưa con trỏ bàn phím vào nút đóng; đóng khung: trả về dòng vừa bấm
    const truoc = document.activeElement as HTMLElement | null;
    nutDong.current?.focus();
    const phim = (e: KeyboardEvent) => {
      // ảnh đang phóng to có lớp phủ riêng, Esc đóng ảnh trước
      if (e.key === 'Escape' && !document.querySelector('.lop-phu')) dongRef.current();
    };
    window.addEventListener('keydown', phim);
    return () => { window.removeEventListener('keydown', phim); truoc?.focus?.(); };
  }, []);
  useEffect(() => { setMoHuyGoi(false); }, [dong.xeId]);

  const d = ct.duLieu;
  return (
    <>
      <div className="khung-ben-che" onClick={khiDong} />
      <aside className="khung-ben" role="dialog" aria-modal="true" aria-label={`Chi tiết xe ${hienThiBienSo(dong.bienSo)}`}>
        <header className="khung-ben-dau">
          <div>
            <div className="bien-so bien-so-lon">{hienThiBienSo(dong.bienSo)}</div>
            <div className="mo">{tenLoaiXe(dong.loaiXeId)} · {dong.tenChuXe}</div>
          </div>
          <button ref={nutDong} className="nut-dong-khung" onClick={khiDong} aria-label="Đóng (Esc)" title="Đóng (Esc)">✕</button>
        </header>

        <div className="khung-ben-than">
          {ct.loi && <ThongBao loai="loi">{ct.loi}</ThongBao>}
          {ct.dangTai && !d && <DangTai />}
          {d && <NoiDung d={d} />}
          {d && moHuyGoi && (
            <GoiCuaXePanel xe={dong} khiDong={() => setMoHuyGoi(false)} khiDaHuy={() => { void ct.taiLai(); khiDaHuyGoi(); }} />
          )}
        </div>

        <footer className="khung-ben-chan">
          <button className="nut nut-phu" disabled={!d || d.lichSuGoi.length === 0} onClick={() => setMoHuyGoi(true)}
                  title={d && d.lichSuGoi.length === 0 ? 'Xe chưa mua gói nào' : ''}>Huỷ gói…</button>
          <button className="nut nut-nguy" onClick={() => khiGoXe(dong)}>Gỡ xe…</button>
        </footer>
      </aside>
    </>
  );
}

function NoiDung({ d }: { d: ChiTietXeAdmin }) {
  const g = d.goiHienTai;
  return (
    <>
      <section className="khung-ben-muc">
        <h3>Thông tin xe</h3>
        <dl className="ds-tt">
          <div><dt>Loại xe</dt><dd>{tenLoaiXe(d.xe.loaiXeId)}</dd></div>
          <div><dt>Đăng ký lúc</dt><dd>{ngayGio(d.xe.taoLuc)}</dd></div>
          <div><dt>Duyệt lúc</dt><dd>{d.xe.duyetLuc ? `${ngayGio(d.xe.duyetLuc)} · ${d.xe.tenAdminDuyet ?? 'Admin không rõ'}` : <span className="mo">Không rõ</span>}</dd></div>
          <div><dt>Nhóm gia đình</dt><dd>{d.xe.chiaSeNhom ? 'Có chia sẻ cho nhóm' : 'Không chia sẻ'}</dd></div>
          <div><dt>Hiện tại</dt><dd>
            {d.trongBai.dangTrongBai
              ? <><span className="nhan nhan-ok">Đang trong bãi</span> vào {ngayGio(d.trongBai.vaoLuc)}{d.trongBai.khu ? ` · ${d.trongBai.khu}` : ''}</>
              : <span className="nhan">Không ở trong bãi</span>}
          </dd></div>
        </dl>
        <div className="mo nho-tren">Giấy đăng ký (bấm để phóng to)</div>
        {d.xe.coAnh ? <AnhGiayDangKy xeId={d.xe.xeId} /> : <div className="anh-gdk anh-gdk-trong">Chưa có ảnh</div>}
      </section>

      <section className="khung-ben-muc">
        <h3>Chủ xe</h3>
        <dl className="ds-tt">
          <div><dt>Họ tên</dt><dd>{d.chuXe.hoTen}</dd></div>
          <div><dt>Điện thoại</dt><dd>{d.chuXe.sdt}</dd></div>
          <div><dt>Email</dt><dd>{d.chuXe.email ?? <span className="mo">Không có</span>}</dd></div>
          <div><dt>Tạo tài khoản</dt><dd>{ngay(d.chuXe.taoLuc)}</dd></div>
        </dl>
      </section>

      <section className="khung-ben-muc">
        <h3>Gói hiện tại</h3>
        {!g ? <p className="mo">Chưa mua gói.</p> : (
          <>
            <div className="goi-hien-tai">
              <div><strong>{g.tenGoi}</strong> · {dinhDangTien(g.soTien)}</div>
              {g.conHan
                ? <span className={`nhan ${g.soNgayConLai <= 3 ? 'nhan-cho' : 'nhan-ok'}`}>Còn {g.soNgayConLai} ngày</span>
                : <span className="nhan nhan-loi">Đã hết hạn {g.soNgayQuaHan} ngày</span>}
            </div>
            <dl className="ds-tt">
              <div><dt>Bắt đầu</dt><dd>{ngayGio(g.ngayBatDau)}</dd></div>
              <div><dt>Hết hạn</dt><dd><strong>{ngayGio(g.ngayKetThuc)}</strong></dd></div>
              {g.phiQuaHan > 0 && <div><dt>Phí quá hạn</dt><dd className="chu-loi"><strong>{dinhDangTien(g.phiQuaHan)}</strong></dd></div>}
            </dl>
          </>
        )}
      </section>

      <section className="khung-ben-muc">
        <h3>Lịch sử gói</h3>
        {d.lichSuGoi.length === 0 ? <p className="mo">Chưa có gói nào.</p> : (
          <div className="bang-cuon">
            <table className="bang bang-gon">
              <thead><tr><th>Gói</th><th>Bắt đầu</th><th>Kết thúc</th><th>Giá</th><th>Trạng thái</th></tr></thead>
              <tbody>
                {d.lichSuGoi.map((l) => (
                  <tr key={l.id}>
                    <td>{l.tenGoi}</td><td>{ngay(l.ngayBatDau)}</td><td>{ngay(l.ngayKetThuc)}</td>
                    <td>{dinhDangTien(l.soTien)}</td><td>{trangThaiDong(l)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </>
  );
}
