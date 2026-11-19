import { CourtEnvironment, Sport } from '../types/player';

export const sports: Sport[] = ['tennis', 'pickleball', 'badminton', 'table_tennis'];

export const sportLabels: Record<Sport, string> = {
  tennis: 'Tennis',
  pickleball: 'Pickleball',
  badminton: 'Cầu lông',
  table_tennis: 'Bóng bàn',
};

/** Màu nền nhẹ cho từng môn, dùng cho ảnh thay thế khi cụm sân chưa có ảnh. */
export const sportTints: Record<Sport, { background: string; ink: string }> = {
  tennis: { background: '#E7EFD9', ink: '#4D6B2E' },
  pickleball: { background: '#E3EEEA', ink: '#2E6553' },
  badminton: { background: '#F3ECDD', ink: '#7A5A23' },
  table_tennis: { background: '#ECE6F0', ink: '#5B4472' },
};

export const environmentLabels: Record<CourtEnvironment, string> = {
  indoor: 'Trong nhà',
  covered: 'Có mái che',
  outdoor: 'Ngoài trời',
};

export const amenityLabels: Record<string, string> = {
  gui_xe: 'Gửi xe',
  nuoc_uong: 'Nước uống',
  phong_thay_do: 'Phòng thay đồ',
  cho_thue_vot: 'Cho thuê vợt',
  nha_tam: 'Nhà tắm',
};
