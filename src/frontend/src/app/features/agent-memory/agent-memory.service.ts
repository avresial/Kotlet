import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { apiUrl } from '../../core/http/api-url';

export interface AgentMemory {
  id: string;
  title: string | null;
  content: string;
  category: string | null;
  source: string;
  confidence: number | null;
  reviewStatus: string;
  createdAt: string;
  updatedAt: string;
  expiresAt: string | null;
  version: number;
  revision: number;
  createdByClient: string | null;
  updatedByClient: string | null;
  deleted: boolean;
}

export interface AgentMemoryList {
  revision: number;
  memories: AgentMemory[];
}

export interface AgentMemoryOperation {
  status: string;
  memory: AgentMemory | null;
  validationErrors: Record<string, string[]> | null;
  message: string | null;
}

export interface AgentMemoryExport {
  revision: number;
  markdown: string;
}

export interface AgentMemoryDraft {
  content: string;
  title: string | null;
  category: string | null;
  source: string;
  confidence: number | null;
  reviewStatus: string | null;
  expiresAt: string | null;
  clientId: string | null;
}

@Injectable({ providedIn: 'root' })
export class AgentMemoryService {
  private readonly http = inject(HttpClient);
  private readonly options = { withCredentials: true } as const;

  list(query = ''): Observable<AgentMemoryList> {
    const params = query.trim() ? new HttpParams().set('q', query.trim()) : undefined;
    return this.http.get<AgentMemoryList>(apiUrl('/api/agent-memory'), { ...this.options, params });
  }

  create(draft: AgentMemoryDraft): Observable<AgentMemoryOperation> {
    return this.http.post<AgentMemoryOperation>(apiUrl('/api/agent-memory'), draft, this.options);
  }

  update(id: string, draft: AgentMemoryDraft, expectedVersion: number): Observable<AgentMemoryOperation> {
    return this.http.put<AgentMemoryOperation>(apiUrl(`/api/agent-memory/${id}`), { ...draft, expectedVersion }, this.options);
  }

  delete(id: string, expectedVersion: number): Observable<AgentMemoryOperation> {
    return this.http.request<AgentMemoryOperation>('DELETE', apiUrl(`/api/agent-memory/${id}`), {
      ...this.options,
      body: { expectedVersion, clientId: 'web' },
    });
  }

  exportMarkdown(): Observable<AgentMemoryExport> {
    return this.http.get<AgentMemoryExport>(apiUrl('/api/agent-memory/export'), this.options);
  }
}
