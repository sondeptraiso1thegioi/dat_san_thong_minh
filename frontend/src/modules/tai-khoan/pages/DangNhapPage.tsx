import { useState, type FormEvent } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { ThongBao } from '@/shared/ui/ui';
import { useApp } from '@/app/AppContext';
import { KhungXacThuc } from '../components/KhungXacThuc';

export function DangNhapPage() {
  const { phien, dangNhap, thongBaoPhien } = useApp();
  const nav = useNavigate();
  const [sdt, setSdt] = useState('');
  const [matKhau, setMatKhau] = useState('');
  const [hienMk, setHienMk] = useState(false);
  const [loi, setLoi] = useState<string | null>(null);
  const [dangGui, setDangGui] = useState(false);

  if (phien) return <Navigate to={phien.vaiTro === 'ADMIN' ? '/admin/cong' : '/tim-xe'} replace />;

  async function gui(e: FormEvent) {
    e.preventDefault();
    setDangGui(true); setLoi(null);
    try {
      const p = await dangNhap(sdt, matKhau);
      nav(p.vaiTro === 'ADMIN' ? '/admin/cong' : '/tim-xe');
    } catch (er) { setLoi((er as Error).message); }
    finally { setDangGui(false); }
  }

  return (
    <KhungXacThuc nhan="Nhà xe thông minh" tieuDe="Đăng nhập" moTa="Gửi xe, tìm lại xe và quản lý gói chỗ của bạn."
      chan={<>
        <p className="xt-mo-ta">Chưa có tài khoản? <Link to="/dang-ky" className="xt-lien-ket">Tạo tài khoản</Link></p>
        <p className="xt-ghi-chu">Mỗi tài khoản đăng nhập trên một điện thoại.</p>
      </>}>
      <form className="xt-form" onSubmit={gui}>
        {thongBaoPhien && <ThongBao loai="canh-bao">{thongBaoPhien}</ThongBao>}
        <div className="xt-truong">
          <label htmlFor="sdt">Số điện thoại</label>
          <input id="sdt" className="o-nhap" type="tel" inputMode="numeric" autoComplete="tel" placeholder="0912 345 678"
                 value={sdt} onChange={(e) => setSdt(e.target.value)} required />
        </div>
        <div className="xt-truong">
          <label htmlFor="mk">Mật khẩu</label>
          <div className="xt-mk">
            <input id="mk" className="o-nhap" type={hienMk ? 'text' : 'password'} autoComplete="current-password" placeholder="Nhập mật khẩu"
                   value={matKhau} onChange={(e) => setMatKhau(e.target.value)} required />
            <button type="button" className="xt-mat" aria-label={hienMk ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} onClick={() => setHienMk(!hienMk)}>
              {hienMk
                ? <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M3 3l18 18" /><path d="M10.6 5.1A10 10 0 0 1 12 5c6 0 9.5 7 9.5 7a17 17 0 0 1-3 3.9" /><path d="M6.6 6.6A17 17 0 0 0 2.5 12S6 19 12 19a9.6 9.6 0 0 0 5.4-1.6" /><path d="M9.9 9.9a3 3 0 0 0 4.2 4.2" /></svg>
                : <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M2.5 12S6 5 12 5s9.5 7 9.5 7-3.5 7-9.5 7-9.5-7-9.5-7z" /><circle cx="12" cy="12" r="3" /></svg>}
            </button>
          </div>
          <Link to="/quen-mat-khau" className="xt-quen">Quên mật khẩu?</Link>
        </div>
        {loi && <ThongBao loai="loi">{loi}</ThongBao>}
        <button className="nut xt-nut" disabled={dangGui}>{dangGui ? 'Đang đăng nhập…' : 'Đăng nhập'}</button>
      </form>
    </KhungXacThuc>
  );
}
