import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useApp } from '@/app/AppContext';
import { DangTai, ThongBao } from '@/shared/ui/ui';
import type { TrangThaiNhom } from '../models';

export function NhomGiaDinhPage() {
  const { services, phien } = useApp(); const api = services.nhomGiaDinh;
  const [ds, setDs] = useState<TrangThaiNhom | null>(null); const [loi, setLoi] = useState('');
  const [ten, setTen] = useState(''); const [ma, setMa] = useState(''); const [ban, setBan] = useState(false);
  const tai = useCallback(async () => { setDs(await api.xem()); }, [api]);
  useEffect(() => { void tai().catch(er => setLoi((er as Error).message)); }, [tai]);
  async function lam(action: () => Promise<unknown>) {
    setBan(true); setLoi('');
    try { await action(); await tai(); } catch (er) { setLoi((er as Error).message); }
    finally { setBan(false); }
  }
  const nhom = ds?.nhom; const chu = nhom?.chuNhomId === phien?.id;
  return <div className="form"><h1>Nhóm gia đình</h1>
    <p className="mo">Mỗi tài khoản thuộc tối đa một nhóm. Chỉ thành viên đã được duyệt mới dùng được xe có bật chia sẻ nhóm.</p>
    {loi && <ThongBao loai="loi">{loi}</ThongBao>}
    <button className="nut nut-phu" disabled={ban} onClick={() => void lam(tai)}>Tải lại</button>
    {!ds ? <DangTai /> : !nhom ? <>
      <form className="the form" onSubmit={e => { e.preventDefault(); void lam(() => api.tao(ten)); }}>
        <h2>Tạo nhóm mới</h2><label>Tên nhóm<input required minLength={2} maxLength={100} value={ten} onChange={e => setTen(e.target.value)} /></label>
        <button className="nut" disabled={ban}>Tạo nhóm</button>
      </form>
      <form className="the form" onSubmit={e => { e.preventDefault(); void lam(() => api.xinThamGia(ma)); }}>
        <h2>Tham gia bằng mã mời</h2><label>Mã mời<input required minLength={6} maxLength={10} value={ma} onChange={e => setMa(e.target.value.toUpperCase())} /></label>
        <button className="nut" disabled={ban}>Gửi yêu cầu</button>
      </form>
      <section className="the"><h2>Yêu cầu của tôi</h2>{ds.yeuCauCuaToi.length === 0 ? <p>Chưa có yêu cầu.</p> : ds.yeuCauCuaToi.map(y => <p key={y.id}>Nhóm #{y.nhomId}: {y.trangThai === 'CHO_DUYET' ? 'Chờ chủ nhóm duyệt' : y.trangThai === 'DONG_Y' ? 'Đã đồng ý' : 'Đã từ chối'}</p>)}</section>
    </> : <>
      <section className="the form"><h2>{nhom.ten}</h2><p>{nhom.thanhVien.length} thành viên · Bạn là {chu ? 'chủ nhóm' : 'thành viên'}</p>
        {chu && <><p>Mã mời: <strong>{nhom.maMoi}</strong></p><p>Hết hạn: {nhom.maHetHanLuc && new Date(nhom.maHetHanLuc).toLocaleString('vi-VN')}</p>
          <button className="nut nut-phu" disabled={ban} onClick={() => void lam(() => api.doiMa())}>Tạo mã mời mới</button></>}
        {nhom.thanhVien.map(v => <div key={v.taiKhoanId} className="hang">
          <span>Tài khoản #{v.taiKhoanId}{v.taiKhoanId === phien?.id ? ' (bạn)' : ''} · {v.vaiTro === 'CHU' ? 'Chủ nhóm' : 'Thành viên'}</span>
          {chu && v.taiKhoanId !== phien?.id && <button className="nut nut-phu" disabled={ban} onClick={() => { if (window.confirm('Xóa thành viên này? Họ sẽ mất quyền dùng xe chia sẻ trong nhóm.')) void lam(() => api.xoaThanhVien(v.taiKhoanId)); }}>Xóa</button>}
        </div>)}
        <Link to="/xe">Xem xe được chia sẻ</Link>
      </section>
      {chu && <section className="the form"><h2>Chờ duyệt</h2>{nhom.yeuCau.length === 0 ? <p>Không có yêu cầu mới.</p> : nhom.yeuCau.map(y => <div key={y.id} className="hang">
        <span>Tài khoản #{y.taiKhoanId}</span><button className="nut" disabled={ban} onClick={() => void lam(() => api.xuLy(y.id, true))}>Đồng ý</button>
        <button className="nut nut-phu" disabled={ban} onClick={() => void lam(() => api.xuLy(y.id, false))}>Từ chối</button>
      </div>)}</section>}
      <button className="nut nut-phu" disabled={ban} onClick={() => { if (window.confirm(chu ? 'Giải tán nhóm? Tất cả thành viên sẽ mất quyền dùng chung xe.' : 'Rời nhóm và mất quyền dùng chung xe?')) void lam(() => chu ? api.giaiTan() : api.roi()); }}>{chu ? 'Giải tán nhóm' : 'Rời nhóm'}</button>
    </>}
  </div>;
}
