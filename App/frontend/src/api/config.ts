import Constants from 'expo-constants';
import { Platform } from 'react-native';

function resolveApiUrl(): string {
  const configured = process.env.EXPO_PUBLIC_API_URL?.trim();
  if (configured) return configured.replace(/\/+$/, '');
  let host = Platform.OS === 'web' ? window.location.hostname : '';
  if (!host && Constants.expoConfig?.hostUri) {
    host = new URL(`http://${Constants.expoConfig.hostUri}`).hostname;
  }
  if (!host || host === 'localhost' || host === '127.0.0.1') {
    host = Platform.OS === 'android' ? '10.0.2.2' : 'localhost';
  }
  return `http://${host}:${process.env.EXPO_PUBLIC_API_PORT || '5100'}/api`;
}

export const apiBaseUrl = resolveApiUrl();
