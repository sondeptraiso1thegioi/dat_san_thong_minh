import axiosClient from '../api/axiosClient';
import { ApiResponse } from '../types/auth';
import { AdminVenueActionResult, AdminVenueCounts, AdminVenueDetail, AdminVenueListItem } from '../types/admin';
import { VenueStatus } from '../types/owner';

function data<T>(value: T | undefined | null, message: string): T {
  if (value === undefined || value === null) throw new Error(message);
  return value;
}

export const adminService = {
  async listVenues(status: VenueStatus): Promise<{ counts: AdminVenueCounts; venues: AdminVenueListItem[] }> {
    const response = await axiosClient.get<ApiResponse<{ counts: AdminVenueCounts; venues: AdminVenueListItem[] }>>('/admin/venues', { params: { status } });
    const result = data(response.data?.data, 'Danh sách cụm sân không hợp lệ.');
    if (!Array.isArray(result.venues) || !result.counts) throw new Error('Danh sách cụm sân không hợp lệ.');
    return result;
  },

  async getVenue(id: string): Promise<AdminVenueDetail> {
    const response = await axiosClient.get<ApiResponse<AdminVenueDetail>>(`/admin/venues/${encodeURIComponent(id)}`);
    return data(response.data?.data, 'Thông tin cụm sân không hợp lệ.');
  },

  /** approve không cần lý do; reject và hide cần lý do để chủ sân biết. */
  async act(id: string, action: 'approve' | 'reject' | 'hide', reason?: string): Promise<AdminVenueActionResult> {
    const response = await axiosClient.post<ApiResponse<AdminVenueActionResult>>(
      `/admin/venues/${encodeURIComponent(id)}/${action}`, action === 'approve' ? {} : { reason });
    return data(response.data?.data, 'Kết quả xử lý cụm sân không hợp lệ.');
  },
};
