import axiosClient from '../api/axiosClient';
import { ApiResponse } from '../types/auth';
import { OwnerVenueDetail, OwnerVenueInput, OwnerVenueListItem, OwnerVenueSaveResult } from '../types/owner';

function data<T>(response: { data: ApiResponse<T> | undefined }, message: string): T {
  const value = response.data?.data;
  if (value === undefined || value === null) throw new Error(message);
  return value;
}

export const ownerService = {
  async listVenues(): Promise<OwnerVenueListItem[]> {
    const list = data(await axiosClient.get<ApiResponse<OwnerVenueListItem[]>>('/owner/venues'), 'Danh sách cụm sân không hợp lệ.');
    if (!Array.isArray(list)) throw new Error('Danh sách cụm sân không hợp lệ.');
    return list;
  },

  async getVenue(id: string): Promise<OwnerVenueDetail> {
    return data(await axiosClient.get<ApiResponse<OwnerVenueDetail>>(`/owner/venues/${encodeURIComponent(id)}`), 'Thông tin cụm sân không hợp lệ.');
  },

  /** Tạo mới (id rỗng) hoặc lưu thay đổi cụm sân. */
  async saveVenue(id: string | null, input: OwnerVenueInput): Promise<OwnerVenueSaveResult> {
    const response = id
      ? await axiosClient.put<ApiResponse<OwnerVenueSaveResult>>(`/owner/venues/${encodeURIComponent(id)}`, input)
      : await axiosClient.post<ApiResponse<OwnerVenueSaveResult>>('/owner/venues', input);
    return data(response, 'Kết quả lưu cụm sân không hợp lệ.');
  },
};
