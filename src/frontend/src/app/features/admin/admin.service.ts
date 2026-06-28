import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { apiUrl } from '../../core/http/api-url';
import { AdminUser, UpdateAdminUserRequest } from './admin.models';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);

  getUsers() {
    return this.http.get<AdminUser[]>(apiUrl('/api/admin/users'));
  }

  updateUser(id: string, request: UpdateAdminUserRequest) {
    return this.http.put<AdminUser>(apiUrl(`/api/admin/users/${id}`), request);
  }

  deleteUser(id: string) {
    return this.http.delete<void>(apiUrl(`/api/admin/users/${id}`));
  }
}
