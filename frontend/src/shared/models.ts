// Kiểu dùng chung cho mọi module (khớp DTO của API, tên trường camelCase).

export type VaiTro = 'ADMIN' | 'USER';

export type LoaiXeMa = 'O_TO' | 'XE_MAY';

/** Lỗi API trả về: { ma, thongDiep }. */
export class LoiApi extends Error {
  constructor(public status: number, public ma: string, thongDiep: string) {
    super(thongDiep);
  }
}
