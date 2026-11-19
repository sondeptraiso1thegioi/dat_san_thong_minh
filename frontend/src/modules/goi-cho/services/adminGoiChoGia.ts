import type { ChiTietXeAdmin, DangKyCho, LocXeDaDuyet, TrangThaiGoiXe, TrangXeDaDuyet, XeQuanLy } from '../models';
import { chuanHoaBienSo } from '@/shared/utils/format';
import type { IAdminGoiChoService } from './adminGoiChoService';

/**
 * Dữ liệu GIẢ cho trang Quản lý xe (tab "Xe đã duyệt", khung chi tiết, badge) để duyệt giao diện trước khi có backend.
 * Chỉ bật khi chạy dev với VITE_DU_LIEU_GIA=1 (xem container.ts). Các việc khác (duyệt, gỡ, huỷ gói...) gọi API thật.
 * Badge đếm từ danh sách chờ duyệt THẬT, nên thêm xe bằng tài khoản người dùng là số đổi ngay.
 */
interface XeGia { xe: XeQuanLy; taoLuc: string; duyetLuc: string | null; admin: string | null; chiaSe: boolean; lichSu: DangKyCho[]; vaoLuc: string | null; khu: string | null }

const NGAY = 86_400_000, GIO = 3_600_000;
const HO = ['Nguyễn Văn An', 'Trần Thị Bình', 'Lê Minh Châu', 'Phạm Quốc Dũng', 'Hoàng Thu Hà', 'Vũ Đức Huy', 'Đặng Ngọc Lan', 'Bùi Thanh Long'];
const DAU = ['29A', '30F', '51G', '59C', '83C', '29B1', '30A', '43C'];
const KHU = ['Khu R', 'Khu S', 'Hầm xe máy B1/B2'];

function taoDuLieu(): XeGia[] {
  const bayGio = Date.now();
  const iso = (ms: number) => new Date(ms).toISOString();
  return Array.from({ length: 46 }, (_, i) => {
    const xeId = 9001 + i;
    const bienSo = i === 0 ? '29A12345' : DAU[i % DAU.length] + String(10000 + ((i * 7919) % 90000)).padStart(5, '0');
    const loaiXeId = i % 6 === 5 ? 1 : 2;
    // 0: chưa từng mua · 1: sắp hết (30 giờ) · 2: đã hết 3 ngày · còn lại: còn hạn
    const kieu = i === 0 ? 3 : i % 5;
    const goiThang = { ten: loaiXeId === 1 ? '1 tháng (ô tô)' : '1 tháng', soNgay: 30, gia: loaiXeId === 1 ? 1_200_000 : 100_000 };
    const ketThuc = kieu === 1 ? bayGio + 30 * GIO : kieu === 2 ? bayGio - 3 * NGAY : bayGio + (10 + (i * 13) % 300) * NGAY;
    const lichSu: DangKyCho[] = kieu === 0 ? [] : [0, 1].slice(0, 1 + (i % 2)).map((k) => {
      const kt = ketThuc - k * goiThang.soNgay * NGAY;
      return {
        id: xeId * 10 + k, tenGoi: goiThang.ten, ngayBatDau: iso(kt - goiThang.soNgay * NGAY), ngayKetThuc: iso(kt),
        soTien: goiThang.gia, trangThai: kt > bayGio ? 'CON_HAN' : 'HET_HAN', muaLuc: iso(kt - goiThang.soNgay * NGAY - GIO),
        lyDoHuy: null, gopY: null, huyLuc: null, huyBoiAdmin: false,
      };
    });
    if (i % 11 === 4) lichSu.push({ id: xeId * 10 + 9, tenGoi: '1 tuần', ngayBatDau: iso(bayGio - 200 * NGAY), ngayKetThuc: iso(bayGio - 193 * NGAY),
      soTien: 30_000, trangThai: 'HUY', muaLuc: iso(bayGio - 201 * NGAY), lyDoHuy: 'Chuyển công tác', gopY: null, huyLuc: iso(bayGio - 198 * NGAY), huyBoiAdmin: false });
    const trangThaiGoi: TrangThaiGoiXe = kieu === 0 || kieu === 2 ? 'CHUA_CO' : kieu === 1 ? 'SAP_HET' : 'CON_HAN';
    const trongBai = i % 3 === 0;
    return {
      xe: { xeId, bienSo, loaiXeId, chuXeId: 500 + (i % 8), tenChuXe: HO[i % 8], hetHanLuc: kieu === 0 ? null : iso(ketThuc),
            dangTrongBai: trongBai, tenGoiHienTai: trangThaiGoi === 'CHUA_CO' ? null : goiThang.ten, trangThaiGoi },
      taoLuc: iso(bayGio - (60 + i) * NGAY),
      duyetLuc: i % 4 === 3 ? null : iso(bayGio - (59 + i) * NGAY + 2 * GIO),
      admin: i % 4 === 3 ? null : 'Quản trị viên',
      chiaSe: i % 4 === 1,
      lichSu,
      vaoLuc: trongBai ? iso(bayGio - (1 + (i % 9)) * GIO) : null,
      khu: trongBai && loaiXeId === 2 ? KHU[i % 3] : null,
    };
  });
}

