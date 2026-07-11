import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { apiUrl } from '../../core/http/api-url';
import { AdminUser, AdminUserPage, UpdateAdminUserRequest } from './admin.models';

export interface YoutubeTranscriptionSettings {
  hasApiKey: boolean;
}

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);

  getUsers(page: number, search?: string) {
    let params = new HttpParams().set('page', page);
    if (search) params = params.set('search', search);
    return this.http.get<AdminUserPage>(apiUrl('/api/admin/users'), { params });
  }

  updateUser(id: string, request: UpdateAdminUserRequest) {
    return this.http.put<AdminUser>(apiUrl(`/api/admin/users/${id}`), request);
  }

  deleteUser(id: string) {
    return this.http.delete<void>(apiUrl(`/api/admin/users/${id}`));
  }

  getYoutubeTranscriptionSettings() {
    return this.http.get<YoutubeTranscriptionSettings>(apiUrl('/api/admin/settings/youtube-transcription'));
  }

  saveYoutubeTranscriptionSettings(apiKey: string | null) {
    return this.http.put<YoutubeTranscriptionSettings>(apiUrl('/api/admin/settings/youtube-transcription'), { apiKey });
  }
}
