import { CourtEnvironment, Sport, VenueCard } from './player';

/** free = còn trống, booked = đã có người đặt/giữ chỗ, past = đã qua, unpriced = chủ sân chưa đặt giá. */
export type SlotState = 'free' | 'booked' | 'past' | 'unpriced';

export interface SlotCell {
  hour: number;
  price: number | null;
  state: SlotState;
}

export interface CourtSchedule {
  id: string;
  name: string;
  sport: Sport;
  environment: CourtEnvironment;
  slots: SlotCell[];
}

export interface VenueDetail {
  venue: VenueCard;
  description: string | null;
  photos: string[];
  /** yyyy-MM-dd theo giờ Việt Nam. */
  date: string;
  maxDate: string;
  /** Giờ bắt đầu nằm trong giờ mở cửa, giống nhau cho mọi sân con. */
  hours: number[];
  maxHours: number;
  holdMinutes: number;
  serverNow: string;
  courts: CourtSchedule[];
}

export interface CreateHoldInput {
  courtId: string;
  date: string;
  startHour: number;
  hours: number;
}

/** expired = giữ chỗ đã hết hạn nhưng chưa thanh toán. */
export type BookingStatus = 'pending' | 'expired' | 'confirmed' | 'cancelled' | 'completed';

export interface BookingDetail {
  id: string;
  status: BookingStatus;
  venueId: string;
  venueName: string;
  venueAddress: string;
  lat: number;
  lng: number;
  courtName: string;
  sport: Sport;
  environment: CourtEnvironment;
  startAt: string;
  endAt: string;
  hours: number;
  price: number;
  holdExpiresAt: string | null;
  cancelReason: string | null;
  createdAt: string;
  /** Giờ máy chủ lúc trả lời, để đếm ngược không phụ thuộc đồng hồ điện thoại. */
  serverNow: string;
  paymentStatus: string | null;
  canCancel: boolean;
  cancellationInfo: string | null;
}

export type BookingListView = 'all' | 'upcoming' | 'history';

export interface BookingListItem {
  id: string;
  status: BookingStatus;
  venueId: string;
  venueName: string;
  venueAddress: string;
  courtName: string;
  sport: Sport;
  environment: CourtEnvironment;
  startAt: string;
  endAt: string;
  hours: number;
  price: number;
  holdExpiresAt: string | null;
  cancelReason: string | null;
  createdAt: string;
  paymentStatus: string | null;
  canCancel: boolean;
  cancellationInfo: string | null;
}

export interface BookingListResponse {
  items: BookingListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  view: BookingListView;
  serverNow: string;
}
