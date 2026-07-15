import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { apiUrl } from '../../core/http/api-url';

export interface AgentMessage {
  role: 'user' | 'assistant';
  content: string;
  model?: string;
  responseTimeMs?: number;
  error?: boolean;
  timestamp?: number;
}

@Injectable({ providedIn: 'root' })
export class AgentService {
  private readonly http = inject(HttpClient);
  chat(model: string, messages: AgentMessage[]) {
    return this.http.post<{ content: string }>(apiUrl('/api/agent/chat'), { model, messages });
  }
}
