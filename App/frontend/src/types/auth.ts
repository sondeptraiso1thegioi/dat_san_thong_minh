export type UserRole = 'player' | 'owner' | 'admin';

export interface UserProfile {
  id: string;
  name: string;
  email: string;
  role: UserRole;
}

export interface SavedSession {
  accessToken: string;
  expiresAt: string;
}

export interface AuthSession extends SavedSession {
  user: UserProfile;
}

export interface ApiResponse<T> { data: T }

/** Admin accounts are never self-registered. */
export type RegisterRole = Exclude<UserRole, 'admin'>;

export interface RegisterPayload {
  name: string;
  phone: string;
  email: string;
  password: string;
  role: RegisterRole;
}

export const roleLabels: Record<UserRole, string> = {
  player: 'Người chơi', owner: 'Chủ sân', admin: 'Quản trị viên',
};

export function isUserRole(value: unknown): value is UserRole {
  return value === 'player' || value === 'owner' || value === 'admin';
}
