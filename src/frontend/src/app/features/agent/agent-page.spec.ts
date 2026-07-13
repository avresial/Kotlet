import { TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AgentPage } from './agent-page';
import { AgentService } from './agent.service';
import { AiProviderService } from '../settings/ai-provider.service';

describe('AgentPage', () => {
  let component: AgentPage;
  let agentService: AgentService;
  let providerService: AiProviderService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AgentPage],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        { provide: AgentService, useValue: { chat: vi.fn() } },
        { provide: AiProviderService, useValue: { get: vi.fn() } },
      ],
    });

    agentService = TestBed.inject(AgentService);
    providerService = TestBed.inject(AiProviderService);

    vi.mocked(providerService.get).mockReturnValue(
      of({
        providerName: 'test',
        baseUrl: 'http://localhost',
        isEnabled: true,
        hasApiKey: true,
        models: ['test-model', 'alt-model'],
        defaultModel: 'test-model',
        createdAtUtc: '2026-01-01T00:00:00Z',
        updatedAtUtc: '2026-01-01T00:00:00Z',
      })
    );

    component = TestBed.createComponent(AgentPage).componentInstance;
  });

  it('should sanitize markdown to prevent XSS', () => {
    const malicious = '# Safe\n\n<script>alert("xss")</script>';
    const html = component.renderMarkdown(malicious);
    const div = document.createElement('div');
    div.innerHTML = html as string;
    expect(div.querySelector('script')).toBeFalsy();
  });

  it('should handle response flow with trimmed content, captured model, response time, and clean API history', () => {
    const mockResponse = { content: '  Response with padding  \n' };
    vi.mocked(agentService.chat).mockReturnValue(of(mockResponse));
    component.model = 'test-model';
    component.prompt = 'Hello';

    component.send();

    const chatCall = vi.mocked(agentService.chat).mock.calls[0];
    const [sentModel, sentHistory] = chatCall;

    // Verify captured model was used for both request and metadata
    expect(sentModel).toBe('test-model');

    // Verify API history strips metadata
    expect(sentHistory.every((msg: any) => 'role' in msg && 'content' in msg)).toBe(true);
    expect(sentHistory.every((msg: any) => !('model' in msg) && !('responseTimeMs' in msg))).toBe(true);

    const messages = component.messages();
    const lastMessage = messages[messages.length - 1];

    // Verify response content is trimmed before storing
    expect(lastMessage.content).toBe('Response with padding');

    // Verify model and response time are captured
    expect(lastMessage.model).toBe('test-model');
    expect(typeof lastMessage.responseTimeMs).toBe('number');
    expect(lastMessage.responseTimeMs! >= 0).toBe(true);
  });
});
