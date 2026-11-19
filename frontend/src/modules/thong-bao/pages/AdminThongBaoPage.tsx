import { DangTai, ThongBao } from '@/shared/ui/ui';
import { useTaiDuLieu } from '@/shared/ui/useTaiDuLieu';
import { LOI_KHONG_MO_DUOC, useThamSoMotLan } from '@/shared/ui/useDichDen';
import { useApp } from '@/app/AppContext';
import type { ThongBaoApp } from '../models';
import { DanhSachThongBao } from '../components/DanhSachThongBao';
import { useMoThongBao } from '../useMoThongBao';

/** Hộp thư Admin: khách huỷ gói (lý do, góp ý), nghi mất xe, cổng ra không mở vì hết giờ. Dữ liệu từ GET /api/admin/thong-bao. */
export function AdminThongBaoPage() {
  const { services } = useApp();
  const hop = useTaiDuLieu(() => services.thongBao.layHopThuAdmin(), [], 15_000);
  const [{ loi: loiUrl }, boLoiUrl] = useThamSoMotLan('loi');
  const mo = useMoThongBao();

  async function bam(t: ThongBaoApp) {
    boLoiUrl();
    if (!(await mo(t))) await hop.taiLai();
  }

  return (
    <>
      <div className="tieu-de-trang">
        <h1>Thông báo</h1>
        <button className="nut nut-phu nut-nho" onClick={() => hop.taiLai()} disabled={hop.dangTai}>Tải lại</button>
      </div>
      <p className="mo">Khách tự huỷ gói (kèm lý do, góp ý), xe chờ duyệt, người dùng xe báo "Không phải tôi" ở cổng ra, xe chờ quá 60 giây không ai xác nhận, đối soát khu. Bấm vào thông báo để mở đúng chỗ cần xử lý.</p>
      {loiUrl === LOI_KHONG_MO_DUOC && <ThongBao loai="loi">Không mở được nội dung này (không còn tồn tại hoặc không có quyền xem).</ThongBao>}
      {hop.loi && <ThongBao loai="loi">{hop.loi}</ThongBao>}
      {hop.dangTai && !hop.duLieu && <DangTai />}
      {hop.duLieu && hop.duLieu.thongBao.length === 0 && <ThongBao loai="tin">Chưa có thông báo nào.</ThongBao>}
      {hop.duLieu && hop.duLieu.thongBao.length > 0 && (
        <>
          <p><strong>{hop.duLieu.soChuaDoc}</strong> thông báo chưa đọc</p>
          <DanhSachThongBao ds={hop.duLieu.thongBao} laAdmin khiBam={(t) => void bam(t)} />
        </>
      )}
    </>
  );
}
