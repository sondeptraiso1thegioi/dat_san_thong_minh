import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { DangTai, ThongBao } from '@/shared/ui/ui';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';
import { useApp } from '@/app/AppContext';
import { useVeKhongMoDuoc } from '@/shared/ui/useDichDen';
import type { PhiQuaHan } from '../models';
import { dinhDangNgay, dinhDangTien, hienThiBienSo } from '@/shared/utils/format';

export function PhiQuaHanPage() {
  const { services } = useApp();
  const xeId = Number(useParams().xeId);
  const xe = useTaiDuLieu(async () => (await services.xe.layDanhSach()).find((x) => x.xeId === xeId) ?? null, [xeId]);
  const no = useTaiDuLieu(() => services.xe.layPhiQuaHan(xeId), [xeId]);
  const [dangTra, setDangTra] = useState(false);
  const [daTra, setDaTra] = useState<PhiQuaHan | null>(null);
  const [loi, setLoi] = useState<string | null>(null);

  // Xe không còn thuộc tài khoản (mở từ thông báo cũ): về hộp thư, báo không mở được
  const veKhongMoDuoc = useVeKhongMoDuoc();
  useEffect(() => { if (xe.duLieu === null && !xe.dangTai && !xe.loi) veKhongMoDuoc(); }, [xe.duLieu, xe.dangTai, xe.loi, veKhongMoDuoc]);

  async function tra() {
    setDangTra(true); setLoi(null);
    try { setDaTra(await services.xe.traPhiQuaHan(xeId)); await no.taiLai(); }
    catch (e) { setLoi((e as Error).message); }
    finally { setDangTra(false); }
  }

  if (no.dangTai && !no.duLieu && !daTra) return <DangTai />;

  return (
    <>
      <h1>Trả phí quá hạn {xe.duLieu && <span className="mo">· {hienThiBienSo(xe.duLieu.bienSo)}</span>}</h1>
      {no.loi && <ThongBao loai="loi">{no.loi}</ThongBao>}

      {daTra && (
        <ThongBao loai="ok">
          Đã thanh toán {dinhDangTien(daTra.soTienConNo)} cho {daTra.soNgayConNo} ngày quá hạn.{' '}
          {xe.duLieu?.laChuXe && <Link to={`/mua-goi?xe=${xeId}`}>Mua gói mới cho xe này</Link>}
        </ThongBao>
      )}

      {no.duLieu ? (
        <div className="the">
          <dl className="xe-tt">
            <div><dt>Gói hết hạn lúc</dt><dd>{dinhDangNgay(no.duLieu.hetHanLuc)}</dd></div>
            <div><dt>Số ngày quá hạn</dt><dd>{no.duLieu.soNgayConNo} ngày</dd></div>
            <div><dt>Số tiền</dt><dd className="chu-loi lon">{dinhDangTien(no.duLieu.soTienConNo)}</dd></div>
          </dl>
          <p className="mo">Quá hạn dưới 1 ngày vẫn tính tròn 1 ngày. Phải trả hết phí quá hạn mới mua gói mới hoặc ra bãi được.</p>
          {loi && <ThongBao loai="loi">{loi}</ThongBao>}
          <button className="nut nut-nguy" onClick={tra} disabled={dangTra}>{dangTra ? 'Đang xử lý…' : 'Thanh toán (giả lập)'}</button>
        </div>
      ) : (
        !daTra && <ThongBao loai="tin">Xe này không còn phí quá hạn (khoản phí đã được thanh toán hoặc gói đã gia hạn). <Link to="/ra-vao">Mở Ra vào</Link> · <Link to="/xe">Xe của tôi</Link></ThongBao>
      )}
    </>
  );
}
