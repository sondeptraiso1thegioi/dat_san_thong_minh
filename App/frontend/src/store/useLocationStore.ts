import { create } from 'zustand';
import * as Location from 'expo-location';

export interface Position { lat: number; lng: number }

interface LocationState {
  /** Vị trí GPS lấy trong phiên này; null thì API dùng vị trí trong hồ sơ. */
  position: Position | null;
  locating: boolean;
  error: string | null;
  locate: () => Promise<Position | null>;
  clear: () => void;
}

export const useLocationStore = create<LocationState>((set) => ({
  position: null,
  locating: false,
  error: null,

  locate: async () => {
    set({ locating: true, error: null });
    try {
      const permission = await Location.requestForegroundPermissionsAsync();
      if (permission.status !== 'granted') {
        set({ locating: false, error: 'Bạn chưa cho phép truy cập vị trí. Sun Sport sẽ dùng địa chỉ trong hồ sơ.' });
        return null;
      }
      const current = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced });
      const position = { lat: current.coords.latitude, lng: current.coords.longitude };
      set({ position, locating: false });
      return position;
    } catch {
      // Trình duyệt chỉ cho lấy vị trí trên https hoặc localhost; điện thoại có thể đang tắt GPS.
      set({ locating: false, error: 'Chưa lấy được vị trí hiện tại. Kiểm tra GPS hoặc quyền vị trí rồi thử lại.' });
      return null;
    }
  },

  clear: () => set({ position: null, error: null }),
}));
