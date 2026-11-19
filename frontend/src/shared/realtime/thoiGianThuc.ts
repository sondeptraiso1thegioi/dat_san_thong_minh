import { HttpTransportType, HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr';

/** Một sự kiện server đẩy xuống (cùng nội dung vừa lưu vào hộp thư). */
export interface SuKienThoiGianThuc {
  /** Id thông báo vừa lưu (bấm thẻ nổi thì đánh dấu đã đọc). Không có ở tín hiệu không lưu hộp thư. */
  id?: number | null;
  loai: string;
  noiDung: string;
  hanhDong?: string | null;
  thamChieuId?: number | null;
}

/**
 * Kênh thời gian thực (DIP): trang chỉ biết interface này, không biết SignalR.
 * Khi kênh rớt (mạng yếu, server khởi động lại) trang vẫn tự tải lại theo chu kỳ, nên đây chỉ là "đến sớm hơn", không phải nguồn duy nhất.
 */
export interface IKenhThoiGianThuc {
  ketNoi(layToken: () => string | null): void;
  ngat(): void;
  nghe(fn: (s: SuKienThoiGianThuc) => void): () => void;
}

/**
 * Cài đặt bằng SignalR, hub /hub/thong-bao. Token gửi qua query access_token (WebSocket không gắn được header).
 * skipNegotiation + WebSockets: chạy 2 bản API sau NGINX vẫn được mà không cần sticky session (mỗi kết nối nằm trọn trên 1 bản).
 * Chưa có Redis backplane: sự kiện phát ở bản API này không tới client đang nối bản kia, nên trang vẫn tải lại theo chu kỳ.
 */
export class SignalRKenh implements IKenhThoiGianThuc {
  private ketNoiHub: HubConnection | null = null;
  private readonly nguoiNghe = new Set<(s: SuKienThoiGianThuc) => void>();

  ketNoi(layToken: () => string | null) {
    if (this.ketNoiHub) return;
    const hub = new HubConnectionBuilder()
      .withUrl('/hub/thong-bao', {
        accessTokenFactory: () => layToken() ?? '',
        skipNegotiation: true,
        transport: HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
      .configureLogging(LogLevel.Warning)
      .build();
    hub.on('thongBao', (s: SuKienThoiGianThuc) => this.nguoiNghe.forEach((fn) => fn(s)));
    this.ketNoiHub = hub;
    hub.start().catch(() => { /* không nối được: các trang vẫn tự tải lại theo chu kỳ */ });
  }

  ngat() {
    const hub = this.ketNoiHub;
    this.ketNoiHub = null;
    void hub?.stop();
  }

  nghe(fn: (s: SuKienThoiGianThuc) => void) {
    this.nguoiNghe.add(fn);
    return () => { this.nguoiNghe.delete(fn); };
  }
}
