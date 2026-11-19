import { useState } from 'react';
import { DangTai, ThongBao } from '@/shared/ui/ui';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';
import { useApp } from '@/app/AppContext';
import { dinhDangNgay } from '@/shared/utils/format';

/** Admin: danh sách tài khoản, tìm theo tên / SĐT, khoá và mở khoá (không khoá được tài khoản quản trị). */
export function AdminTaiKhoanPage() {
  const { services } = useApp();
  const [tuKhoa, setTuKhoa] = useState('');
  const [loc, setLoc] = useState({ tuKhoa: '', trangThai: '' });
  const ds = useTaiDuLieu(() => services.adminTaiKhoan.layDanhSach(loc.tuKhoa, loc.trangThai), [loc]);
  const [loi, setLoi] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);

  async function doi(id: number, ten: string, khoa: boolean) {
    if (khoa && !confirm(`Khoá tài khoản ${ten}? Người này sẽ bị đăng xuất và không đăng nhập được.`)) return;
    setLoi(null); setOk(null);
    try {
      await (khoa ? services.adminTaiKhoan.khoa(id) : services.adminTaiKhoan.moKhoa(id));
      setOk(`${khoa ? 'Đã khoá' : 'Đã mở khoá'} ${ten}.`);
      await ds.taiLai();
    } catch (e) { setLoi((e as Error).message); }
  }

  return (
    <>
      <h1>Tài khoản</h1>
      <form className="the hang-form" onSubmit={(e) => { e.preventDefault(); setLoc({ ...loc, tuKhoa }); }}>
        <input value={tuKhoa} onChange={(e) => setTuKhoa(e.target.value)} placeholder="Tìm theo tên hoặc số điện thoại" />
        <select value={loc.trangThai} onChange={(e) => setLoc({ ...loc, trangThai: e.target.value })} aria-label="Trạng thái">
          <option value="">Mọi trạng thái</option>
          <option value="HOAT_DONG">Đang hoạt động</option>
          <option value="KHOA">Đã khoá</option>
        </select>
        <button className="nut">Tìm</button>
      </form>
      {ok && <ThongBao loai="ok">{ok}</ThongBao>}
      {loi && <ThongBao loai="loi">{loi}</ThongBao>}
      {ds.loi && <ThongBao loai="loi">{ds.loi}</ThongBao>}
      {ds.dangTai && !ds.duLieu && <DangTai />}
      {ds.duLieu && (
        <div className="bang-cuon">
          <table className="bang">
            <thead><tr><th>Họ tên</th><th>SĐT</th><th>Email</th><th>Vai trò</th><th>Trạng thái</th><th>Đăng nhập gần nhất</th><th></th></tr></thead>
            <tbody>
              {ds.duLieu.map((t) => (
                <tr key={t.id}>
                  <td><strong>{t.hoTen}</strong></td>
                  <td>{t.sdt}</td>
                  <td>{t.email ?? '—'}</td>
                  <td>{t.vaiTro === 'ADMIN' ? 'Quản trị' : 'Người dùng'}</td>
                  <td>
                    <span className={`nhan ${t.trangThai === 'KHOA' ? 'nhan-loi' : 'nhan-ok'}`}>{t.trangThai === 'KHOA' ? 'Đã khoá' : 'Hoạt động'}</span>
                    {t.dangTamKhoaDangNhap && <span className="nhan nhan-cho">Tạm khoá đăng nhập</span>}
                  </td>
                  <td>{dinhDangNgay(t.dangNhapLuc)}</td>
                  <td>
                    {t.vaiTro !== 'ADMIN' && (t.trangThai === 'KHOA'
                      ? <button className="nut nut-phu nut-nho" onClick={() => void doi(t.id, t.hoTen, false)}>Mở khoá</button>
                      : <button className="nut nut-nguy nut-nho" onClick={() => void doi(t.id, t.hoTen, true)}>Khoá</button>)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
