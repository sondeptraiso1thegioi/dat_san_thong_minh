import { StyleProp, ViewStyle } from 'react-native';

export interface MapMarker {
  id: string;
  lat: number;
  lng: number;
  /** Nhãn ngắn trên ghim, ví dụ giá "120k". */
  label: string;
  vip: boolean;
  selected: boolean;
}

export interface LeafletMapProps {
  markers: MapMarker[];
  user?: { lat: number; lng: number } | null;
  radiusKm?: number | null;
  onSelect: (id: string) => void;
  /** Chạm vào bản đồ (dùng để ghim vị trí cụm sân). */
  onMapPress?: (point: { lat: number; lng: number }) => void;
  style?: StyleProp<ViewStyle>;
}

/**
 * Khu vực hỗ trợ đặt sân (đất liền và đảo ven bờ), giống điều kiện backend (OwnerVenueRules):
 * vĩ độ 8–24, kinh độ 102–110. Bản đồ bị giới hạn quanh khung này để không phóng ra cả thế giới
 * hoặc kéo vòng qua kinh tuyến 180° (khi đó Leaflet trả kinh độ kiểu 1091°).
 */
export const VIETNAM_BOUNDS = { minLat: 8, maxLat: 24, minLng: 102, maxLng: 110 };
export const MAP_LIMITS = { minZoom: 5, south: 7, west: 100.5, north: 24.5, east: 111.5 };

export function isInVietnam(lat: number, lng: number): boolean {
  return lat >= VIETNAM_BOUNDS.minLat && lat <= VIETNAM_BOUNDS.maxLat && lng >= VIETNAM_BOUNDS.minLng && lng <= VIETNAM_BOUNDS.maxLng;
}

/** Trung tâm Hà Nội, dùng khi chưa có vị trí và chưa có sân nào. */
export const DEFAULT_CENTER = { lat: 21.0285, lng: 105.8542 };

export const TILE_URL = 'https://tile.openstreetmap.org/{z}/{x}/{y}.png';
export const TILE_ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>';

/** CSS ghim dùng chung cho web và WebView. Ghim là divIcon nên không cần ảnh marker mặc định của Leaflet. */
export const PIN_CSS = `
.ss-pin{position:relative;display:inline-block;transform:translate(-50%,calc(-100% - 7px));background:#28563D;color:#fff;
  font:600 11px/1 system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;padding:6px 9px;border-radius:999px;border:2px solid #fff;
  box-shadow:0 2px 8px rgba(0,0,0,.25);white-space:nowrap;cursor:pointer}
.ss-pin:after{content:'';position:absolute;left:50%;bottom:-8px;margin-left:-5px;border:5px solid transparent;border-top-color:#fff}
.ss-pin.vip{background:#F7C84B;color:#203C2D}
.ss-pin.sel{background:#203C2D;color:#fff;z-index:1000;transform:translate(-50%,calc(-100% - 7px)) scale(1.15)}
.ss-me{width:16px;height:16px;margin:-8px 0 0 -8px;border-radius:50%;background:#2F7BEA;border:3px solid #fff;box-shadow:0 0 0 3px rgba(47,123,234,.3)}
`;

export function pinHtml(marker: MapMarker): string {
  const classes = ['ss-pin', marker.vip ? 'vip' : '', marker.selected ? 'sel' : ''].join(' ');
  const text = marker.label.replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c] ?? c));
  return `<div class="${classes}">${marker.vip ? '★ ' : ''}${text}</div>`;
}
