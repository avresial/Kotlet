export interface CurrentUser {
  id: string;
  email: string;
  displayName: string | null;
  preferredLanguage: 'en' | 'pl' | null;
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
  defaultHouseId?: string | null;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}
