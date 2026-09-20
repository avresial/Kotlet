import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { apiUrl } from '../../core/http/api-url';
import { AgentStructuredResult } from '../../shared/ui/recipe-card/recipe-card.models';

export interface AgentMessage {
  role: 'user' | 'assistant';
  content: string;
  model?: string;
  responseTimeMs?: number;
  error?: boolean;
  timestamp?: number;
  structuredResults?: AgentStructuredResult[];
}

export interface AgentChatResponse {
  content: string;
  structuredResults?: AgentStructuredResult[];
}

@Injectable({ providedIn: 'root' })
export class AgentService {
  private readonly http = inject(HttpClient);
  chat(model: string, messages: AgentMessage[]) {
    return this.http.post<AgentChatResponse>(apiUrl('/api/agent/chat'), { model, messages });
  }
}
