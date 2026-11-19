// Định dạng hiển thị. Tự tính theo giờ Việt Nam (+07:00, không có giờ mùa hè)
// thay vì Intl, vì Intl trên một số máy Android có hỗ trợ múi giờ hạn chế.
const VN_OFFSET_MS = 7 * 60 * 60 * 1000;
const weekdays = ['Chủ nhật', 'Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7'];

function vnParts(value: string | Date) {
  const shifted = new Date((typeof value === 'string' ? Date.parse(value) : value.getTime()) + VN_OFFSET_MS);
  return {
    year: shifted.getUTCFullYear(), month: shifted.getUTCMonth() + 1, day: shifted.getUTCDate(),
    weekday: shifted.getUTCDay(), hour: shifted.getUTCHours(), minute: shifted.getUTCMinutes(),
  };
}

const pad = (value: number) => String(value).padStart(2, '0');

export function formatPrice(amount: number): string {
  return `${Math.round(amount).toString().replace(/\B(?=(\d{3})+(?!\d))/g, '.')}đ`;
}

export function formatDistance(km: number): string {
  return `${km.toFixed(1).replace('.', ',')} km`;
}

export function formatTime(value: string): string {
  const p = vnParts(value);
  return `${pad(p.hour)}:${pad(p.minute)}`;
}

/** "Hôm nay", "Ngày mai" hoặc "Thứ 6, 20/11". */
export function formatDay(value: string, now: Date = new Date()): string {
  const p = vnParts(value);
  const today = vnParts(now);
  const dayIndex = (x: { year: number; month: number; day: number }) => Date.UTC(x.year, x.month - 1, x.day) / 86_400_000;
  const diff = dayIndex(p) - dayIndex(today);
  if (diff === 0) return 'Hôm nay';
  if (diff === 1) return 'Ngày mai';
  return `${weekdays[p.weekday] ?? ''}, ${pad(p.day)}/${pad(p.month)}`;
}

export function greeting(now: Date = new Date()): string {
  const hour = vnParts(now).hour;
  if (hour < 11) return 'Chào buổi sáng';
  if (hour < 14) return 'Chào buổi trưa';
  if (hour < 18) return 'Chào buổi chiều';
  return 'Chào buổi tối';
}

/** Tên gọi trong tiếng Việt là từ cuối của họ tên: "Nguyễn Văn An" → "An". */
export function givenName(fullName: string): string {
  const parts = fullName.trim().split(/\s+/);
  return parts[parts.length - 1] || fullName;
}

export function minutesUntil(value: string, now: Date = new Date()): number {
  return Math.max(0, Math.ceil((Date.parse(value) - now.getTime()) / 60_000));
}

/** "120k", "1,2tr" — nhãn ngắn cho ghim trên bản đồ. */
export function formatShortPrice(amount: number): string {
  if (amount >= 1_000_000) return `${(amount / 1_000_000).toFixed(1).replace('.', ',').replace(',0', '')}tr`;
  return `${Math.round(amount / 1000)}k`;
}

export interface DayOption { iso: string; label: string; sub: string; isToday: boolean }

/** n ngày tới theo giờ Việt Nam, bắt đầu từ hôm nay. */
export function upcomingDays(count: number, now: Date = new Date()): DayOption[] {
  const today = vnParts(now);
  return Array.from({ length: count }, (_, offset) => {
    const date = new Date(Date.UTC(today.year, today.month - 1, today.day + offset));
    const iso = `${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())}`;
    const label = offset === 0 ? 'Hôm nay' : offset === 1 ? 'Ngày mai' : weekdays[date.getUTCDay()] ?? '';
    return { iso, label, sub: `${pad(date.getUTCDate())}/${pad(date.getUTCMonth() + 1)}`, isToday: offset === 0 };
  });
}

/** Giờ hiện tại theo giờ Việt Nam (0–23). */
export function currentVnHour(now: Date = new Date()): number {
  return vnParts(now).hour;
}

export function formatHour(hour: number): string {
  return `${pad(hour)}:00`;
}

/** "vừa xong", "5 phút trước", "3 giờ trước", "2 ngày trước" hoặc ngày dd/mm/yyyy nếu quá 30 ngày. */
export function formatRelative(value: string, now: Date = new Date()): string {
  const minutes = Math.floor((now.getTime() - Date.parse(value)) / 60_000);
  if (minutes < 1) return 'vừa xong';
  if (minutes < 60) return `${minutes} phút trước`;
  if (minutes < 24 * 60) return `${Math.floor(minutes / 60)} giờ trước`;
  if (minutes < 30 * 24 * 60) return `${Math.floor(minutes / (24 * 60))} ngày trước`;
  const p = vnParts(value);
  return `${pad(p.day)}/${pad(p.month)}/${p.year}`;
}
