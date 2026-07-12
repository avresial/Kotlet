import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { catchError, map, of, tap } from 'rxjs';
import { apiUrl } from '../../core/http/api-url';

export interface AiProviderConfiguration {
  providerName: string;
  baseUrl: string;
  defaultModel: string | null;
  models: string[];
  isEnabled: boolean;
  hasApiKey: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface SaveAiProviderConfiguration {
  providerName: string;
  baseUrl: string;
  defaultModel: string | null;
  models: string[];
  isEnabled: boolean;
  apiKey?: string;
}

@Injectable({ providedIn: 'root' })
export class AiProviderService {
  private readonly http = inject(HttpClient);
  private readonly options = { withCredentials: true };
  readonly isAvailable = signal(false);

  get() { return this.http.get<AiProviderConfiguration>(apiUrl('/api/ai-provider'), this.options); }
  loadAvailability() {
    return this.get().pipe(
      map(configuration => configuration.isEnabled && configuration.hasApiKey),
      catchError(() => of(false)),
      tap(available => this.isAvailable.set(available)),
    );
  }
  save(request: SaveAiProviderConfiguration) {
    return this.http.put<AiProviderConfiguration>(apiUrl('/api/ai-provider'), request, this.options)
      .pipe(tap(configuration => this.isAvailable.set(configuration.isEnabled && configuration.hasApiKey)));
  }
  delete() {
    return this.http.delete<void>(apiUrl('/api/ai-provider'), this.options)
      .pipe(tap(() => this.isAvailable.set(false)));
  }
}
