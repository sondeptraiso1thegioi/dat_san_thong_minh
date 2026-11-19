import type { DieuHuongCuaToi, DiemBanDo, DuongDiBanDo, KhuCho, PhienDieuHuong } from '../models';
import { KICH_THUOC_ANH, maSoDoCua, type MaSoDo } from '../soDoXeMay';
import type { IDieuHuongService } from './dieuHuongService';

/**
 * Dữ liệu GIẢ cho màn hình chỉ đường (chỉ bật khi chạy dev với VITE_DU_LIEU_GIA=1). Không gọi backend.
 * Đường đi ở đây là các đường VẼ SẴN theo đúng đồ thị mốc sẽ đặt ở backend (Dijkstra chỉ có một bản, ở server).
 * giaLap.*: nút trên bảng "Giả lập" để thử từng luồng: xe vào cổng, camera khu thấy xe, đi sai khu, khu đầy, hết hạn...
 */
const M: Record<string, DiemBanDo> = {
  CONG_X: { x: 0.035, y: 0.41, ten: 'Cổng vào X' },
  NGA_BA_X1: { x: 0.195, y: 0.41, ten: 'Ngã ba Nhà xe cán bộ X1' },
  GOC_TRUC: { x: 0.205, y: 0.47, ten: 'Trục đường chính' },
  GOC_TREN: { x: 0.205, y: 0.29, ten: null },
  DUONG_R: { x: 0.24, y: 0.265, ten: null },
  DAU_R: { x: 0.30, y: 0.27, ten: 'Đầu khu R' },
  KHU_R: { x: 0.476, y: 0.272, ten: 'Khu R' },
  TRUC_GIUA: { x: 0.40, y: 0.47, ten: 'Qua Nhà H1' },
  RE_HAM: { x: 0.668, y: 0.47, ten: 'Rẽ giữa Hội trường G3 và lối hầm' },
  LOI_HAM: { x: 0.669, y: 0.53, ten: 'Lối xuống hầm' },
  KHU_HAM: { x: 0.669, y: 0.62, ten: 'Hầm B1/B2' },
  TRUC_DONG: { x: 0.697, y: 0.47, ten: null },
  DOC_S: { x: 0.697, y: 0.31, ten: 'Đường giữa H1 và H3' },
  DAU_S: { x: 0.735, y: 0.275, ten: 'Đầu khu S' },
  KHU_S: { x: 0.85, y: 0.278, ten: 'Khu S' },
};
const TUYEN: Record<MaSoDo, string[]> = {
  R: ['CONG_X', 'NGA_BA_X1', 'GOC_TRUC', 'GOC_TREN', 'DUONG_R', 'DAU_R', 'KHU_R'],
  S: ['CONG_X', 'NGA_BA_X1', 'GOC_TRUC', 'TRUC_GIUA', 'RE_HAM', 'TRUC_DONG', 'DOC_S', 'DAU_S', 'KHU_S'],
  HAM: ['CONG_X', 'NGA_BA_X1', 'GOC_TRUC', 'TRUC_GIUA', 'RE_HAM', 'LOI_HAM', 'KHU_HAM'],
};
/** Ảnh "không theo tỉ lệ": tạm coi 1 px ảnh ≈ 0,6 m để ra con số dễ hình dung. */
const MET_MOI_PX = 0.6;

function duongToi(k: KhuCho): DuongDiBanDo | null {
  const ma = maSoDoCua(k);
  if (!ma) return null;
  const diem = TUYEN[ma].map((t) => M[t]);
  let px = 0;
  for (let i = 1; i < diem.length; i++)
    px += Math.hypot((diem[i].x - diem[i - 1].x) * KICH_THUOC_ANH.rong, (diem[i].y - diem[i - 1].y) * KICH_THUOC_ANH.cao);
  return { denKhuId: k.id, tongMet: Math.round(px * MET_MOI_PX), diem };
}

const cho = (ms: number) => new Promise((r) => setTimeout(r, ms));
const PHUT_GIU = 5;

export class GiaDieuHuongService implements IDieuHuongService {
  private phien: PhienDieuHuong | null = null;
  private hetCho: DieuHuongCuaToi['hetCho'] = null;
  private soPhien = 0;

  constructor(private readonly layKhu: () => Promise<KhuCho[]>) {}

  private async khuXeMay() { return (await this.layKhu()).filter((k) => maSoDoCua(k) !== null); }

  async layCuaToi(): Promise<DieuHuongCuaToi> {
    await cho(120);
    const p = this.phien;
    if (p) {
      // số chỗ khu đích cập nhật theo lần tải mới nhất
      const k = (await this.khuXeMay()).find((x) => x.id === p.khuDich.id);
      if (k) p.khuDich = k;
      if (p.trangThai === 'DANG_DAN' && Date.now() >= new Date(p.hetHanLuc).getTime()) this.ketThuc('HET_HAN');
    }
    return { bayGio: new Date().toISOString(), phien: this.phien && { ...this.phien }, hetCho: this.hetCho };
  }

