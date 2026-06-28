import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { apiUrl } from '../../core/http/api-url';

export interface AiProviderConfiguration {
  providerName: string;
  baseUrl: string;
  defaultModel: string | null;
  isEnabled: boolean;
  hasApiKey: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface SaveAiProviderConfiguration {
  providerName: string;
  baseUrl: string;
  defaultModel: string | null;
  isEnabled: boolean;
  apiKey?: string;
}

@Injectable({ providedIn: 'root' })
export class AiProviderService {
  private readonly http = inject(HttpClient);
  private readonly options = { withCredentials: true };

  get() { return this.http.get<AiProviderConfiguration>(apiUrl('/api/ai-provider'), this.options); }
  save(request: SaveAiProviderConfiguration) { return this.http.put<AiProviderConfiguration>(apiUrl('/api/ai-provider'), request, this.options); }
  delete() { return this.http.delete<void>(apiUrl('/api/ai-provider'), this.options); }
}
