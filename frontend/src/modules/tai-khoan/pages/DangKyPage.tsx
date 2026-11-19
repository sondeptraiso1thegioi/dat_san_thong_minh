import { useState, type FormEvent, type ReactElement } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { useApp } from '@/app/AppContext';
import { ThongBao } from '@/shared/ui/ui';
import { KhungXacThuc } from '../components/KhungXacThuc';

export function DangKyPage() {
  const { services, phien, dangNhap } = useApp();
  const nav = useNavigate();
  const [hoTen, setHoTen] = useState('');
  const [sdt, setSdt] = useState('');
  const [email, setEmail] = useState('');
  const [matKhau, setMatKhau] = useState('');
  const [xacNhan, setXacNhan] = useState('');
  const [loi, setLoi] = useState('');
  const [xong, setXong] = useState(false);
  const [ban, setBan] = useState(false);

  if (phien && !ban) return <Navigate to={phien.vaiTro === 'ADMIN' ? '/admin/cong' : '/tim-xe'} replace />;

  async function gui(e: FormEvent) {
    e.preventDefault(); setLoi('');
    if (matKhau !== xacNhan) { setLoi('Mật khẩu xác nhận không khớp.'); return; }
    setBan(true);
    try { await services.auth.dangKy(hoTen, sdt, email, matKhau); }
    catch (er) { setLoi((er as Error).message); setBan(false); return; }
    // Tài khoản đã lưu vào CSDL: đăng nhập luôn bằng chính SĐT + mật khẩu vừa nhập, đưa tới bước tiếp theo là thêm xe.
    try { await dangNhap(sdt, matKhau); nav('/xe', { replace: true }); }
    catch { setXong(true); } // hiếm: đăng ký được nhưng đăng nhập lỗi (mạng, quá nhiều yêu cầu) thì để người dùng tự đăng nhập
    finally { setBan(false); }
  }
  const truong = (id: string, nhan: string, o: ReactElement) => <div className="xt-truong"><label htmlFor={id}>{nhan}</label>{o}</div>;
  return (
    <KhungXacThuc nhan="Tài khoản mới" tieuDe="Tạo tài khoản" moTa="Sau khi đăng ký, bạn thêm xe và mua gói chỗ ngay trong ứng dụng."
      chan={<p className="xt-mo-ta">Đã có tài khoản? <Link to="/dang-nhap" className="xt-lien-ket">Đăng nhập</Link></p>}>
      {xong ? <ThongBao loai="ok">Đăng ký thành công. <Link to="/dang-nhap" className="xt-lien-ket">Đăng nhập</Link> bằng số điện thoại vừa đăng ký.</ThongBao> : (
        <form className="xt-form" onSubmit={gui}>
          {truong('ht', 'Họ và tên', <input id="ht" className="o-nhap" required minLength={2} maxLength={100} autoComplete="name" placeholder="Nguyễn Văn An" value={hoTen} onChange={e => setHoTen(e.target.value)} />)}
          {truong('sdt', 'Số điện thoại', <input id="sdt" className="o-nhap" required type="tel" inputMode="numeric" autoComplete="tel" placeholder="0912 345 678" value={sdt} onChange={e => setSdt(e.target.value)} />)}
          {truong('em', 'Email', <input id="em" className="o-nhap" required type="email" maxLength={255} autoComplete="email" placeholder="ban@email.com" value={email} onChange={e => setEmail(e.target.value)} />)}
          {truong('mk', 'Mật khẩu', <input id="mk" className="o-nhap" required type="password" minLength={8} maxLength={64} autoComplete="new-password" placeholder="Từ 8 đến 64 ký tự" value={matKhau} onChange={e => setMatKhau(e.target.value)} />)}
          {truong('mk2', 'Nhập lại mật khẩu', <input id="mk2" className="o-nhap" required type="password" autoComplete="new-password" placeholder="Nhập lại mật khẩu" value={xacNhan} onChange={e => setXacNhan(e.target.value)} />)}
          {loi && <ThongBao loai="loi">{loi}</ThongBao>}
          <button className="nut xt-nut" disabled={ban}>{ban ? 'Đang tạo tài khoản…' : 'Tạo tài khoản'}</button>
        </form>
      )}
    </KhungXacThuc>
  );
}
