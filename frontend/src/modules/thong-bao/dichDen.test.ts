import { describe, expect, it } from 'vitest';
import { duongDanCho, HANH_DONG_ADMIN, HANH_DONG_KHACH, hanhDongCua, nhanHanhDong, type ThongBaoDich } from './dichDen';

const tb = (p: Partial<ThongBaoDich>): ThongBaoDich => ({ loai: 'RA', noiDung: '', ...p });

describe('duongDanCho: có hanhDong (thông báo mới)', () => {
  const khach: [string, number | null, string][] = [
    ['XAC_NHAN_RA', 123, '/ra-vao?luot=123&xacNhan=1'],
    ['XEM_LUOT', 123, '/ra-vao?luot=123'],
    ['TRA_PHI', 45, '/phi-qua-han/45'],
    ['DAN_DUONG', 123, '/tim-xe?danDuong=123'],
    ['XEM_VI_TRI', 45, '/tim-xe?xe=45'],
    ['XEM_SO_DO', null, '/so-do'],
    ['MUA_GOI', 45, '/mua-goi?xe=45'],
    ['GUI_LAI_GIAY', 45, '/xe?xe=45'],
    ['XEM_XE', 45, '/xe?xe=45'],
    ['XEM_NHOM', 7, '/nhom-gia-dinh'],
    ['DOI_MAT_KHAU', null, '/doi-mat-khau'],
  ];
  it.each(khach)('khách %s (%s) → %s', (hanhDong, thamChieuId, dich) => {
    expect(duongDanCho(tb({ hanhDong, thamChieuId }), false)).toBe(dich);
  });

  const admin: [string, number | null, string][] = [
    ['DUYET_XE', 45, '/admin/duyet-xe?tab=cho-duyet&xe=45'],
    ['XEM_XE_ADMIN', 45, '/admin/duyet-xe?tab=da-duyet&xe=45'],
    ['XU_LY_CONG_RA', 123, '/admin/cong/ra?luot=123'],
    ['XEM_CONG_VAO', null, '/admin/cong/vao'],
    ['XEM_DI_SAI_KHU', 9, '/admin/quan-ly-bai?diSai=9'],
    ['XEM_SO_XE_KHU', null, '/admin/quan-ly-bai?muc=so-xe'],
    ['XEM_CAMERA_KHU', null, '/admin/camera-khu'],
  ];
  it.each(admin)('Admin %s (%s) → %s', (hanhDong, thamChieuId, dich) => {
    expect(duongDanCho(tb({ hanhDong, thamChieuId }), true)).toBe(dich);
  });

  it('mọi hành động đều có đường dẫn (không sót case)', () => {
    for (const h of HANH_DONG_KHACH) expect(duongDanCho(tb({ hanhDong: h, thamChieuId: 1 }), false)).toMatch(/^\//);
    for (const h of HANH_DONG_ADMIN) expect(duongDanCho(tb({ hanhDong: h, thamChieuId: 1 }), true)).toMatch(/^\//);
  });

  it('thiếu id: về trang chung, không có tham số rỗng', () => {
    expect(duongDanCho(tb({ hanhDong: 'XAC_NHAN_RA' }), false)).toBe('/ra-vao');
    expect(duongDanCho(tb({ hanhDong: 'MUA_GOI' }), false)).toBe('/mua-goi');
    expect(duongDanCho(tb({ hanhDong: 'TRA_PHI' }), false)).toBe('/ra-vao');
  });

  it('không có quyền: hành động Admin ở tài khoản khách (và ngược lại) → null', () => {
    expect(duongDanCho(tb({ hanhDong: 'DUYET_XE', thamChieuId: 45 }), false)).toBeNull();
    expect(duongDanCho(tb({ hanhDong: 'MUA_GOI', thamChieuId: 45 }), true)).toBeNull();
  });

  it('hành động lạ → null', () => {
    expect(duongDanCho(tb({ hanhDong: 'LAM_GI_DO', thamChieuId: 1 }), false)).toBeNull();
  });
});

describe('duongDanCho: thông báo cũ (hanh_dong NULL) suy theo loai', () => {
  it.each([
    ['RA', 'Xe 36C239395 đang ở cổng ra. Mở mục Ra vào và xác nhận trong 60 giây nếu đúng là bạn.', '/ra-vao'],
    ['RA', 'Xe 36C239395 đã được An xác nhận ra bãi lúc 03:48 19/11/2026.', '/ra-vao'],
    ['VAO', 'Xe 36C239395 đã vào bãi lúc 03:42.', '/ra-vao'],
    ['KHU', 'Xe 36C239395: mời vào Khu R. Bãi giữ cho bạn 1 chỗ trong 5 phút, mở bản đồ để xem đường đi.', '/tim-xe'],
    ['KHU', 'Xe 36C239395: bãi xe máy đã hết chỗ (R, S, Hầm đều đầy).', '/so-do'],
    ['KHU', 'Đã đến bãi đỗ thành công: xe 36C239395 đang đỗ tại Hầm.', '/tim-xe'],
    ['SAP_HET_HAN', 'Gói Tháng của xe 36C239395 sẽ hết hạn…', '/mua-goi'],
    ['HET_HAN', 'Xe 36C239395 vừa tới cổng vào nhưng chưa có chỗ hoặc gói đã hết hạn.', '/mua-goi'],
    ['QUA_HAN', 'Xe 36C239395 đã quá hạn 2 ngày…', '/ra-vao'],
    ['KET_QUA_DUYET', 'Xe 36C239395 đã được duyệt, bạn có thể mua gói.', '/mua-goi'],
    ['KET_QUA_DUYET', 'Xe 36C239395 bị từ chối: ảnh mờ.', '/xe'],
    ['XE_BI_GO', 'Xe 36C239395 bị gỡ…', '/xe'],
    ['NHOM', 'Có người xin tham gia nhóm…', '/nhom-gia-dinh'],
  ])('khách %s "%s" → %s', (loai, noiDung, dich) => {
    expect(duongDanCho({ loai, noiDung }, false)).toBe(dich);
  });

  it.each([
    ['XE_CHO_DUYET', 'Có xe mới chờ duyệt: 36C239395', '/admin/duyet-xe'],
    ['KHACH_HUY', 'An đã huỷ gói…', '/admin/duyet-xe?tab=da-duyet'],
    ['RA', 'Xe 36C239395 ở cổng ra: quá 60 giây không ai xác nhận', '/admin/cong/ra'],
    ['DOI_SOAT', 'Xe 36C239395 tới cổng vào trong khi đang có lượt gửi chưa kết thúc.', '/admin/cong/vao'],
    ['DOI_SOAT', 'Xe 36C239395 vào bãi nhưng mọi khu xe máy đã đầy.', '/admin/quan-ly-bai?muc=so-xe'],
  ])('Admin %s "%s" → %s', (loai, noiDung, dich) => {
    expect(duongDanCho({ loai, noiDung }, true)).toBe(dich);
  });

  it('loại lạ → null', () => {
    expect(duongDanCho({ loai: 'LOAI_MOI', noiDung: 'x' }, false)).toBeNull();
    expect(duongDanCho({ loai: 'LOAI_MOI', noiDung: 'x' }, true)).toBeNull();
  });
});

describe('nhãn hành động', () => {
  it('việc cần làm có nhãn, thông báo chỉ để biết thì không', () => {
    expect(nhanHanhDong(tb({ hanhDong: 'XAC_NHAN_RA', thamChieuId: 1 }), false)).toBe('Xác nhận ra');
    expect(nhanHanhDong(tb({ hanhDong: 'DUYET_XE', thamChieuId: 1 }), true)).toBe('Duyệt xe');
    expect(nhanHanhDong(tb({ hanhDong: 'XEM_LUOT', thamChieuId: 1 }), false)).toBeNull();
  });
  it('thông báo cũ "đang ở cổng ra" vẫn có nhãn Xác nhận ra', () => {
    expect(hanhDongCua({ loai: 'RA', noiDung: 'Xe A đang ở cổng ra…' }, false)).toBe('XAC_NHAN_RA');
  });
});
