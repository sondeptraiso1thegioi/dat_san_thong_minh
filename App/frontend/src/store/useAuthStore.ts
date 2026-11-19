import { create } from 'zustand';
import { ApiError, setAccessToken, setUnauthorizedHandler } from '../api/axiosClient';
import { authService } from '../services/authService';
import { AuthSession, RegisterPayload } from '../types/auth';
import { storage } from '../utils/storage';

interface AuthState {
  session: AuthSession | null;
  status: 'loading' | 'signedOut' | 'signedIn' | 'unavailable';
  initializationError: string | null;
  initialize: () => Promise<void>;
  signIn: (email: string, password: string) => Promise<void>;
  signUp: (payload: RegisterPayload) => Promise<void>;
  signOut: () => Promise<void>;
}

export const useAuthStore = create<AuthState>((set) => ({
  session: null,
  status: 'loading',
  initializationError: null,

  initialize: async () => {
    set({ status: 'loading', initializationError: null });
    try {
      const saved = await storage.read();
      if (!saved || Date.parse(saved.expiresAt) <= Date.now()) {
        await storage.clear();
        set({ session: null, status: 'signedOut' });
        return;
      }
      const user = await authService.me(saved.accessToken);
      setAccessToken(saved.accessToken);
      set({ session: { ...saved, user }, status: 'signedIn' });
    } catch (error) {
      if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
        await storage.clear();
        set({ session: null, status: 'signedOut' });
      } else {
        set({ status: 'unavailable', initializationError: error instanceof Error
          ? error.message : 'Không thể khôi phục phiên đăng nhập.' });
      }
    }
  },

  signIn: async (email, password) => {
    const session = await authService.login(email, password);
    await storage.write(session);
    setAccessToken(session.accessToken);
    set({ session, status: 'signedIn', initializationError: null });
  },

  signUp: async (payload) => {
    const session = await authService.register(payload);
    await storage.write(session);
    setAccessToken(session.accessToken);
    set({ session, status: 'signedIn', initializationError: null });
  },

  signOut: async () => {
    setAccessToken(null);
    await storage.clear();
    set({ session: null, status: 'signedOut', initializationError: null });
  },
}));

// API báo phiên không còn hợp lệ → quay về màn đăng nhập.
setUnauthorizedHandler(() => { void useAuthStore.getState().signOut(); });
