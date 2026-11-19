import { sportLabels } from '../../constants/sports';
import { OwnerPrice } from '../../types/owner';
import { Sport } from '../../types/player';

/**
 * Một dòng trong bảng giá trên giao diện: một khung giờ, một mức giá, áp dụng cho nhiều ngày.
 * Khi gửi lên API, mỗi dòng được tách thành từng dòng price_rules theo ngày.
 */
export interface PriceBand {
  key: string;
  sport: Sport;
  days: number[];
  startHour: number;
  endHour: number;
  price: number | null;
}

export const dayShort = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
export const dayName = (d: number) => (d === 0 ? 'Chủ nhật' : `Thứ ${d + 1}`);
export const ALL_DAYS = [1, 2, 3, 4, 5, 6, 0];
export const WEEKDAYS = [1, 2, 3, 4, 5];
export const WEEKEND = [6, 0];

let counter = 0;
export const newKey = () => `k${Date.now().toString(36)}${(counter++).toString(36)}`;

export function bandsFromPrices(prices: OwnerPrice[]): PriceBand[] {
  const groups = new Map<string, PriceBand>();
  for (const p of prices) {
    const id = `${p.sport}|${p.startHour}|${p.endHour}|${p.pricePerHour}`;
    const band = groups.get(id);
    if (band) band.days.push(p.weekday);
    else groups.set(id, { key: newKey(), sport: p.sport, days: [p.weekday], startHour: p.startHour, endHour: p.endHour, price: p.pricePerHour });
  }
  return [...groups.values()].sort((a, b) => a.startHour - b.startHour);
}

export function pricesFromBands(bands: PriceBand[], activeSports: Sport[]): OwnerPrice[] {
  return bands
    .filter(b => activeSports.includes(b.sport) && b.price !== null)
    .flatMap(b => b.days.map(weekday => ({ sport: b.sport, weekday, startHour: b.startHour, endHour: b.endHour, pricePerHour: b.price ?? 0 })));
}

/** Mẫu "giờ thường + cao điểm" (cao điểm 17:00–21:00) gói trong giờ mở cửa, cả tuần; giá để trống cho chủ sân nhập. */
export function templateBands(sport: Sport, openMinutes: number, closeMinutes: number): PriceBand[] {
  const open = Math.ceil(openMinutes / 60);
  const close = Math.floor(closeMinutes / 60);
  return [[open, Math.min(17, close)], [Math.max(17, open), Math.min(21, close)], [Math.max(21, open), close]]
    .filter(([start, end]) => (start ?? 0) < (end ?? 0))
    .map(([start, end]) => ({ key: newKey(), sport, days: [...ALL_DAYS], startHour: start ?? 0, endHour: end ?? 0, price: null }));
}

/** Kiểm tra giống backend; trả câu báo lỗi đầu tiên hoặc null. */
export function validateBands(bands: PriceBand[], activeSports: Sport[]): string | null {
  const used = bands.filter(b => activeSports.includes(b.sport));
  for (const b of used) {
    const label = sportLabels[b.sport];
    if (b.days.length === 0) return `${label}: chọn ít nhất một ngày cho khung ${b.startHour}:00–${b.endHour}:00.`;
    if (b.endHour <= b.startHour) return `${label}: giờ kết thúc phải sau giờ bắt đầu.`;
    if (b.price === null) return `${label}: nhập giá cho khung ${b.startHour}:00–${b.endHour}:00.`;
    if (b.price < 1000 || b.price > 10_000_000) return `${label}: giá mỗi giờ từ 1.000đ đến 10.000.000đ.`;
    if (b.price % 1000 !== 0) return `${label}: giá phải là bội số của 1.000đ.`;
  }
  for (const sport of activeSports) {
    const sportBands = used.filter(b => b.sport === sport);
    if (sportBands.length === 0) return `Môn ${sportLabels[sport]} chưa có bảng giá.`;
    for (const day of ALL_DAYS) {
      const ranges = sportBands.filter(b => b.days.includes(day)).sort((a, b) => a.startHour - b.startHour);
      for (let i = 1; i < ranges.length; i++) {
        const prev = ranges[i - 1]!;
        const cur = ranges[i]!;
        if (cur.startHour < prev.endHour) {
          return `${sportLabels[sport]} ${dayName(day)}: khung ${prev.startHour}:00–${prev.endHour}:00 và ${cur.startHour}:00–${cur.endHour}:00 bị chồng giờ.`;
        }
      }
    }
  }
  return null;
}
