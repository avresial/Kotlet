import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

/**
 * Discovery document served by the backend at <c>/.well-known/mcp.json</c>.
 * Only public connection metadata — never any secrets or tokens.
 */
export interface McpDiscoveryDocument {
  name: string;
  version: string;
  description: string;
  mcp_endpoint: string;
  authorization_endpoint: string;
  token_endpoint: string;
  client_id: string;
  scopes_supported: string[];
}

/** Copyable/downloadable manifest matching the shape AI clients expect. */
export interface McpManifest {
  name: string;
  transport: 'http';
  url: string;
  auth: { type: 'oauth2' };
}

@Injectable({ providedIn: 'root' })
export class McpService {
  private readonly http = inject(HttpClient);

  /** Absolute URL of the discovery document, honouring a configured API base. */
  private readonly discoveryUrl = `${environment.apiBaseUrl}/.well-known/mcp.json`;

  /**
   * Fetches the discovery document. Falls back to a locally composed document
   * (same-origin <c>/mcp</c>) so the onboarding page still works if the request
   * fails — for example when the backend is unreachable during local dev.
   */
  discover(): Observable<McpDiscoveryDocument> {
    return this.http.get<McpDiscoveryDocument>(this.discoveryUrl).pipe(
      catchError(() => of(this.fallbackDiscovery())),
    );
  }

  /** Composes the client manifest from a discovery document. */
  manifest(): Observable<McpManifest> {
    return this.discover().pipe(map((document) => this.toManifest(document)));
  }

  toManifest(document: McpDiscoveryDocument): McpManifest {
    return {
      name: 'kotlet',
      transport: 'http',
      url: document.mcp_endpoint,
      auth: { type: 'oauth2' },
    };
  }

  private fallbackDiscovery(): McpDiscoveryDocument {
    const origin = environment.apiBaseUrl || window.location.origin;
    return {
      name: 'Kotlet',
      version: '1.0.0',
      description: 'Kotlet recipe MCP server',
      mcp_endpoint: `${origin}/mcp`,
      authorization_endpoint: `${origin}/connect/authorize`,
      token_endpoint: `${origin}/connect/token`,
      client_id: 'kotlet-mcp-dev',
      scopes_supported: ['mcp'],
    };
  }
}
