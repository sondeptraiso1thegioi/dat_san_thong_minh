import { Platform } from 'react-native';
import * as SecureStore from 'expo-secure-store';
import { SavedSession } from '../types/auth';

const key = 'sun-sport.session.v1';

export const storage = {
  async read(): Promise<SavedSession | null> {
    const raw = Platform.OS === 'web'
      ? window.sessionStorage.getItem(key)
      : await SecureStore.getItemAsync(key);
    if (!raw) return null;
    try {
      const value: unknown = JSON.parse(raw);
      if (typeof value === 'object' && value !== null
        && 'accessToken' in value && typeof value.accessToken === 'string'
        && 'expiresAt' in value && typeof value.expiresAt === 'string'
        && Number.isFinite(Date.parse(value.expiresAt))) {
        return { accessToken: value.accessToken, expiresAt: value.expiresAt };
      }
    } catch { /* Invalid saved data is discarded below. */ }
    await storage.clear();
    return null;
  },

  async write(value: SavedSession): Promise<void> {
    const raw = JSON.stringify({ accessToken: value.accessToken, expiresAt: value.expiresAt });
    if (Platform.OS === 'web') window.sessionStorage.setItem(key, raw);
    else await SecureStore.setItemAsync(key, raw);
  },

  async clear(): Promise<void> {
    if (Platform.OS === 'web') window.sessionStorage.removeItem(key);
    else await SecureStore.deleteItemAsync(key);
  },
};