const cho = (ms: number) => new Promise((r) => setTimeout(r, ms));

export class GiaAdminGoiChoService implements IAdminGoiChoService {
  private readonly duLieu = taoDuLieu();
  constructor(private readonly that: IAdminGoiChoService) {}

  layXeChoDuyet() { return this.that.layXeChoDuyet(); }
  duyetXe(xeId: number, dongY: boolean, lyDo?: string) { return this.that.duyetXe(xeId, dongY, lyDo); }
  duyetHangLoat(xeIds: number[], dongY: boolean, lyDo?: string) { return this.that.duyetHangLoat(xeIds, dongY, lyDo); }
  goXe(xeId: number, lyDo: string) { return this.that.goXe(xeId, lyDo); }
  layLichSuGoi(xeId: number) { return this.that.layLichSuGoi(xeId); }
  layChinhSachHoan() { return this.that.layChinhSachHoan(); }
  xemHuyGoi(id: number, chinhSach: string) { return this.that.xemHuyGoi(id, chinhSach); }
  huyGoi(id: number, lyDo: string, chinhSach: string) { return this.that.huyGoi(id, lyDo, chinhSach); }
  layBangGia() { return this.that.layBangGia(); }
  doiGia(goiId: number, gia: number) { return this.that.doiGia(goiId, gia); }
  doiSucChua(loaiXeId: number, sucChua: number) { return this.that.doiSucChua(loaiXeId, sucChua); }

  async demXeChoDuyet() { return (await this.that.layXeChoDuyet()).length; }

  async layXeDaDuyet(q: string, loc: LocXeDaDuyet, trang: number, soDong: number): Promise<TrangXeDaDuyet> {
    await cho(250);
    const tim = chuanHoaBienSo(q);
    const loc2 = this.duLieu.map((d) => d.xe).filter((x) =>
      (!tim || x.bienSo.includes(tim)) &&
      (loc === 'TAT_CA' || (loc === 'TRONG_BAI' ? x.dangTrongBai : x.trangThaiGoi === loc)));
    const tr = Math.max(1, trang);
    return { tong: loc2.length, trang: tr, soDong, ds: loc2.slice((tr - 1) * soDong, tr * soDong) };
  }

  async layChiTietXe(xeId: number): Promise<ChiTietXeAdmin> {
    await cho(300);
    const d = this.duLieu.find((x) => x.xe.xeId === xeId);
    if (!d) throw new Error('Không tìm thấy xe.');
    const bayGio = Date.now();
    const gan = [...d.lichSu].filter((g) => g.trangThai !== 'HUY').sort((a, b) => b.ngayKetThuc.localeCompare(a.ngayKetThuc))[0];
    const kt = gan ? new Date(gan.ngayKetThuc).getTime() : 0;
    const soNgayQuaHan = gan && kt <= bayGio ? Math.ceil((bayGio - kt) / NGAY) : 0;
    return {
      xe: { xeId, bienSo: d.xe.bienSo, loaiXeId: d.xe.loaiXeId, trangThaiDuyet: 'DA_DUYET', coAnh: true, chiaSeNhom: d.chiaSe,
            taoLuc: d.taoLuc, duyetLuc: d.duyetLuc, tenAdminDuyet: d.admin },
      chuXe: { id: d.xe.chuXeId, hoTen: d.xe.tenChuXe, sdt: `09${String(d.xe.chuXeId * 1234567).slice(-8)}`,
               email: d.xe.chuXeId % 2 ? null : `khach${d.xe.chuXeId}@vi-du.vn`, taoLuc: d.taoLuc },
      trongBai: { dangTrongBai: d.xe.dangTrongBai, vaoLuc: d.vaoLuc, khu: d.khu },
      goiHienTai: gan ? {
        dangKyChoId: gan.id, tenGoi: gan.tenGoi, soTien: gan.soTien, ngayBatDau: gan.ngayBatDau, ngayKetThuc: gan.ngayKetThuc,
        conHan: kt > bayGio, soNgayConLai: kt > bayGio ? Math.ceil((kt - bayGio) / NGAY) : 0, soNgayQuaHan,
        phiQuaHan: d.xe.dangTrongBai ? soNgayQuaHan * 10_000 : 0,
      } : null,
      lichSuGoi: [...d.lichSu].sort((a, b) => b.ngayBatDau.localeCompare(a.ngayBatDau)),
    };
  }
}
