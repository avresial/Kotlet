export interface AdminUser {
  id: string;
  email: string;
  displayName: string | null;
  preferredLanguage: 'en' | 'pl' | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  lastLoginAtUtc: string | null;
  roles: string[];
}

export interface UpdateAdminUserRequest {
  email: string;
  displayName: string | null;
  preferredLanguage: 'en' | 'pl' | null;
  roles: string[];
}

export interface AdminUserPage {
  items: AdminUser[];
  page: number;
  pageSize: number;
  totalCount: number;
}
