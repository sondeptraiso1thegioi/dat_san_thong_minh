import axiosClient from '../api/axiosClient';
import { ApiResponse, AuthSession, RegisterPayload, UserProfile, isUserRole } from '../types/auth';

function validateUser(user: UserProfile | undefined): UserProfile {
  if (!user || typeof user !== 'object' || !isUserRole(user.role) || typeof user.id !== 'string'
    || typeof user.email !== 'string' || typeof user.name !== 'string') {
    throw new Error('Thông tin tài khoản trả về không hợp lệ.');
  }
  return user;
}

function validateSession(session: AuthSession | undefined): AuthSession {
  if (!session || typeof session !== 'object') throw new Error('Phiên đăng nhập trả về không hợp lệ.');
  validateUser(session.user);
  if (typeof session.accessToken !== 'string' || !session.accessToken
    || typeof session.expiresAt !== 'string' || !Number.isFinite(Date.parse(session.expiresAt))) {
    throw new Error('Phiên đăng nhập trả về không hợp lệ.');
  }
  return session;
}

export const authService = {
  async login(email: string, password: string): Promise<AuthSession> {
    const response = await axiosClient.post<ApiResponse<AuthSession>>('/auth/login', {
      email: email.trim(), password,
    });
    return validateSession(response.data?.data);
  },

  /** Creates a player/owner account; the API returns a session so the user is signed in at once. */
  async register(payload: RegisterPayload): Promise<AuthSession> {
    const response = await axiosClient.post<ApiResponse<AuthSession>>('/auth/register', {
      name: payload.name.trim().replace(/\s+/g, ' '),
      phone: payload.phone.trim(),
      email: payload.email.trim(),
      password: payload.password,
      role: payload.role,
    });
    return validateSession(response.data?.data);
  },

  async me(accessToken: string): Promise<UserProfile> {
    const response = await axiosClient.get<ApiResponse<UserProfile>>('/me', {
      headers: { Authorization: `Bearer ${accessToken}` },
    });
    return validateUser(response.data?.data);
  },
};
