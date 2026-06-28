export interface CurrentUser {
  id: string;
  email: string;
  displayName: string | null;
  createdAtUtc: string;
  lastLoginAtUtc: string | null;
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
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}
