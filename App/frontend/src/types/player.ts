export type Sport = 'tennis' | 'pickleball' | 'badminton' | 'table_tennis';
export type CourtEnvironment = 'indoor' | 'covered' | 'outdoor';

export interface HomeLocation {
  /** device = GPS gửi lên, profile = địa chỉ đã ghim trong hồ sơ, none = chưa có vị trí. */
  source: 'device' | 'profile' | 'none';
  address: string | null;
  lat: number | null;
  lng: number | null;
}

export interface VenueCard {
  id: string;
  name: string;
  address: string;
  lat: number;
  lng: number;
  distanceKm: number | null;
  sports: Sport[];
  hasShelter: boolean;
  minPricePerHour: number | null;
  photoUrl: string | null;
  isVip: boolean;
  openTime: string;
  closeTime: string;
  isOpenNow: boolean;
  amenities: string[];
}

export interface UpcomingBooking {
  id: string;
  venueId: string;
  venueName: string;
  venueAddress: string;
  courtName: string;
  sport: Sport;
  environment: CourtEnvironment;
  startAt: string;
  endAt: string;
  status: 'pending' | 'confirmed';
  price: number;
  holdExpiresAt: string | null;
}

export interface PlayerHome {
  location: HomeLocation;
  radiusKm: number;
  featured: VenueCard[];
  nearby: VenueCard[];
  upcoming: UpcomingBooking | null;
}

export interface VenueSearchParams {
  q?: string;
  sport?: Sport;
  radiusKm?: number;
  lat?: number;
  lng?: number;
  sheltered?: boolean;
  /** Mã tiện ích, ví dụ ['gui_xe', 'nha_tam']. */
  amenities?: string[];
  /** Ngày chơi theo giờ Việt Nam, yyyy-MM-dd; đi cùng startHour. */
  date?: string;
  startHour?: number;
  hours?: number;
}

export interface VenueSearchItem {
  venue: VenueCard;
  /** Chỉ có khi tìm theo giờ chơi. */
  freeCourts: number | null;
  slotPrice: number | null;
}

export interface VenueSearchResult {
  location: HomeLocation;
  radiusKm: number | null;
  slot: { startAt: string; endAt: string; hours: number } | null;
  venues: VenueSearchItem[];
}
