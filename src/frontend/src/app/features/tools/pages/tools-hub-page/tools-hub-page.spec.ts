import '@angular/compiler';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { BrowserTestingModule, platformBrowserTesting } from '@angular/platform-browser/testing';
import { provideRouter, Router } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ToolsHubPage } from './tools-hub-page';

try {
  TestBed.initTestEnvironment(BrowserTestingModule, platformBrowserTesting());
} catch {
  // Environment already initialized.
}

describe('ToolsHubPage', () => {
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [ToolsHubPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => http.verify());

  it('validates youtube and tiktok video URLs correctly', () => {
    const fixture = TestBed.createComponent(ToolsHubPage);
    const comp = fixture.componentInstance;

    expect(comp.isSupportedVideoUrl('https://www.youtube.com/watch?v=123')).toBe(true);
    expect(comp.isSupportedVideoUrl('https://youtu.be/123')).toBe(true);
    expect(comp.isSupportedVideoUrl('https://tiktok.com/@user/video/123')).toBe(true);
    expect(comp.isSupportedVideoUrl('https://example.com/video')).toBe(false);
    expect(comp.isSupportedVideoUrl('invalid-url')).toBe(false);
  });

  it('starts transcription and navigates on success', () => {
    const fixture = TestBed.createComponent(ToolsHubPage);
    const comp = fixture.componentInstance;
    const navigateSpy = vi.spyOn(router, 'navigate');

    comp.url.set('https://www.youtube.com/watch?v=abc');
    comp.startTranscription();

    const req = http.expectOne('/api/tools/video-transcriptions');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ url: 'https://www.youtube.com/watch?v=abc' });
    req.flush({ id: 'job-xyz' });

    expect(navigateSpy).toHaveBeenCalledWith(['/tools/video-transcription', 'job-xyz']);
  });
});
