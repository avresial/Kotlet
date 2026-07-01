import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable, switchMap } from 'rxjs';
import { apiUrl } from '../../core/http/api-url';
import { AuthService } from '../../core/auth/auth.service';
import { CurrentUser } from '../../core/auth/auth.models';
import {
  HomeDetail,
  DashboardStats,
  HomeSummary,
  HouseWithToken,
  IncomingInvitation,
  PendingInvitation,
  TokenResponse,
} from './home.models';

@Injectable({ providedIn: 'root' })
export class HomeService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly options = { withCredentials: true } as const;

  listHomes(): Observable<HomeSummary[]> {
    return this.http.get<HomeSummary[]>(apiUrl('/api/houses'), this.options);
  }

  getHome(id: string): Observable<HomeDetail> {
    return this.http.get<HomeDetail>(apiUrl(`/api/houses/${id}`), this.options);
  }

  getDashboardStats(): Observable<DashboardStats> {
    return this.http.get<DashboardStats>(apiUrl('/api/dashboard/stats'), this.options);
  }

  create(name: string): Observable<CurrentUser> {
    return this.http
      .post<HouseWithToken>(apiUrl('/api/houses'), { name }, this.options)
      .pipe(switchMap((result) => this.applyAndReload(result.token)));
  }

  rename(id: string, name: string): Observable<void> {
    return this.http.put<void>(apiUrl(`/api/houses/${id}`), { name }, this.options);
  }

  delete(id: string): Observable<CurrentUser> {
    return this.http
      .delete<TokenResponse>(apiUrl(`/api/houses/${id}`), this.options)
      .pipe(switchMap((token) => this.applyAndReload(token)));
  }

  switch(id: string): Observable<CurrentUser> {
    return this.http
      .post<TokenResponse>(apiUrl(`/api/houses/${id}/switch`), null, this.options)
      .pipe(switchMap((token) => this.applyAndReload(token)));
  }

  invite(id: string, email: string): Observable<PendingInvitation> {
    return this.http.post<PendingInvitation>(apiUrl(`/api/houses/${id}/members`), { email }, this.options);
  }

  removeMember(id: string, userId: string): Observable<void> {
    return this.http.delete<void>(apiUrl(`/api/houses/${id}/members/${userId}`), this.options);
  }

  cancelInvitation(id: string, invitationId: string): Observable<void> {
    return this.http.delete<void>(apiUrl(`/api/houses/${id}/invitations/${invitationId}`), this.options);
  }

  listMyInvitations(): Observable<IncomingInvitation[]> {
    return this.http.get<IncomingInvitation[]>(apiUrl('/api/houses/invitations'), this.options);
  }

  accept(invitationId: string): Observable<CurrentUser> {
    return this.http
      .post<HouseWithToken>(apiUrl(`/api/houses/invitations/${invitationId}/accept`), null, this.options)
      .pipe(switchMap((result) => this.applyAndReload(result.token)));
  }

  decline(invitationId: string): Observable<void> {
    return this.http.post<void>(apiUrl(`/api/houses/invitations/${invitationId}/decline`), null, this.options);
  }

  private applyAndReload(token: TokenResponse | null): Observable<CurrentUser> {
    if (token) {
      this.auth.applyToken(token.accessToken);
    }
    return this.auth.reloadUser().pipe(map((user) => user));
  }
}
