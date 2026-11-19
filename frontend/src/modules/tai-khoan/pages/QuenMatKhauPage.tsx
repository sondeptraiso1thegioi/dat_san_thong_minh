import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { useApp } from '@/app/AppContext';
import { ThongBao } from '@/shared/ui/ui';

export function QuenMatKhauPage() {
  const { services } = useApp();
  const [sdt, setSdt] = useState('');
  const [ma, setMa] = useState('');
  const [matKhau, setMatKhau] = useState('');
  const [xacNhan, setXacNhan] = useState('');
  const [loi, setLoi] = useState('');
  const [tin, setTin] = useState('');
  const [ban, setBan] = useState(false);
  async function gui(e: FormEvent, datLai: boolean) {
    e.preventDefault(); setLoi(''); setTin('');
    if (datLai && matKhau !== xacNhan) { setLoi('Mật khẩu xác nhận không khớp.'); return; }
    setBan(true);
    try {
      if (datLai) {
        await services.auth.datLaiMatKhau(sdt, ma, matKhau);
        setTin('Đã đặt lại mật khẩu. Hãy đăng nhập bằng mật khẩu mới.'); setMa(''); setMatKhau(''); setXacNhan('');
      } else setTin((await services.auth.quenMatKhau(sdt)).thongDiep);
    } catch (er) { setLoi((er as Error).message); }
    finally { setBan(false); }
  }
  return <div className="trang-dang-nhap"><div className="the form dang-nhap">
    <h1>Khôi phục mật khẩu</h1>
    <form className="form" onSubmit={e => void gui(e, false)}>
      <label>Số điện thoại<input required inputMode="tel" autoComplete="tel" value={sdt} onChange={e => setSdt(e.target.value)} /></label>
      <button className="nut nut-phu" disabled={ban}>Gửi mã qua email</button>
    </form>
    <form className="form" onSubmit={e => void gui(e, true)}>
      <label>Mã trong email<input required maxLength={64} value={ma} onChange={e => setMa(e.target.value)} autoComplete="one-time-code" /></label>
      <label>Mật khẩu mới<input required type="password" minLength={8} maxLength={64} autoComplete="new-password" value={matKhau} onChange={e => setMatKhau(e.target.value)} /></label>
      <label>Nhập lại mật khẩu<input required type="password" autoComplete="new-password" value={xacNhan} onChange={e => setXacNhan(e.target.value)} /></label>
      <button className="nut" disabled={ban || !sdt}>{ban ? 'Đang xử lý…' : 'Đặt lại mật khẩu'}</button>
    </form>
    {loi && <ThongBao loai="loi">{loi}</ThongBao>}{tin && <ThongBao loai="ok">{tin}</ThongBao>}
    <Link to="/dang-nhap">Về đăng nhập</Link>
  </div></div>;
}
