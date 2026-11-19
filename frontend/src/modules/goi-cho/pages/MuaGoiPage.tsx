import { useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { ChoConBanThe } from '../components/GoiChoUi';
import { DangTai, ThongBao } from '@/shared/ui/ui';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';
import { useApp } from '@/app/AppContext';
import { LoiApi } from '@/shared/models';
import type { DangKyCho, KetQuaMuaGoi, XemHuyGoi } from '../models';
import { dinhDangNgay, dinhDangTien, hienThiBienSo, tenLoaiXe } from '@/shared/utils/format';

export function MuaGoiPage() {
  const { services } = useApp();
  const [params, setParams] = useSearchParams();
  const dsXe = useTaiDuLieu(() => services.xe.layDanhSach());
  const bangGia = useTaiDuLieu(() => services.goiCho.layBangGia());

  const dsDuyet = useMemo(() => dsXe.duLieu?.filter((x) => x.trangThaiDuyet === 'DA_DUYET') ?? [], [dsXe.duLieu]);
  // ?xe= trỏ tới xe không mua được (chưa duyệt, bị gỡ, không còn thuộc tài khoản): KHÔNG tự chọn xe khác, báo rõ để không mua nhầm
  const xeYeuCau = params.get('xe');
  const xeDuocChon = useMemo(
    () => (xeYeuCau !== null ? dsDuyet.find((x) => x.xeId === Number(xeYeuCau)) ?? null : dsDuyet[0] ?? null),
    [dsDuyet, xeYeuCau]);
  const xeKhongMuaDuoc = xeYeuCau !== null && !!dsXe.duLieu && !xeDuocChon;

  const lichSu = useTaiDuLieu(
    () => (xeDuocChon ? services.xe.layLichSuGoi(xeDuocChon.xeId) : Promise.resolve([])),
    [xeDuocChon?.xeId]);

  const [goiId, setGoiId] = useState<number | null>(null);
  const [dangMua, setDangMua] = useState(false);
  const [ketQua, setKetQua] = useState<KetQuaMuaGoi | null>(null);
  const [loi, setLoi] = useState<LoiApi | null>(null);
  // giá cũ của gói vừa bị Admin đổi giá, để hiện gạch ngang bên cạnh giá mới
  const [giaCu, setGiaCu] = useState<Record<number, number>>({});

  // Huỷ gói trước hạn
  const [huy, setHuy] = useState<{ dk: DangKyCho; xem: XemHuyGoi } | null>(null);
  const [dangHuy, setDangHuy] = useState(false);
  const [lyDoHuy, setLyDoHuy] = useState('');
  const [gopY, setGopY] = useState('');
  const [dsLyDo, setDsLyDo] = useState<string[]>([]);
  useEffect(() => { services.xe.layLyDoHuy().then(setDsLyDo).catch(() => setDsLyDo([])); }, [services]);
  const [thongBaoHuy, setThongBaoHuy] = useState<{ loai: 'ok' | 'loi'; noiDung: string } | null>(null);

  useEffect(() => { setGoiId(null); setKetQua(null); setLoi(null); setHuy(null); setThongBaoHuy(null); setGiaCu({}); }, [xeDuocChon?.xeId]);

  if (dsXe.dangTai && !dsXe.duLieu) return <DangTai />;
  if (dsXe.loi) return <ThongBao loai="loi">{dsXe.loi}</ThongBao>;
  if (xeKhongMuaDuoc) {
    const xe = dsXe.duLieu?.find((x) => x.xeId === Number(xeYeuCau));
    return (
      <>
        <h1>Mua / gia hạn gói</h1>
        <ThongBao loai="loi">
          {xe ? `Xe ${hienThiBienSo(xe.bienSo)} hiện không mua gói được (chưa được duyệt hoặc đã bị gỡ).`
              : 'Xe này hiện không mua gói được (không còn thuộc tài khoản của bạn hoặc đã bị gỡ).'}
        </ThongBao>
        {dsDuyet.length > 0 ? (
          <div className="hang-nut">
            <span className="mo">Chọn xe khác:</span>
            {dsDuyet.map((x) => (
              <button key={x.xeId} className="nut nut-phu nut-nho" onClick={() => setParams({ xe: String(x.xeId) })}>{hienThiBienSo(x.bienSo)}</button>
            ))}
          </div>
        ) : <p><Link to="/xe">Mở Xe của tôi</Link></p>}
      </>
    );
  }
  if (!xeDuocChon) return <ThongBao loai="tin">Chưa có xe đã duyệt. Vào <Link to="/xe">Xe của tôi</Link> để đăng ký xe.</ThongBao>;

  const cho = bangGia.duLieu?.choConBan.find((c) => c.loaiXeId === xeDuocChon.loaiXeId);
  const goiCuaLoai = bangGia.duLieu?.goi.filter((g) => g.loaiXeId === xeDuocChon.loaiXeId) ?? [];
  const goi = goiCuaLoai.find((g) => g.id === goiId) ?? null;
  // Xe đã giữ suất (gói còn hạn hoặc đang trong bãi) thì mua là gia hạn, không tốn thêm chỗ (giống sp_mua_goi).
  const giuSuat = xeDuocChon.conHan || xeDuocChon.dangTrongBai;
  const hetCho = !giuSuat && cho?.soChoConBan === 0;
  const batDauDuKien = xeDuocChon.conHan && xeDuocChon.hetHanLuc ? xeDuocChon.hetHanLuc : null;

  // Chỉ gói CUỐI CÙNG còn hiệu lực mới huỷ được (lịch sử đã sắp mới nhất lên đầu).
  const bayGio = Date.now();
  const goiHuyDuoc = lichSu.duLieu?.find((d) => d.trangThai === 'CON_HAN' && new Date(d.ngayKetThuc).getTime() > bayGio) ?? null;

  async function moHuy(dk: DangKyCho) {
    setThongBaoHuy(null);
    setLyDoHuy(''); setGopY('');
    try { setHuy({ dk, xem: await services.xe.xemHuyGoi(dk.id) }); }
    catch (e) { setThongBaoHuy({ loai: 'loi', noiDung: (e as Error).message }); }
  }

  async function xacNhanHuy() {
    if (!huy) return;
    setDangHuy(true);
    try {
      const kq = await services.xe.huyGoi(huy.dk.id, lyDoHuy, gopY.trim());
      setThongBaoHuy({ loai: 'ok', noiDung: `Đã huỷ gói ${kq.tenGoi}, hoàn lại ${dinhDangTien(kq.soTienHoan)}. Suất đã trả lại cho bãi. Cảm ơn bạn đã góp ý.` });
      setHuy(null);
      await Promise.all([dsXe.taiLai(), bangGia.taiLai(), lichSu.taiLai()]);
    } catch (e) {
      setThongBaoHuy({ loai: 'loi', noiDung: (e as Error).message });
      setHuy(null);
    } finally { setDangHuy(false); }
  }

  async function mua() {
    if (!goi) return;
    setDangMua(true); setLoi(null); setKetQua(null);
    try {
      // gửi kèm giá đang hiện trên màn hình: Admin vừa đổi giá thì server báo GIA_DA_DOI, không trừ tiền
      setKetQua(await services.goiCho.muaGoi(xeDuocChon!.xeId, goi.id, goi.gia));
      setGoiId(null);
      setGiaCu({});
      await Promise.all([dsXe.taiLai(), bangGia.taiLai(), lichSu.taiLai()]);
    } catch (e) {
      const l = e instanceof LoiApi ? e : new LoiApi(0, 'LOI', (e as Error).message);
      setLoi(l);
      if (l.ma === 'GIA_DA_DOI') {
        // tải lại bảng giá, giữ nguyên gói đang chọn để khách xem giá mới rồi bấm thanh toán lại
        setGiaCu({ [goi.id]: goi.gia });
        await bangGia.taiLai();
      }
    } finally { setDangMua(false); }
  }

  return (
    <>
      <h1>Mua / gia hạn gói</h1>

      <div className="the">
        <label>Chọn xe
          <select value={xeDuocChon.xeId} onChange={(e) => setParams({ xe: e.target.value })}>
            {dsXe.duLieu!.filter((x) => x.trangThaiDuyet === 'DA_DUYET').map((x) => (
              <option key={x.xeId} value={x.xeId}>
                {hienThiBienSo(x.bienSo)} · {tenLoaiXe(x.loaiXe)}{!x.laChuXe ? ` · xe của ${x.tenChuXe}` : ''}
              </option>
            ))}
          </select>
        </label>
        <div className="xe-tt ngang">
          <div><dt>Gói hiện tại</dt><dd>{xeDuocChon.goiHienTai ?? 'Chưa có'}</dd></div>
          <div><dt>Hết hạn</dt><dd>{dinhDangNgay(xeDuocChon.hetHanLuc)}</dd></div>
          <div><dt>Trạng thái</dt><dd>{giuSuat ? 'Đang giữ suất' : 'Chưa giữ suất'}</dd></div>
        </div>
        {!xeDuocChon.laChuXe && <ThongBao loai="canh-bao">Đây là xe được chia sẻ. Theo quy định, chỉ chủ xe mới được mua gói.</ThongBao>}
      </div>

      {cho && <ChoConBanThe c={cho} />}
      {hetCho && <ThongBao loai="loi">Loại xe này đã hết chỗ. Xe chưa giữ suất nên chưa mua được gói mới.</ThongBao>}

      <h2>Chọn gói</h2>
      <section className="luoi-goi">
        {goiCuaLoai.map((g) => (
          <button key={g.id} type="button" className={`the goi ${goiId === g.id ? 'goi-chon' : ''}`} onClick={() => { setGoiId(g.id); setKetQua(null); setLoi(null); setGiaCu({}); }}>
            <div className="goi-ten">{g.ten}</div>
            <div className="goi-gia">
              {giaCu[g.id] !== undefined && giaCu[g.id] !== g.gia && <span className="gia-cu">{dinhDangTien(giaCu[g.id])}</span>}
              {dinhDangTien(g.gia)}
            </div>
            <div className="mo">{g.soNgay} ngày</div>
          </button>
        ))}
      </section>

      {goi && (
        <div className="the xac-nhan">
          <h3>Xác nhận thanh toán</h3>
          <p>
            {batDauDuKien
              ? <>Gia hạn <strong>{goi.ten}</strong>, nối tiếp sau ngày hết hạn {dinhDangNgay(batDauDuKien)}. Không tốn thêm chỗ.</>
              : <>Mua mới <strong>{goi.ten}</strong>, bắt đầu ngay khi thanh toán.</>}
          </p>
          <p>Số tiền: <strong>{dinhDangTien(goi.gia)}</strong> <span className="mo">(thanh toán giả lập)</span></p>
          <div className="hang-nut">
            <button className="nut" onClick={mua} disabled={dangMua}>{dangMua ? 'Đang xử lý…' : 'Thanh toán'}</button>
            <button className="nut nut-phu" onClick={() => setGoiId(null)}>Huỷ</button>
          </div>
        </div>
      )}

      {ketQua && (
        <ThongBao loai="ok">
          {ketQua.laGiaHan ? 'Gia hạn' : 'Mua gói'} thành công: hiệu lực từ {dinhDangNgay(ketQua.ngayBatDau)} đến {dinhDangNgay(ketQua.ngayKetThuc)}, đã trả {dinhDangTien(ketQua.soTien)}.
        </ThongBao>
      )}
      {loi && (
        <ThongBao loai={loi.ma === 'GIA_DA_DOI' ? 'canh-bao' : 'loi'}>
          {loi.message}
          {loi.ma === 'CON_NO_QUA_HAN' && <> <Link to={`/phi-qua-han/${xeDuocChon.xeId}`}>Trả phí quá hạn ngay</Link></>}
          {loi.ma === 'GIA_DA_DOI' && <> Bảng giá bên trên đã cập nhật.</>}
        </ThongBao>
      )}

      <h2>Lịch sử gói của xe</h2>
      {thongBaoHuy && <ThongBao loai={thongBaoHuy.loai}>{thongBaoHuy.noiDung}</ThongBao>}
      {huy && (
        <div className="the xac-nhan xac-nhan-nguy">
          <h3>Huỷ gói {huy.xem.tenGoi} trước hạn?</h3>
          <p>
            {huy.xem.chuaBatDau
              ? <>Gói chưa bắt đầu nên được hoàn <strong>toàn bộ {dinhDangTien(huy.xem.soTienHoan)}</strong>.</>
              : <>Còn {huy.xem.soNgayConLai} ngày chưa dùng, được hoàn <strong>{dinhDangTien(huy.xem.soTienHoan)}</strong> (tính theo ngày, làm tròn xuống).</>}
          </p>
          {!huy.xem.chuaBatDau && xeDuocChon.dangTrongBai && (
            <ThongBao loai="canh-bao">Xe đang trong bãi: phải đưa xe ra trước mới huỷ được gói đang dùng.</ThongBao>
          )}
          <div className="form gop-y">
            <label>
              Lý do huỷ (không bắt buộc)
              <select value={lyDoHuy} onChange={(e) => setLyDoHuy(e.target.value)}>
                <option value="">Không chọn</option>
                {dsLyDo.map((l) => <option key={l} value={l}>{l}</option>)}
              </select>
            </label>
            <label>
              Góp ý cho nhà xe (không bắt buộc)
              <textarea value={gopY} maxLength={500} rows={3} onChange={(e) => setGopY(e.target.value)}
                        placeholder="VD: nên có gói 3 tháng, lối vào khu B hơi tối…" />
              <span className="mo">{gopY.length}/500 · Admin sẽ đọc được góp ý này</span>
            </label>
          </div>
          <div className="hang-nut">
            <button className="nut nut-nguy" onClick={xacNhanHuy} disabled={dangHuy}>{dangHuy ? 'Đang huỷ…' : 'Xác nhận huỷ gói'}</button>
            <button className="nut nut-phu" onClick={() => setHuy(null)}>Không huỷ</button>
          </div>
        </div>
      )}
      {lichSu.duLieu && lichSu.duLieu.length === 0 && <p className="mo">Xe chưa mua gói nào.</p>}
      {lichSu.duLieu && lichSu.duLieu.length > 0 && (
        <div className="bang-cuon">
          <table className="bang">
            <thead><tr><th>Gói</th><th>Bắt đầu</th><th>Kết thúc</th><th>Số tiền</th><th>Trạng thái</th><th></th></tr></thead>
            <tbody>
              {lichSu.duLieu.map((d) => (
                <tr key={d.id}>
                  <td>{d.tenGoi}</td><td>{dinhDangNgay(d.ngayBatDau)}</td><td>{dinhDangNgay(d.ngayKetThuc)}</td>
                  <td>{dinhDangTien(d.soTien)}</td>
                  <td>{tenTrangThaiGoi(d)}</td>
                  <td>
                    {xeDuocChon.laChuXe && goiHuyDuoc?.id === d.id && (
                      <button className="nut nut-phu nut-nho" onClick={() => moHuy(d)}>Huỷ gói</button>
                    )}
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

function tenTrangThaiGoi(d: DangKyCho) {
  if (d.trangThai === 'HUY') return d.huyBoiAdmin ? `Admin huỷ: ${d.lyDoHuy ?? 'không ghi lý do'}` : `Bạn đã huỷ${d.lyDoHuy ? ` (${d.lyDoHuy})` : ''}`;
  const bayGio = Date.now();
  if (new Date(d.ngayKetThuc).getTime() <= bayGio) return 'Hết hạn';
  if (new Date(d.ngayBatDau).getTime() > bayGio) return 'Chưa bắt đầu';
  return 'Đang dùng';
}
