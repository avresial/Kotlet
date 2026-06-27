export interface CurrentUser {
  id: string;
  email: string;
  displayName: string | null;
  createdAtUtc: string;
  lastLoginAtUtc: string | null;
}

export interface AuthResponse {
  user: CurrentUser;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest extends LoginRequest {
  confirmPassword: string;
}
