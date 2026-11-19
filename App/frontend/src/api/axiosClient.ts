import axios from 'axios';
import { apiBaseUrl } from './config';

export class ApiError extends Error {
  constructor(message: string, public readonly status?: number, public readonly code?: string) {
    super(message);
    this.name = 'ApiError';
  }
}

const axiosClient = axios.create({ baseURL: apiBaseUrl, timeout: 15000 });

// Token của phiên hiện tại; store gọi setAccessToken khi đăng nhập/đăng xuất.
let accessToken: string | null = null;
let onUnauthorized: (() => void) | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

/** Gọi khi API trả 401 cho một request có token (phiên hết hạn hoặc tài khoản bị khóa). */
export function setUnauthorizedHandler(handler: (() => void) | null) {
  onUnauthorized = handler;
}

axiosClient.interceptors.request.use(config => {
  if (accessToken && !config.headers.Authorization) config.headers.Authorization = `Bearer ${accessToken}`;
  return config;
});

axiosClient.interceptors.response.use(
  response => response,
  (error: unknown) => {
    if (axios.isAxiosError(error)) {
      if (error.response?.status === 401 && accessToken) onUnauthorized?.();
      const body: unknown = error.response?.data;
      const message = typeof body === 'object' && body !== null && 'message' in body
        && typeof body.message === 'string' ? body.message
        : 'Chưa kết nối được máy chủ. Vui lòng kiểm tra kết nối và thử lại.';
      const code = typeof body === 'object' && body !== null && 'code' in body
        && typeof body.code === 'string' ? body.code : undefined;
      return Promise.reject(new ApiError(message, error.response?.status, code));
    }
    return Promise.reject(error);
  },
);

export default axiosClient;
