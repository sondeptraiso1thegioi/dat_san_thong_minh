import { useState } from 'react';
import { ThemXeForm } from '../components/ThemXeForm';
import { ChoConBanThe } from '../components/GoiChoUi';
import { DangTai, ThongBao } from '@/shared/ui/ui';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';
import { XeThe } from '../components/XeThe';
import { useApp } from '@/app/AppContext';
import { soId, toSang, useKhiMoTuThongBao, useThamSoMotLan, useVeKhongMoDuoc } from '@/shared/ui/useDichDen';
import { hetCho, timChoCon } from '../utils/choCon';
import { hienThiBienSo, tenLoaiXe } from '@/shared/utils/format';

export function XeCuaToiPage() {
  const { services } = useApp();
  const ds = useTaiDuLieu(() => services.xe.layDanhSach());
  const bangGia = useTaiDuLieu(() => services.goiCho.layBangGia());
  const [moForm, setMoForm] = useState(false);
  const [thanhCong, setThanhCong] = useState<string | null>(null);
  const [loiAnh, setLoiAnh] = useState<string | null>(null);

  async function themXe(loaiXeId: number, bienSo: string, chiaSe: boolean, anh: File) {
    const { xeId } = await services.xe.them(loaiXeId, bienSo, chiaSe);
    setMoForm(false);
    try { await services.xe.taiAnhGiayDangKy(xeId, anh); }
    catch (e) {
      // xe đã tạo nhưng ảnh lỗi: báo để khách tải lại ngay trên thẻ xe
      setThanhCong(null); setLoiAnh(`Đã tạo xe nhưng chưa tải được ảnh: ${(e as Error).message} Hãy tải lại ảnh ở thẻ xe bên dưới.`);
      await ds.taiLai(); return;
    }
    const cho = timChoCon(bangGia.duLieu?.choConBan, loaiXeId);
    const nhacHetCho = hetCho(cho) ? ` Lưu ý: ${tenLoaiXe(loaiXeId)} đang hết chỗ, duyệt xong bạn chưa mua gói được cho tới khi có chỗ trống.` : '';
    setThanhCong(`Đã gửi đăng ký xe ${hienThiBienSo(bienSo.toUpperCase().replace(/[^0-9A-Z]/g, ''))}. Xe đang chờ Admin duyệt.${nhacHetCho}`);
    await Promise.all([ds.taiLai(), bangGia.taiLai()]);
  }

  // Mở từ thông báo (xe bị từ chối / bị gỡ / gói bị huỷ): ?xe=45 → tô sáng thẻ xe (có lý do, ô tải lại ảnh giấy)
  const [thamSo] = useThamSoMotLan('xe');
  const veKhongMoDuoc = useVeKhongMoDuoc();
  useKhiMoTuThongBao(thamSo, !!ds.duLieu, (t) => {
    const id = soId(t.xe);
    if (id !== null && !toSang(`xe-${id}`)) veKhongMoDuoc();
  });

  const cuaToi = ds.duLieu?.filter((x) => x.laChuXe) ?? [];
  const chiaSe = ds.duLieu?.filter((x) => !x.laChuXe) ?? [];

  return (
    <>
      <div className="tieu-de-trang">
        <h1>Xe của tôi</h1>
        {!moForm && <button className="nut" onClick={() => { setMoForm(true); setThanhCong(null); setLoiAnh(null); }}>+ Thêm xe</button>}
      </div>

      {bangGia.duLieu && (
        <section className="luoi-cho">{bangGia.duLieu.choConBan.map((c) => <ChoConBanThe key={c.loaiXeId} c={c} />)}</section>
      )}

      {moForm && <ThemXeForm onGui={themXe} onHuy={() => setMoForm(false)} choConBan={bangGia.duLieu?.choConBan} />}
      {thanhCong && <ThongBao loai="ok">{thanhCong}</ThongBao>}
      {loiAnh && <ThongBao loai="loi">{loiAnh}</ThongBao>}
      {ds.loi && <ThongBao loai="loi">{ds.loi}</ThongBao>}
      {ds.dangTai && !ds.duLieu && <DangTai />}

      {ds.duLieu && cuaToi.length === 0 && <p className="mo">Bạn chưa đăng ký xe nào.</p>}
      <section className="luoi-xe">{cuaToi.map((x) => <XeThe key={x.xeId} xe={x} onDoi={ds.taiLai} />)}</section>

      {chiaSe.length > 0 && (
        <>
          <h2>Xe được chia sẻ trong nhóm</h2>
          <section className="luoi-xe">{chiaSe.map((x) => <XeThe key={x.xeId} xe={x} />)}</section>
        </>
      )}
    </>
  );
}
