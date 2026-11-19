// Module BaoCao: khớp BaoCaoDto (ghép số liệu từ CongRaVao, GoiCho, TimXe qua Contracts).
import type { LoaiXeMa } from '@/shared/models';

export interface ThongKeCong { soLuotVao: number; soLuotRa: number; soTuChoi: number; soAiTuDoc: number; soAdminXuLy: number; soNghiMat: number; doTinCayTb: number | null }
export interface DoanhThu { muaGoi: number; phiQuaHan: number; hoanTien: number; soGiaoDich: number; thuc: number }
export interface ThongKeDinhVi { soLuot: number; soCoKhu: number; soCameraGhi: number; soNguoiDungBao: number; soAdminGan: number; tiLeDinhVi: number | null }
export interface SucChuaLoaiXe { loaiXeId: number; loaiXe: LoaiXeMa; sucChua: number; soXeGiuSuat: number; soXeQuaHanTrongBai: number; soChoConBan: number; dangChiem: number }

export interface BaoCao {
  tu: string; den: string; cong: ThongKeCong; doanhThu: DoanhThu; dinhVi: ThongKeDinhVi;
  sucChua: SucChuaLoaiXe[]; tiLeAiTuDoc: number | null;
}
