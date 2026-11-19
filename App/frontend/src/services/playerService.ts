import axiosClient from '../api/axiosClient';
import { ApiResponse } from '../types/auth';
import { PlayerHome, VenueSearchParams, VenueSearchResult } from '../types/player';

export const playerService = {
  /** Trang chủ người chơi; truyền vị trí GPS nếu có, không thì API dùng vị trí trong hồ sơ. */
  async getHome(position?: { lat: number; lng: number } | null): Promise<PlayerHome> {
    const response = await axiosClient.get<ApiResponse<PlayerHome>>('/player/home', { params: position ?? undefined });
    const home = response.data?.data;
    if (!home || !Array.isArray(home.nearby) || !Array.isArray(home.featured) || !home.location) {
      throw new Error('Dữ liệu trang chủ trả về không hợp lệ.');
    }
    return home;
  },

  /** Tìm sân (F02/UC03). Bỏ qua tham số rỗng để URL gọn và đúng với API. */
  async searchVenues(params: VenueSearchParams, signal?: AbortSignal): Promise<VenueSearchResult> {
    const query: Record<string, string | number | boolean> = {};
    if (params.q?.trim()) query.q = params.q.trim();
    if (params.sport) query.sport = params.sport;
    if (params.radiusKm) query.radiusKm = params.radiusKm;
    if (params.lat !== undefined && params.lng !== undefined) { query.lat = params.lat; query.lng = params.lng; }
    if (params.sheltered) query.sheltered = true;
    if (params.amenities?.length) query.amenities = params.amenities.join(',');
    if (params.date && params.startHour !== undefined) {
      query.date = params.date;
      query.startHour = params.startHour;
      query.hours = params.hours ?? 1;
    }
    const response = await axiosClient.get<ApiResponse<VenueSearchResult>>('/player/venues', { params: query, signal });
    const result = response.data?.data;
    if (!result || !Array.isArray(result.venues) || !result.location) throw new Error('Kết quả tìm sân không hợp lệ.');
    return result;
  },
};
