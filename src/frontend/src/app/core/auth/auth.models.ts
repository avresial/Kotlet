export interface CurrentUser {
  id: string;
  email: string;
  displayName: string | null;
  preferredLanguage: 'en' | 'pl' | null;
  theme: 'light' | 'dark' | 'auto';
  createdAtUtc: string;
  lastLoginAtUtc: string | null;
  defaultHouseId: string | null;
  activeHouseId: string | null;
  hasHome: boolean;
  roles: string[];
}

export interface AuthResponse {
  user: CurrentUser;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  /**
   * Raw refresh token, duplicated from the Set-Cookie header. The API cookie is third-party to
   * the GitHub Pages origin and many mobile browsers refuse to store it, so the app keeps this
   * copy in localStorage and presents it via the X-Refresh-Token header.
   */
  refreshToken: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest extends LoginRequest {
  confirmPassword: string;
  displayName: string;
}

export interface UpdateProfileRequest {
  displayName: string | null;
  preferredLanguage: 'en' | 'pl' | null;
  theme: 'light' | 'dark' | 'auto';
  defaultHouseId?: string | null;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}
