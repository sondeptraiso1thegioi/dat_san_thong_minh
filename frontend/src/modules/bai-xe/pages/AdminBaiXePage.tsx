import { useState } from 'react';
import { Link } from 'react-router-dom';
import { DangTai, ThongBao } from '@/shared/ui/ui';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';
import { useApp } from '@/app/AppContext';
import type { Khu, SucChuaBai, TinhTrangGoi, XeTrongBai } from '../models';
import { conLai, dinhDangNgay, dinhDangTien, hienThiBienSo, tenLoaiXe } from '@/shared/utils/format';

const tenTinhTrang: Record<TinhTrangGoi, string> = {
  CON_HAN: 'Còn hạn',
  SAP_HET: 'Sắp hết (dưới 72 giờ)',
  QUA_HAN: 'Quá hạn',
  KHONG_CO_GOI: 'Không có gói',
};

/**
 * Trang Admin "Sơ đồ bãi": sức chứa ô tô / xe máy, các khu và ô, xe nào đang ở ô nào, còn hạn bao lâu.
 * Trang chỉ điều phối; mỗi phần vẽ là một component nhỏ (SRP). Dữ liệu thật từ GET /api/admin/bai.
 */
export function AdminBaiXePage() {
  const { services } = useApp();
  const bai = useTaiDuLieu(() => services.baiXe.laySoDoBai());
  const [tim, setTim] = useState('');
  const timChuan = tim.toUpperCase().replace(/[^0-9A-Z]/g, '');
  const khop = (x: XeTrongBai | null) => !!x && timChuan.length >= 2 && x.bienSo.includes(timChuan);
  const xeTimThay = bai.duLieu?.xeTrongBai.filter(khop) ?? [];

  return (
    <>
      <div className="tieu-de-trang">
        <h1>Sơ đồ bãi</h1>
        <button className="nut nut-phu nut-nho" onClick={() => bai.taiLai()} disabled={bai.dangTai}>Tải lại</button>
      </div>
      {bai.loi && <ThongBao loai="loi">{bai.loi}</ThongBao>}
      {bai.dangTai && !bai.duLieu && <DangTai />}
      {bai.duLieu && (
        <>
          <p className="mo">Cập nhật lúc {dinhDangNgay(bai.duLieu.capNhatLuc)}. Vị trí xe lấy từ lần quét camera gần nhất.</p>

          <h2>Sức chứa</h2>
          <p className="mo">Đổi sức chứa và giá gói ở trang <Link to="/admin/goi-gia">Gói &amp; giá</Link> (module Đăng ký xe và mua gói).</p>
          <div className="luoi-suc-chua">
            {bai.duLieu.sucChua.map((s) => <SucChuaThe key={s.loaiXeId} s={s} />)}
          </div>

          <h2>Các khu</h2>
          <div className="o-ly-do">
            <input value={tim} onChange={(e) => setTim(e.target.value)} placeholder="Tìm biển số, VD: 30A12345" />
            {timChuan.length >= 2 && (
              <span className="mo">
                {xeTimThay.length === 0 ? 'Không thấy xe này trong bãi.'
                  : xeTimThay.map((x) => `${hienThiBienSo(x.bienSo)}: ${x.khu ? `${x.khu}${x.maO ? `, ô ${x.maO}` : ''}` : 'chưa định vị'}`).join(' · ')}
              </span>
            )}
          </div>
          <ChuThich />
          {bai.duLieu.khu.map((k) => <KhuThe key={k.id} k={k} khop={khop} />)}

          {bai.duLieu.xeChuaDinhVi.length > 0 && (
            <ThongBao loai="canh-bao">
              {bai.duLieu.xeChuaDinhVi.length} xe trong bãi chưa định vị được (camera chưa đọc thấy biển):{' '}
              {bai.duLieu.xeChuaDinhVi.map((x) => hienThiBienSo(x.bienSo)).join(', ')}
            </ThongBao>
          )}

          <h2>Tất cả xe đang trong bãi ({bai.duLieu.xeTrongBai.length})</h2>
          <BangXeTrongBai ds={bai.duLieu.xeTrongBai} khop={khop} />
        </>
      )}
    </>
  );
}

function SucChuaThe({ s }: { s: SucChuaBai }) {
  const phanTram = s.sucChua ? Math.min(100, Math.round((s.dangChiem / s.sucChua) * 100)) : 100;

  return (
    <div className={`the suc-chua ${s.hetCho ? 'cho-het' : ''}`}>
      <div className="xe-dau">
        <h3>{tenLoaiXe(s.loaiXe)}</h3>
        <span className={`nhan ${s.hetCho ? 'nhan-loi' : 'nhan-ok'}`}>{s.hetCho ? 'Hết chỗ' : `Còn ${s.soChoConBan} chỗ`}</span>
      </div>
      <div className="thanh"><div className="thanh-day" style={{ width: `${phanTram}%` }} /></div>
      <dl className="so-lieu">
        <div><dt>Sức chứa</dt><dd>{s.sucChua}</dd></div>
        <div><dt>Giữ suất (gói còn hạn)</dt><dd>{s.soXeGiuSuat}</dd></div>
        <div><dt>Quá hạn còn trong bãi</dt><dd>{s.soXeQuaHanTrongBai}</dd></div>
        <div><dt>Xe đang trong bãi</dt><dd>{s.soXeTrongBai}</dd></div>
        <div><dt>Ô trên sơ đồ</dt><dd>{s.soODo || 'Không chia ô'}</dd></div>
      </dl>
      {s.soODo > 0 && s.sucChua > s.soODo && (
        <p className="chu-loi">Sức chứa đang lớn hơn số ô thật ({s.soODo}).</p>
      )}
    </div>
  );
}

