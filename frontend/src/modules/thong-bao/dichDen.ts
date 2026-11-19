// Một chỗ duy nhất quy đổi thông báo → trang cần tới. Trang Thông báo (khách, Admin) và thẻ nổi SignalR đều gọi file này.
// Backend chỉ lưu hành động nghiệp vụ (hanh_dong) + id đối tượng (tham_chieu_id), KHÔNG lưu đường dẫn trang.

/** Khớp NhaXe.Modules.ThongBao.Contracts.HanhDongThongBao (C#). Đổi bên này thì đổi bên kia. */
export const HANH_DONG_KHACH = [
  'XAC_NHAN_RA',   // luot_gui_id: xe đang ở cổng ra, xác nhận trong 60 giây
  'XEM_LUOT',      // luot_gui_id: xem lượt gửi trong lịch sử Ra vào
  'TRA_PHI',       // xe_id: trả phí quá hạn
  'DAN_DUONG',     // luot_gui_id: mở màn hình chỉ đường tới khu
  'XEM_VI_TRI',    // xe_id: xe đang đỗ ở khu nào (Tìm xe)
  'XEM_SO_DO',     // —: sơ đồ chỗ trống (bãi hết chỗ, khu đầy không có chỉ dẫn)
  'MUA_GOI',       // xe_id: mua / gia hạn gói, chọn sẵn xe
  'GUI_LAI_GIAY',  // xe_id: xe bị từ chối, tải lại ảnh giấy đăng ký
  'XEM_XE',        // xe_id: xe bị gỡ / gói bị huỷ
  'XEM_NHOM',      // nhóm: nhóm gia đình
  'DOI_MAT_KHAU',  // —: chưa có nơi gửi (dự phòng cho cảnh báo đăng nhập lạ)
] as const;

export const HANH_DONG_ADMIN = [
  'DUYET_XE',       // xe_id: xe chờ duyệt
  'XEM_XE_ADMIN',   // xe_id: khung chi tiết xe (khách huỷ gói)
  'XU_LY_CONG_RA',  // luot_gui_id: quá 60 giây / người dùng báo không phải họ / không khớp lượt
  'XEM_CONG_VAO',   // —: biển trùng lượt đang mở ở cổng vào
  'XEM_DI_SAI_KHU', // di_sai_khu_id: tô sáng dòng đi sai khu
  'XEM_SO_XE_KHU',  // —: số xe các khu (mọi khu đầy, người dùng báo khác camera)
  'XEM_CAMERA_KHU', // —: camera khu (xe sai loại đi qua)
] as const;

export type HanhDongKhach = (typeof HANH_DONG_KHACH)[number];
export type HanhDongAdmin = (typeof HANH_DONG_ADMIN)[number];
export type HanhDong = HanhDongKhach | HanhDongAdmin;

/** Thông báo cần người nhận LÀM gì đó thì có nhãn bên phải. Chỉ để biết thì không có nhãn. */
export const NHAN_HANH_DONG: Partial<Record<HanhDong, string>> = {
  XAC_NHAN_RA: 'Xác nhận ra',
  DAN_DUONG: 'Xem đường đi',
  TRA_PHI: 'Trả phí',
  MUA_GOI: 'Mua gói',
  GUI_LAI_GIAY: 'Gửi lại giấy',
  DOI_MAT_KHAU: 'Đổi mật khẩu',
  DUYET_XE: 'Duyệt xe',
  XU_LY_CONG_RA: 'Mở barie',
};

/** Phần thông báo đủ để quyết định nơi tới (dùng chung cho hộp thư và sự kiện SignalR). */
export interface ThongBaoDich {
  loai: string;
  noiDung: string;
  hanhDong?: string | null;
  thamChieuId?: number | null;
}

const laKhach = (h: string): h is HanhDongKhach => (HANH_DONG_KHACH as readonly string[]).includes(h);
const laAdminHd = (h: string): h is HanhDongAdmin => (HANH_DONG_ADMIN as readonly string[]).includes(h);

