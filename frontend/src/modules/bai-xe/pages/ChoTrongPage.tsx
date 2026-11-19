import { DangTai, ThongBao } from '@/shared/ui/ui';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';
import { useApp } from '@/app/AppContext';
import { dinhDangNgay, tenLoaiXe } from '@/shared/utils/format';
import { TEN_TRANG_THAI_O } from '../models';

/** Trang khách "Chỗ trống": mỗi khu còn bao nhiêu ô trống. Không hiện biển số hay chủ xe. Tự tải lại mỗi 30 giây. */
export function ChoTrongPage() {
  const { services } = useApp();
  const ct = useTaiDuLieu(() => services.baiXe.layChoTrong(), [], 30_000);

  return (
    <>
      <div className="tieu-de-trang">
        <h1>Chỗ trống trong bãi</h1>
        <button className="nut nut-phu nut-nho" onClick={() => ct.taiLai()} disabled={ct.dangTai}>Tải lại</button>
      </div>
      {ct.loi && <ThongBao loai="loi">{ct.loi}</ThongBao>}
      {ct.dangTai && !ct.duLieu && <DangTai />}
      {ct.duLieu && (
        <>
          <p className="mo">Cập nhật lúc {dinhDangNgay(ct.duLieu.capNhatLuc)}. "Không rõ" là ô camera chưa báo hoặc báo đã quá 15 phút.</p>
          <div className="chu-thich">
            <span><i className="o-mau o-tt-TRONG" />Trống</span>
            <span><i className="o-mau o-tt-CO_XE" />Có xe</span>
            <span><i className="o-mau o-tt-KHONG_RO" />Không rõ</span>
          </div>
          {ct.duLieu.khu.map((k) => (
            <section key={k.id} className="the khu">
              <div className="xe-dau">
                <h3>{k.ten} · {tenLoaiXe(k.loaiXeId)}</h3>
                {k.soO > 0
                  ? <span className={`nhan ${k.soTrong > 0 ? 'nhan-ok' : 'nhan-loi'}`}>{k.soTrong > 0 ? `${k.soTrong}/${k.soO} ô trống` : 'Hết ô trống'}</span>
                  : <span className="mo">Khu không chia ô</span>}
              </div>
              {k.soO > 0 && (
                <div className="luoi-o luoi-o-nho">
                  {k.o.map((o) => (
                    <div key={o.id} className={`o-do o-tt-${o.trangThai}`} title={TEN_TRANG_THAI_O[o.trangThai]}>
                      <div className="o-ma">{o.maO}</div>
                      <div>{TEN_TRANG_THAI_O[o.trangThai]}</div>
                    </div>
                  ))}
                </div>
              )}
            </section>
          ))}
        </>
      )}
    </>
  );
}
