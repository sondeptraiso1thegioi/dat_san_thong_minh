import { NavigatorScreenParams } from '@react-navigation/native';
import { Sport } from '../types/player';

export type PlayerTabRoutes = {
  Home: undefined;
  Search: { sport?: Sport } | undefined;
  Suggest: undefined;
  Bookings: undefined;
  Account: undefined;
};

export type OwnerTabRoutes = {
  Venues: undefined;
  OwnerBookings: undefined;
  Revenue: undefined;
  OwnerAccount: undefined;
};

export type AdminTabRoutes = {
  AdminVenues: undefined;
  Stats: undefined;
  Settings: undefined;
  AdminAccount: undefined;
};

export type RootRoutes = {
  Login: undefined;
  Register: undefined;
  Player: NavigatorScreenParams<PlayerTabRoutes> | undefined;
  /** sport/date/startHour/hours (tùy chọn): chọn sẵn khung giờ người chơi đã lọc ở Tìm sân. */
  VenueDetail: { venueId: string; name: string; sport?: Sport; date?: string; startHour?: number; hours?: number };
  BookingHold: { bookingId: string };
  Owner: NavigatorScreenParams<OwnerTabRoutes> | undefined;
  /** Không có venueId = thêm cụm sân mới. */
  OwnerVenueEditor: { venueId?: string } | undefined;
  Admin: NavigatorScreenParams<AdminTabRoutes> | undefined;
  AdminVenueReview: { venueId: string };
};
