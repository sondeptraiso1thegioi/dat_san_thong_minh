import { LoiApi } from '../models';

/** Interface gọi HTTP (DIP): service phụ thuộc interface này, không gọi fetch trực tiếp. */
export interface IHttpClient {
  get<T>(url: string): Promise<T>;
  post<T>(url: string, body?: unknown): Promise<T>;
  put<T>(url: string, body?: unknown): Promise<T>;
  del<T>(url: string): Promise<T>;
  /** Gửi multipart/form-data (tải tệp). */
  putForm<T>(url: string, form: FormData): Promise<T>;
  postForm<T>(url: string, form: FormData): Promise<T>;
  /** Lấy tệp nhị phân (ảnh) có kèm JWT; thẻ <img src> không tự gắn header được. */
  getBlob(url: string): Promise<Blob>;
}

export interface ITokenStore {
  layToken(): string | null;
  /** Gọi khi API báo 401: phiên hết hạn hoặc đã đăng nhập ở máy khác. */
  khiHetPhien(thongDiep: string): void;
}

/** Cài đặt bằng fetch: tự gắn JWT, tự đổi lỗi { ma, thongDiep } thành LoiApi. */
export class FetchHttpClient implements IHttpClient {
  constructor(private readonly tokens: ITokenStore, private readonly goc = '') {}

  get<T>(url: string) { return this.goi<T>('GET', url); }
  post<T>(url: string, body?: unknown) { return this.goi<T>('POST', url, body); }
  put<T>(url: string, body?: unknown) { return this.goi<T>('PUT', url, body); }
  del<T>(url: string) { return this.goi<T>('DELETE', url); }
  putForm<T>(url: string, form: FormData) { return this.goi<T>('PUT', url, form); }
  postForm<T>(url: string, form: FormData) { return this.goi<T>('POST', url, form); }
  async getBlob(url: string) {
    const res = await this.guiYeuCau('GET', url);
    if (!res.ok) await this.nemLoi(res);
    return res.blob();
  }

  private guiYeuCau(method: string, url: string, body?: unknown) {
    const token = this.tokens.layToken();
    const laForm = body instanceof FormData; // FormData: để trình duyệt tự đặt Content-Type kèm boundary
    return fetch(this.goc + url, {
      method,
      headers: {
        ...(body !== undefined && !laForm ? { 'Content-Type': 'application/json' } : {}),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: body === undefined ? undefined : laForm ? (body as FormData) : JSON.stringify(body),
    });
  }

  private async nemLoi(res: Response): Promise<never> {
    const data = await res.json().catch(() => null);
    const loi = new LoiApi(res.status, data?.ma ?? 'LOI', data?.thongDiep ?? `Lỗi ${res.status}`);
    if (res.status === 401 && this.tokens.layToken()) this.tokens.khiHetPhien(loi.message);
    throw loi;
  }

  private async goi<T>(method: string, url: string, body?: unknown): Promise<T> {
    const res = await this.guiYeuCau(method, url, body);
    if (!res.ok) await this.nemLoi(res);
    if (res.status === 204) return undefined as T;
    return (await res.json().catch(() => null)) as T;
  }
}
