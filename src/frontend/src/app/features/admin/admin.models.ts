export interface AdminUser {
  id: string;
  email: string;
  displayName: string | null;
  preferredLanguage: 'en' | 'pl' | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  lastLoginAtUtc: string | null;
}

export interface UpdateAdminUserRequest {
  email: string;
  displayName: string | null;
  preferredLanguage: 'en' | 'pl' | null;
}
