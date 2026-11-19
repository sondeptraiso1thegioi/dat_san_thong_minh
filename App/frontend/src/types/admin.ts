import { OwnerPrice, VenueStatus } from './owner';
import { CourtEnvironment, Sport } from './player';

export interface AdminVenueCounts { pending: number; approved: number; rejected: number; deleted: number }

export interface AdminVenueListItem {
  id: string;
  name: string;
  address: string;
  status: VenueStatus;
  rejectReason: string | null;
  ownerName: string;
  ownerEmail: string;
  sports: Sport[];
  activeCourts: number;
  createdAt: string;
}

export interface AdminVenueDetail {
  id: string;
  name: string;
  address: string;
  lat: number;
  lng: number;
  openTime: string;
  closeTime: string;
  amenities: string[];
  description: string | null;
  status: VenueStatus;
  /** Lý do từ chối hoặc lý do ẩn. */
  rejectReason: string | null;
  createdAt: string;
  owner: { id: string; name: string; email: string; phone: string | null; status: string };
  courts: { id: string; sport: Sport; name: string; environment: CourtEnvironment; isActive: boolean }[];
  prices: OwnerPrice[];
  /** Số booking sắp tới sẽ bị hủy nếu ẩn cụm sân. */
  upcomingBookings: number;
}

export interface AdminVenueActionResult {
  venue: AdminVenueDetail;
  cancelledBookings: number;
  refundsRequested: number;
}