  async doiKhuDich(khuId: number): Promise<PhienDieuHuong> {
    await cho(300);
    const p = this.phien;
    if (!p || p.trangThai !== 'DANG_DAN') throw new Error('Phiên dẫn đường đã kết thúc, không đổi khu được nữa.');
    const k = (await this.khuXeMay()).find((x) => x.id === khuId);
    if (!k) throw new Error('Không tìm thấy khu.');
    if (k.conLai <= 0) throw new Error(`${k.ten} đã đầy, hãy chọn khu khác.`);
    // Suất giữ chuyển sang khu mới, GIỮ NGUYÊN thời hạn còn lại
    p.khuDich = k; p.duong = duongToi(k); p.canhBao = null;
    return { ...p };
  }

  async layDuongDi(denKhuId: number): Promise<DuongDiBanDo> {
    await cho(150);
    const k = (await this.khuXeMay()).find((x) => x.id === denKhuId);
    const d = k && duongToi(k);
    if (!d) throw new Error('Khu này chưa có đường đi trên sơ đồ.');
    return d;
  }

  private ketThuc(tt: PhienDieuHuong['trangThai'], khuThucTe?: KhuCho) {
    if (!this.phien) return;
    this.phien = {
      ...this.phien, trangThai: tt, duong: null, canhBao: null,
      khuThucTe: khuThucTe ? { id: khuThucTe.id, ten: khuThucTe.ten, vaoLuc: new Date().toISOString() } : null,
    };
  }

  /** Các thao tác giả lập camera / thời gian (chỉ có ở dữ liệu giả). */
  readonly giaLap = {
    /** Camera cổng vào X quét biển xe máy: chọn khu theo quy tắc 2.1 và mở phiên DANG_DAN. */
    vaoCong: async (moiKhuDay = false) => {
      const ds = (await this.khuXeMay()).map((k) => (moiKhuDay ? { ...k, conLai: 0 } : k));
      const ungVien = ds.filter((k) => k.conLai > 0).map((k) => ({ k, met: duongToi(k)?.tongMet ?? Infinity }));
      const conNhieu = ungVien.filter((u) => u.k.conLai * 100 > u.k.sucChua * 20).sort((a, b) => a.met - b.met);
      const chon = conNhieu[0]?.k ?? [...ungVien].sort((a, b) => b.k.conLai - a.k.conLai)[0]?.k;
      const bayGio = Date.now();
      if (!chon) { this.phien = null; this.hetCho = { bienSo: '59C201909', luc: new Date(bayGio).toISOString() }; return; }
      this.hetCho = null;
      this.phien = {
        id: ++this.soPhien, luotGuiId: 700 + this.soPhien, xeId: 2, bienSo: '59C201909', trangThai: 'DANG_DAN',
        batDauLuc: new Date(bayGio).toISOString(), hetHanLuc: new Date(bayGio + PHUT_GIU * 60_000).toISOString(),
        khuDich: chon, khuGoiYId: chon.id, khuThucTe: null, duong: duongToi(chon), canhBao: null,
      };
    },
    /** Camera VÀO của khu đích thấy xe. */
    toiDung: async () => { if (this.phien?.trangThai === 'DANG_DAN') this.ketThuc('DA_TOI', this.phien.khuDich); },
    /** Camera VÀO của một khu khác (còn chỗ) thấy xe: tin camera, không phạt. */
    toiKhuKhac: async () => {
      const p = this.phien;
      if (p?.trangThai !== 'DANG_DAN') return;
      const khac = (await this.khuXeMay()).find((k) => k.id !== p.khuDich.id && k.conLai > 0);
      if (khac) this.ketThuc('TOI_KHU_KHAC', khac);
    },
    /** Xe tới một khu đã đầy: nhân viên không cho vào, camera không +1, vẫn dẫn tới khu đích. */
    toiKhuDay: async () => {
      const p = this.phien;
      if (p?.trangThai !== 'DANG_DAN') return;
      const day = (await this.khuXeMay()).find((k) => k.id !== p.khuDich.id && k.conLai <= 0)
        ?? (await this.khuXeMay()).find((k) => k.id !== p.khuDich.id);
      if (day) p.canhBao = { khuId: day.id, ten: day.ten, luc: new Date().toISOString() };
    },
    /** Tua nhanh: còn 20 giây giữ chỗ. */
    sapHetGio: async () => { if (this.phien?.trangThai === 'DANG_DAN') this.phien.hetHanLuc = new Date(Date.now() + 20_000).toISOString(); },
    hetHan: async () => { if (this.phien?.trangThai === 'DANG_DAN') this.ketThuc('HET_HAN'); },
    raCong: async () => { if (this.phien?.trangThai === 'DANG_DAN') this.ketThuc('DA_DONG'); },
    xoa: async () => { this.phien = null; this.hetCho = null; },
  };
}
