import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useApp } from '@/app/AppContext';
import { ThongBao } from '@/shared/ui/ui';

export function DoiMatKhauPage() {
  const { services } = useApp();
  const nav = useNavigate();
  const [cu, setCu] = useState(''); const [moi, setMoi] = useState(''); const [xacNhan, setXacNhan] = useState('');
  const [loi, setLoi] = useState(''); const [ban, setBan] = useState(false);
  async function gui(e: FormEvent) {
    e.preventDefault(); setLoi('');
    if (moi !== xacNhan) { setLoi('Mật khẩu xác nhận không khớp.'); return; }
    setBan(true);
    try { await services.auth.doiMatKhau(cu, moi); services.phien.luu(null, 'Đã đổi mật khẩu. Hãy đăng nhập lại.'); nav('/dang-nhap'); }
    catch (er) { setLoi((er as Error).message); }
    finally { setBan(false); }
  }
  return <form className="the form" onSubmit={gui}><h1>Đổi mật khẩu</h1>
    <label>Mật khẩu hiện tại<input type="password" required autoComplete="current-password" value={cu} onChange={e => setCu(e.target.value)} /></label>
    <label>Mật khẩu mới<input type="password" required minLength={8} maxLength={64} autoComplete="new-password" value={moi} onChange={e => setMoi(e.target.value)} /></label>
    <label>Nhập lại mật khẩu<input type="password" required autoComplete="new-password" value={xacNhan} onChange={e => setXacNhan(e.target.value)} /></label>
    <p className="mo">Sau khi đổi mật khẩu, bạn cần đăng nhập lại.</p>
    {loi && <ThongBao loai="loi">{loi}</ThongBao>}<button className="nut" disabled={ban}>Đổi mật khẩu</button>
  </form>;
}