/** Thông báo cũ (trước khi có cột hanh_dong): đoán hành động từ loai + câu chữ. Không đoán được thì null. */
export function suyHanhDong(tb: ThongBaoDich, laAdmin: boolean): HanhDong | null {
  const nd = tb.noiDung.toLowerCase();
  if (laAdmin) {
    switch (tb.loai) {
      case 'XE_CHO_DUYET': return 'DUYET_XE';
      case 'KHACH_HUY': return 'XEM_XE_ADMIN';
      case 'RA': case 'NGHI_MAT': return 'XU_LY_CONG_RA';
      case 'DOI_SOAT':
        if (nd.includes('cổng vào')) return 'XEM_CONG_VAO';
        if (nd.includes('cổng ra')) return 'XU_LY_CONG_RA';
        if (nd.includes('camera') && nd.includes('không dành cho')) return 'XEM_CAMERA_KHU';
        if (nd.includes('đã đầy') && nd.includes('nhân viên không cho vào')) return 'XEM_DI_SAI_KHU';
        return 'XEM_SO_XE_KHU';
      default: return null;
    }
  }
  switch (tb.loai) {
    case 'RA': return nd.includes('đang ở cổng ra') ? 'XAC_NHAN_RA' : 'XEM_LUOT';
    case 'VAO': case 'NGHI_MAT': return 'XEM_LUOT';
    case 'CHO_THANH_TOAN': case 'QUA_HAN': return 'TRA_PHI';
    case 'KHU':
      if (nd.includes('hết chỗ') || nd.includes('sơ đồ')) return 'XEM_SO_DO';
      if (nd.includes('mời vào') || nd.includes('mời bạn sang')) return 'DAN_DUONG';
      return 'XEM_VI_TRI';
    case 'SAP_HET_HAN': case 'HET_HAN': return 'MUA_GOI';
    case 'KET_QUA_DUYET': return nd.includes('từ chối') ? 'GUI_LAI_GIAY' : 'MUA_GOI';
    case 'XE_BI_GO': case 'HUY_GOI': return 'XEM_XE';
    case 'NHOM': return 'XEM_NHOM';
    default: return null;
  }
}

/** Hành động hiệu lực: cột hanh_dong nếu có (và hợp lệ), không thì suy từ loai. */
export function hanhDongCua(tb: ThongBaoDich, laAdmin: boolean): HanhDong | null {
  if (tb.hanhDong) return laKhach(tb.hanhDong) || laAdminHd(tb.hanhDong) ? tb.hanhDong : null;
  return suyHanhDong(tb, laAdmin);
}

/**
 * Đường dẫn trang đích, kèm id trên URL để trang tô sáng đúng dòng / thẻ.
 * null: không có trang phù hợp (loại lạ) hoặc không có quyền (thông báo Admin mở ở tài khoản khách và ngược lại).
 */
export function duongDanCho(tb: ThongBaoDich, laAdmin: boolean): string | null {
  const hd = hanhDongCua(tb, laAdmin);
  if (!hd) return null;
  if (laAdmin ? !laAdminHd(hd) : !laKhach(hd)) return null;
  const id = tb.thamChieuId ?? null;
  const q = (ten: string) => (id === null ? '' : `?${ten}=${id}`);
  switch (hd) {
    case 'XAC_NHAN_RA': return id === null ? '/ra-vao' : `/ra-vao?luot=${id}&xacNhan=1`;
    case 'XEM_LUOT': return `/ra-vao${q('luot')}`;
    case 'TRA_PHI': return id === null ? '/ra-vao' : `/phi-qua-han/${id}`;
    case 'DAN_DUONG': return `/tim-xe${q('danDuong')}`;
    case 'XEM_VI_TRI': return `/tim-xe${q('xe')}`;
    case 'XEM_SO_DO': return '/so-do';
    case 'MUA_GOI': return `/mua-goi${q('xe')}`;
    case 'GUI_LAI_GIAY': case 'XEM_XE': return `/xe${q('xe')}`;
    case 'XEM_NHOM': return '/nhom-gia-dinh';
    case 'DOI_MAT_KHAU': return '/doi-mat-khau';
    case 'DUYET_XE': return id === null ? '/admin/duyet-xe' : `/admin/duyet-xe?tab=cho-duyet&xe=${id}`;
    case 'XEM_XE_ADMIN': return id === null ? '/admin/duyet-xe?tab=da-duyet' : `/admin/duyet-xe?tab=da-duyet&xe=${id}`;
    case 'XU_LY_CONG_RA': return `/admin/cong/ra${q('luot')}`;
    case 'XEM_CONG_VAO': return '/admin/cong/vao';
    case 'XEM_DI_SAI_KHU': return `/admin/quan-ly-bai${id === null ? '?muc=di-sai' : `?diSai=${id}`}`;
    case 'XEM_SO_XE_KHU': return '/admin/quan-ly-bai?muc=so-xe';
    case 'XEM_CAMERA_KHU': return '/admin/camera-khu';
  }
}

/** Nhãn hành động bên phải thông báo ("Xác nhận ra →"), hoặc null nếu chỉ để biết. */
export function nhanHanhDong(tb: ThongBaoDich, laAdmin: boolean): string | null {
  const hd = hanhDongCua(tb, laAdmin);
  return hd ? NHAN_HANH_DONG[hd] ?? null : null;
}
