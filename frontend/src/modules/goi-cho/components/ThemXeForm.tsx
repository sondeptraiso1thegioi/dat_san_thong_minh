import { useState, type FormEvent } from 'react';
import type { ChoConBan } from '../models';
import { canhBaoChoKhach, hetCho, timChoCon } from '../utils/choCon';
import { kiemTraAnh } from '../utils/anhGiayDangKy';
import { ChonAnh } from './AnhGiayDangKy';

/** Form thêm xe: chỉ lo nhập liệu và kiểm tra sơ bộ, việc gọi API do trang cha truyền vào (SRP). */
export function ThemXeForm({ onGui, onHuy, choConBan }: {
  onGui: (loaiXeId: number, bienSo: string, chiaSe: boolean, anh: File) => Promise<void>;
  onHuy: () => void;
  choConBan?: ChoConBan[];
}) {
  const [loaiXeId, setLoaiXeId] = useState(2);
  const [bienSo, setBienSo] = useState('');
  const [chiaSe, setChiaSe] = useState(false);
  const [anh, setAnh] = useState<File | null>(null);
  const [dangGui, setDangGui] = useState(false);
  const [loi, setLoi] = useState<string | null>(null);

  const cho = timChoCon(choConBan, loaiXeId);
  const canhBao = canhBaoChoKhach(cho);

  async function gui(e: FormEvent) {
    e.preventDefault();
    if (bienSo.replace(/[^0-9a-z]/gi, '').length < 7) { setLoi('Biển số phải có ít nhất 7 ký tự chữ và số.'); return; }
    const loiAnh = kiemTraAnh(anh);
    if (loiAnh) { setLoi(loiAnh); return; }
    setDangGui(true); setLoi(null);
    try { await onGui(loaiXeId, bienSo, chiaSe, anh!); }
    catch (er) { setLoi((er as Error).message); }
    finally { setDangGui(false); }
  }

  return (
    <form className="the form" onSubmit={gui}>
      <h3>Đăng ký xe mới</h3>
      <label>Loại xe
        <select value={loaiXeId} onChange={(e) => setLoaiXeId(Number(e.target.value))}>
          <option value={2}>Xe máy</option>
          <option value={1}>Ô tô</option>
        </select>
      </label>
      {canhBao && <div className={`thong-bao ${hetCho(cho) ? 'thong-bao-canh-bao' : 'thong-bao-tin'}`}>{canhBao}</div>}
      <label>Biển số
        <input value={bienSo} onChange={(e) => setBienSo(e.target.value)} placeholder="VD: 29B1-123.45" autoFocus />
      </label>
      <label>Ảnh giấy đăng ký xe (cà vẹt)
        <ChonAnh tep={anh} onChon={setAnh} />
      </label>
      <label className="check"><input type="checkbox" checked={chiaSe} onChange={(e) => setChiaSe(e.target.checked)} /> Chia sẻ xe cho nhóm gia đình</label>
      {loi && <div className="thong-bao thong-bao-loi">{loi}</div>}
      <div className="hang-nut">
        <button className="nut" disabled={dangGui}>{dangGui ? 'Đang gửi…' : 'Gửi đăng ký'}</button>
        <button type="button" className="nut nut-phu" onClick={onHuy}>Huỷ</button>
      </div>
    </form>
  );
}