function ChuThich() {
  return (
    <div className="chu-thich">
      <span><i className="o-mau o-trong" />Trống</span>
      <span><i className="o-mau tt-CON_HAN" />Còn hạn</span>
      <span><i className="o-mau tt-SAP_HET" />Sắp hết (dưới 72 giờ)</span>
      <span><i className="o-mau tt-QUA_HAN" />Quá hạn / không có gói</span>
    </div>
  );
}

function KhuThe({ k, khop }: { k: Khu; khop: (x: XeTrongBai | null) => boolean }) {
  return (
    <section className="the khu">
      <div className="xe-dau">
        <h3>{k.ten} · {tenLoaiXe(k.loaiXe)}</h3>
        <span className="mo">{k.soO > 0 ? `${k.soOTrong}/${k.soO} ô trống · ` : ''}{k.soXe} xe</span>
      </div>
      {k.soO > 0 && (
        <div className="luoi-o">
          {k.o.map((o) => (
            <div key={o.id}
                 className={`o-do ${o.xe ? `tt-${mau(o.xe.tinhTrangGoi)}` : 'o-trong'} ${khop(o.xe) ? 'o-tim-thay' : ''}`}
                 title={o.xe ? `${o.xe.tenChuXe} · vào ${dinhDangNgay(o.xe.thoiGianVao)}` : 'Ô trống'}>
              <div className="o-ma">{o.maO}</div>
              {o.xe ? <XeTomTat x={o.xe} />
                : o.trangThai === 'CO_XE' ? <div className="chu-loi">Camera thấy có xe, chưa rõ xe nào</div>
                : <div className="mo">{o.trangThai === 'KHONG_RO' ? 'Không rõ' : 'Trống'}</div>}
            </div>
          ))}
        </div>
      )}
      {k.xeChuaRoO.length > 0 && (
        <>
          <p className="mo">{k.soO > 0 ? 'Xe trong khu nhưng chưa rõ ô:' : 'Khu không chia ô, xe đang trong khu:'}</p>
          <div className="luoi-o">
            {k.xeChuaRoO.map((x) => (
              <div key={x.luotGuiId} className={`o-do tt-${mau(x.tinhTrangGoi)} ${khop(x) ? 'o-tim-thay' : ''}`}>
                <XeTomTat x={x} />
              </div>
            ))}
          </div>
        </>
      )}
      {k.soO === 0 && k.xeChuaRoO.length === 0 && <p className="mo">Không có xe.</p>}
    </section>
  );
}

function XeTomTat({ x }: { x: XeTrongBai }) {
  return (
    <>
      <div className="bien-so nho">{hienThiBienSo(x.bienSo)}</div>
      <div className="o-chu">{x.tenChuXe}</div>
      <div className="o-chu"><strong>{conLai(x.hetHanLuc)}</strong></div>
      {x.soTienNo > 0 && <div className="o-chu chu-loi">Nợ {dinhDangTien(x.soTienNo)}</div>}
    </>
  );
}

function BangXeTrongBai({ ds, khop }: { ds: XeTrongBai[]; khop: (x: XeTrongBai | null) => boolean }) {
  if (ds.length === 0) return <ThongBao loai="tin">Bãi đang trống.</ThongBao>;
  return (
    <div className="bang-cuon">
      <table className="bang">
        <thead><tr><th>Biển số</th><th>Loại</th><th>Chủ xe</th><th>Vị trí</th><th>Vào lúc</th><th>Hết hạn</th><th>Tình trạng</th><th>Nợ quá hạn</th></tr></thead>
        <tbody>
          {ds.map((x) => (
            <tr key={x.luotGuiId} className={khop(x) ? 'dong-tim-thay' : ''}>
              <td className="bien-so nho">{hienThiBienSo(x.bienSo)}</td>
              <td>{tenLoaiXe(x.loaiXe)}</td>
              <td>{x.tenChuXe}</td>
              <td>{x.khu ? `${x.khu}${x.maO ? ` · ô ${x.maO}` : ''}` : <span className="chu-loi">Chưa định vị</span>}</td>
              <td>{dinhDangNgay(x.thoiGianVao)}</td>
              <td>{dinhDangNgay(x.hetHanLuc)}</td>
              <td><span className={`nhan nhan-${mau(x.tinhTrangGoi)}`}>{tenTinhTrang[x.tinhTrangGoi]}</span> <span className="mo">{conLai(x.hetHanLuc)}</span></td>
              <td>{x.soTienNo > 0 ? <span className="chu-loi">{dinhDangTien(x.soTienNo)} ({x.soNgayNo} ngày)</span> : '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** Không có gói và quá hạn cùng một màu đỏ: đều là xe đang chiếm chỗ mà không có suất. */
const mau = (t: TinhTrangGoi) => (t === 'KHONG_CO_GOI' ? 'QUA_HAN' : t);
