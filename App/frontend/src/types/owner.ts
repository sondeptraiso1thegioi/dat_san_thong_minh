import { CourtEnvironment, Sport } from './player';

export type VenueStatus = 'pending' | 'approved' | 'rejected' | 'deleted';

export interface OwnerCourt {
  /** null = sân con mới, chưa lưu. */
  id: string | null;
  sport: Sport;
  name: string;
  environment: CourtEnvironment;
  isActive: boolean;
}

export interface OwnerPrice {
  sport: Sport;
  /** 0 = Chủ nhật … 6 = Thứ 7. */
  weekday: number;
  startHour: number;
  endHour: number;
  pricePerHour: number;
}

export interface OwnerVenueInput {
  name: string;
  address: string;
  lat: number;
  lng: number;
  openTime: string;
  closeTime: string;
  amenities: string[];
  description: string | null;
  courts: OwnerCourt[];
  prices: OwnerPrice[];
}

export interface OwnerVenueDetail extends Omit<OwnerVenueInput, 'courts'> {
  id: string;
  status: VenueStatus;
  rejectReason: string | null;
  isVip: boolean;
  courts: (OwnerCourt & { id: string })[];
  createdAt: string;
}

export interface OwnerVenueListItem {
  id: string;
  name: string;
  address: string;
  status: VenueStatus;
  rejectReason: string | null;
  isVip: boolean;
  sports: Sport[];
  activeCourts: number;
  totalCourts: number;
  hasPrices: boolean;
  openTime: string;
  closeTime: string;
  createdAt: string;
}

export interface OwnerVenueSaveResult {
  venue: OwnerVenueDetail;
  /** Lần lưu này đưa cụm sân về chờ duyệt. */
  reviewRequired: boolean;
}
