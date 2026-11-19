import axiosClient from '../api/axiosClient';
import { ApiResponse } from '../types/auth';
import { BookingDetail, BookingListResponse, BookingListView, CreateHoldInput, VenueDetail } from '../types/booking';

function data<T>(value: T | undefined | null, message: string): T {
  if (value === undefined || value === null) throw new Error(message);
  return value;
}

const bookingPath = (id: string) => `/player/bookings/${encodeURIComponent(id)}`;

export const bookingService = {
  /** Chi tiết cụm sân và lưới giờ của một ngày (yyyy-MM-dd, bỏ trống = hôm nay). */
  async getVenue(venueId: string, date?: string, position?: { lat: number; lng: number } | null): Promise<VenueDetail> {
    const params: Record<string, string | number> = {};
    if (date) params.date = date;
    if (position) { params.lat = position.lat; params.lng = position.lng; }
    const response = await axiosClient.get<ApiResponse<VenueDetail>>(`/player/venues/${encodeURIComponent(venueId)}`, { params });
    const detail = data(response.data?.data, 'Thông tin cụm sân không hợp lệ.');
    if (!detail.venue || !Array.isArray(detail.courts) || !Array.isArray(detail.hours)) throw new Error('Thông tin cụm sân không hợp lệ.');
    return detail;
  },

  /** Giữ chỗ: tạo booking chờ thanh toán. */
  async createHold(input: CreateHoldInput): Promise<BookingDetail> {
    const response = await axiosClient.post<ApiResponse<BookingDetail>>('/player/bookings', input);
    return data(response.data?.data, 'Kết quả giữ chỗ không hợp lệ.');
  },

  /** Chỗ đang giữ còn hạn, hoặc null. */
  async getActiveHold(): Promise<BookingDetail | null> {
    const response = await axiosClient.get<ApiResponse<BookingDetail | null>>('/player/bookings/active-hold');
    return response.data?.data ?? null;
  },

  async getBookings(view: BookingListView = 'all', page = 1, pageSize = 20): Promise<BookingListResponse> {
    const response = await axiosClient.get<ApiResponse<BookingListResponse>>('/player/bookings', {
      params: { view, page, pageSize },
    });
    const result = data(response.data?.data, 'Danh sách lịch đặt không hợp lệ.');
    if (!Array.isArray(result.items)) throw new Error('Danh sách lịch đặt không hợp lệ.');
    return result;
  },

  async getBooking(id: string): Promise<BookingDetail> {
    const response = await axiosClient.get<ApiResponse<BookingDetail>>(bookingPath(id));
    return data(response.data?.data, 'Thông tin đặt sân không hợp lệ.');
  },

  async cancelBooking(id: string): Promise<BookingDetail> {
    const response = await axiosClient.post<ApiResponse<BookingDetail>>(`${bookingPath(id)}/cancel`);
    return data(response.data?.data, 'Kết quả hủy không hợp lệ.');
  },

  /** Tương thích với các màn cũ đang gọi tên cancelHold. */
  async cancelHold(id: string): Promise<BookingDetail> {
    return this.cancelBooking(id);
  },
};
