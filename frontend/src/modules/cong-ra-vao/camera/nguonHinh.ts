/**
 * Nguồn hình cho màn hình camera cổng (SOLID: trang chỉ biết INguonHinh).
 * Thêm nguồn mới (VD camera IP qua backend) = thêm một lớp, không sửa trang.
 */
export interface INguonHinh {
  readonly ten: string;
  /** Gắn nguồn vào thẻ <video> và bắt đầu phát. */
  mo(video: HTMLVideoElement): Promise<void>;
  dong(video: HTMLVideoElement): void;
}

/** Webcam / camera USB / điện thoại làm webcam (DroidCam, Iriun). Cần localhost hoặc HTTPS. */
export class NguonWebcam implements INguonHinh {
  private luong: MediaStream | null = null;
  constructor(private readonly thietBiId: string | null, readonly ten: string) {}

  async mo(video: HTMLVideoElement) {
    if (!navigator.mediaDevices?.getUserMedia)
      throw new Error('Trình duyệt chặn camera: hãy mở trang bằng http://localhost hoặc HTTPS.');
    this.luong = await navigator.mediaDevices.getUserMedia({
      video: { ...(this.thietBiId ? { deviceId: { exact: this.thietBiId } } : {}), width: { ideal: 1280 }, height: { ideal: 720 } },
      audio: false,
    });
    video.srcObject = this.luong;
    video.muted = true;
    await video.play();
  }

  dong(video: HTMLVideoElement) {
    this.luong?.getTracks().forEach((t) => t.stop());
    this.luong = null;
    video.srcObject = null;
  }
}

/** File video quay ở bãi: phát lặp lại, quét như camera thật (dùng để demo). */
export class NguonFileVideo implements INguonHinh {
  private url: string | null = null;
  constructor(private readonly file: File) {}
  get ten() { return `Video: ${this.file.name}`; }

  async mo(video: HTMLVideoElement) {
    this.url = URL.createObjectURL(this.file);
    video.srcObject = null;
    video.src = this.url;
    video.loop = true;
    video.muted = true;
    await video.play();
  }

  dong(video: HTMLVideoElement) {
    video.pause();
    video.removeAttribute('src');
    video.load();
    if (this.url) URL.revokeObjectURL(this.url);
    this.url = null;
  }
}

/** Danh sách camera của máy. Tên thiết bị chỉ có sau khi người dùng đã cho phép camera một lần. */
export async function lietKeCamera(): Promise<{ id: string; ten: string }[]> {
  if (!navigator.mediaDevices?.enumerateDevices) return [];
  const ds = await navigator.mediaDevices.enumerateDevices();
  return ds.filter((d) => d.kind === 'videoinput').map((d, i) => ({ id: d.deviceId, ten: d.label || `Camera ${i + 1}` }));
}

/** Chụp khung hình đang chiếu thành JPEG (cạnh dài tối đa 1280 px) để gửi cho AI. */
export function chupKhung(video: HTMLVideoElement, canvas: HTMLCanvasElement): Promise<File | null> {
  const w0 = video.videoWidth, h0 = video.videoHeight;
  if (!w0 || !h0) return Promise.resolve(null);
  const tiLe = Math.min(1, 1280 / Math.max(w0, h0));
  canvas.width = Math.round(w0 * tiLe);
  canvas.height = Math.round(h0 * tiLe);
  canvas.getContext('2d')!.drawImage(video, 0, 0, canvas.width, canvas.height);
  return new Promise((ok) => canvas.toBlob(
    (b) => ok(b ? new File([b], `khung-${Date.now()}.jpg`, { type: 'image/jpeg' }) : null), 'image/jpeg', 0.85));
}
