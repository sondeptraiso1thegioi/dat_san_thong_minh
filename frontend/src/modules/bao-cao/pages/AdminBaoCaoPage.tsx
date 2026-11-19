import { useState } from 'react';
import { DangTai, ThongBao } from '@/shared/ui/ui';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';
import { useApp } from '@/app/AppContext';
import { dinhDangNgay, dinhDangTien, tenLoaiXe } from '@/shared/utils/format';

type Khoang = 'hom-nay' | '7-ngay' | '30-ngay';
const TEN_KHOANG: Record<Khoang, string> = { 'hom-nay': 'Hôm nay', '7-ngay': '7 ngày', '30-ngay': '30 ngày' };

/** Mốc bắt đầu theo giờ địa phương, gửi dạng "yyyy-MM-ddTHH:mm:ss" (server dùng giờ địa phương của bãi). */
function tuNgay(k: Khoang): string {
  const d = new Date();
  d.setHours(0, 0, 0, 0);
  if (k === '7-ngay') d.setDate(d.getDate() - 6);
  if (k === '30-ngay') d.setDate(d.getDate() - 29);
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T00:00:00`;
}

const phanTram = (v: number | null) => (v == null ? '—' : `${v.toLocaleString('vi-VN')}%`);

/** Báo cáo Admin: chỉ đọc, số liệu ghép từ các module qua Contracts (không có bảng riêng). */
export function AdminBaoCaoPage() {
  const { services } = useApp();
  const [khoang, setKhoang] = useState<Khoang>('hom-nay');
  const bc = useTaiDuLieu(() => services.baoCao.lay(tuNgay(khoang)), [khoang]);
  const d = bc.duLieu;

  return (
    <>
      <div className="tieu-de-trang">
        <h1>Báo cáo</h1>
        <div className="tab tab-gon" role="tablist">
          {(Object.keys(TEN_KHOANG) as Khoang[]).map((k) => (
            <button key={k} role="tab" aria-selected={khoang === k} className={khoang === k ? 'chon' : ''} onClick={() => setKhoang(k)}>{TEN_KHOANG[k]}</button>
          ))}
        </div>
      </div>
      {bc.loi && <ThongBao loai="loi">{bc.loi}</ThongBao>}
      {bc.dangTai && !d && <DangTai />}
      {d && (
        <>
          <p className="mo">Từ {dinhDangNgay(d.tu)} tới {dinhDangNgay(d.den)}</p>
          <div className="luoi-so">
            <O so={d.cong.soLuotVao.toLocaleString('vi-VN')} ten="Lượt vào" />
            <O so={d.cong.soLuotRa.toLocaleString('vi-VN')} ten="Lượt ra" />
            <O so={dinhDangTien(d.doanhThu.thuc)} ten="Doanh thu thực" phu={`${d.doanhThu.soGiaoDich} giao dịch`} />
            <O so={phanTram(d.dinhVi.tiLeDinhVi)} ten="Định vị được khu" phu={`${d.dinhVi.soCoKhu}/${d.dinhVi.soLuot} lượt được mời vào khu`} />
            <O so={phanTram(d.tiLeAiTuDoc)} ten="AI tự đọc biển" phu={d.cong.doTinCayTb != null ? `tin cậy TB ${Math.round(d.cong.doTinCayTb * 100)}%` : undefined} />
            <O so={d.cong.soNghiMat.toLocaleString('vi-VN')} ten="Báo nghi mất" canh={d.cong.soNghiMat > 0} />
          </div>

          <div className="luoi-bao-cao">
            <section className="the">
              <h3>Cổng ra vào</h3>
              <dl className="so-lieu">
                <div><dt>Lượt bị từ chối</dt><dd>{d.cong.soTuChoi}</dd></div>
                <div><dt>AI tự đọc, không cần Admin</dt><dd>{d.cong.soAiTuDoc}</dd></div>
                <div><dt>Admin xử lý (gõ tay, chọn biển, mở tay)</dt><dd>{d.cong.soAdminXuLy}</dd></div>
                <div><dt>"Không phải tôi" (nghi mất)</dt><dd>{d.cong.soNghiMat}</dd></div>
              </dl>
            </section>
            <section className="the">
              <h3>Doanh thu</h3>
              <dl className="so-lieu">
                <div><dt>Mua / gia hạn gói</dt><dd>{dinhDangTien(d.doanhThu.muaGoi)}</dd></div>
                <div><dt>Phí quá hạn</dt><dd>{dinhDangTien(d.doanhThu.phiQuaHan)}</dd></div>
                <div><dt>Hoàn tiền khi huỷ gói</dt><dd>{d.doanhThu.hoanTien > 0 ? '−' : ''}{dinhDangTien(d.doanhThu.hoanTien)}</dd></div>
                <div><dt>Thực thu</dt><dd>{dinhDangTien(d.doanhThu.thuc)}</dd></div>
              </dl>
            </section>
            <section className="the">
              <h3>Định vị khu xe máy</h3>
              <dl className="so-lieu">
                <div><dt>Camera lối vào khu ghi</dt><dd>{d.dinhVi.soCameraGhi}</dd></div>
                <div><dt>Người dùng bấm "Đã đỗ xe"</dt><dd>{d.dinhVi.soNguoiDungBao}</dd></div>
                <div><dt>Admin gán tay</dt><dd>{d.dinhVi.soAdminGan}</dd></div>
              </dl>
              <p className="mo">Số lần ghi theo nguồn; một lượt có thể được ghi nhiều lần (chuyển khu, ra vào khu).</p>
            </section>
          </div>

          <h2>Sức chứa hiện tại</h2>
          <div className="luoi-khu">
            {d.sucChua.map((s) => {
              const dung = s.sucChua > 0 ? Math.min(100, (s.dangChiem / s.sucChua) * 100) : 100;
              return (
                <div key={s.loaiXeId} className="the khu-cho">
                  <div className="khu-cho-dau"><strong>{tenLoaiXe(s.loaiXe)}</strong><span className="mo">{s.sucChua} suất</span></div>
                  <div className="khu-cho-so"><span className="lon">{s.dangChiem}</span> <span className="mo">đang chiếm · {s.soChoConBan} còn bán</span></div>
                  <div className="thanh" role="meter" aria-valuemin={0} aria-valuemax={s.sucChua} aria-valuenow={s.dangChiem}><div className="thanh-day" style={{ width: `${dung}%` }} /></div>
                  {s.soXeQuaHanTrongBai > 0 && <div className="mo khu-cho-ct">{s.soXeQuaHanTrongBai} xe quá hạn còn trong bãi</div>}
                </div>
              );
            })}
          </div>
        </>
      )}
    </>
  );
}

function O({ so, ten, phu, canh }: { so: string; ten: string; phu?: string; canh?: boolean }) {
  return (
    <div className={`the o-so ${canh ? 'o-so-canh' : ''}`}>
      <div className="o-so-ten">{ten}</div>
      <div className="o-so-gia">{so}</div>
      {phu && <div className="mo o-so-phu">{phu}</div>}
    </div>
  );
}
