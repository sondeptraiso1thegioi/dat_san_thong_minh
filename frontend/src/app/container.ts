import { ApiBaiXeService, type IBaiXeService } from '@/modules/bai-xe/services/baiXeService';
import { ApiAdminGoiChoService, type IAdminGoiChoService } from '@/modules/goi-cho/services/adminGoiChoService';
import { GiaAdminGoiChoService } from '@/modules/goi-cho/services/adminGoiChoGia';
import { ApiGoiChoService, type IGoiChoService } from '@/modules/goi-cho/services/goiChoService';
import { ApiXeService, type IXeService } from '@/modules/goi-cho/services/xeService';
import { ApiAuthService, type IAuthService } from '@/modules/tai-khoan/services/authService';
import { PhienStore } from '@/modules/tai-khoan/services/phienStore';
import { ApiThongBaoService, type IThongBaoService } from '@/modules/thong-bao/services/thongBaoService';
import { FetchHttpClient } from '@/shared/api/httpClient';
import { ApiNhomGiaDinhService, type INhomGiaDinhService } from '@/modules/nhom-gia-dinh/services/nhomGiaDinhService';
import { ApiCongRaVaoService, type ICongRaVaoService } from '@/modules/cong-ra-vao/services/congRaVaoService';
import { ApiTimXeService, type ITimXeService } from '@/modules/tim-xe/services/timXeService';
import { GiaTimXeService } from '@/modules/tim-xe/services/timXeGia';
import { ApiDieuHuongService, type IDieuHuongService } from '@/modules/tim-xe/services/dieuHuongService';
import { GiaDieuHuongService } from '@/modules/tim-xe/services/dieuHuongGia';
import { ApiBaoCaoService, type IBaoCaoService } from '@/modules/bao-cao/services/baoCaoService';
import { ApiAdminTaiKhoanService, type IAdminTaiKhoanService } from '@/modules/tai-khoan/services/adminTaiKhoanService';
import { SignalRKenh, type IKenhThoiGianThuc } from '@/shared/realtime/thoiGianThuc';

/**
 * Composition root: nơi DUY NHẤT chọn cài đặt cụ thể cho từng interface, gom theo module.
 * Muốn chạy với dữ liệu giả để test giao diện, chỉ cần thay ở đây, các trang không đổi.
 */
export interface Services {
  phien: PhienStore;
  // module TaiKhoan
  auth: IAuthService;
  adminTaiKhoan: IAdminTaiKhoanService;
  nhomGiaDinh: INhomGiaDinhService;
  // module GoiCho
  xe: IXeService;
  goiCho: IGoiChoService;
  adminGoiCho: IAdminGoiChoService;
  // module CongRaVao
  congRaVao: ICongRaVaoService;
  // module BaiXe
  baiXe: IBaiXeService;
  // module ThongBao
  thongBao: IThongBaoService;
  /** Kênh SignalR: thông báo tới ngay, trang tải lại sớm hơn chu kỳ. */
  thoiGianThuc: IKenhThoiGianThuc;
  // module TimXe (xe máy: chỉ dẫn khu, camera lối vào khu, đối soát)
  timXe: ITimXeService;
  /** Điều hướng trong bãi: phiên dẫn đường, đổi khu đích, đường đi từ cổng. */
  dieuHuong: IDieuHuongService;
  // module BaoCao
  baoCao: IBaoCaoService;
}

/** Chạy dev với VITE_DU_LIEU_GIA=1: vài màn hình dùng dữ liệu giả để duyệt giao diện trước khi có backend. Bản build không bao giờ bật. */
const DU_LIEU_GIA = import.meta.env.DEV && import.meta.env.VITE_DU_LIEU_GIA === '1';

export function taoServices(): Services {
  const phien = new PhienStore();
  const http = new FetchHttpClient(phien);
  const timXe: ITimXeService = DU_LIEU_GIA ? new GiaTimXeService(new ApiTimXeService(http)) : new ApiTimXeService(http);
  return {
    phien,
    auth: new ApiAuthService(http),
    adminTaiKhoan: new ApiAdminTaiKhoanService(http),
    nhomGiaDinh: new ApiNhomGiaDinhService(http),
    xe: new ApiXeService(http),
    goiCho: new ApiGoiChoService(http),
    adminGoiCho: DU_LIEU_GIA ? new GiaAdminGoiChoService(new ApiAdminGoiChoService(http)) : new ApiAdminGoiChoService(http),
    congRaVao: new ApiCongRaVaoService(http),
    baiXe: new ApiBaiXeService(http),
    thongBao: new ApiThongBaoService(http),
    thoiGianThuc: new SignalRKenh(),
    timXe,
    dieuHuong: DU_LIEU_GIA ? new GiaDieuHuongService(async () => (await timXe.laySoXeKhu()).khu) : new ApiDieuHuongService(http),
    baoCao: new ApiBaoCaoService(http),
  };
}
