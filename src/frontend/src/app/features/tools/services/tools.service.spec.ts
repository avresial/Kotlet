import '@angular/compiler';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { BrowserTestingModule, platformBrowserTesting } from '@angular/platform-browser/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { ToolsService } from './tools.service';
import { VideoTranscriptionJob, VideoTranscriptionStatus } from '../models/tools.models';

try {
  TestBed.initTestEnvironment(BrowserTestingModule, platformBrowserTesting());
} catch {
  // Already initialized
}

describe('ToolsService', () => {
  let service: ToolsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ToolsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts video url to create transcription', () => {
    service.createVideoTranscription('https://youtube.com/watch?v=123').subscribe((res) => {
      expect(res).toEqual({ id: 'job-123' });
    });

    const req = http.expectOne('/api/tools/video-transcriptions');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ url: 'https://youtube.com/watch?v=123' });
    req.flush({ id: 'job-123' });
  });

  it('fetches video transcription job by id', () => {
    const dummyJob: VideoTranscriptionJob = {
      id: 'job-123',
      status: VideoTranscriptionStatus.Ready,
      transcript: 'Slice garlic and fry.',
      title: 'Garlic Noodles',
      author: 'Chef',
      platform: 'YouTube',
      language: 'en',
      sourceUrl: 'https://youtube.com/watch?v=123',
      detectedIngredients: [
        { sourceName: 'Garlic', quantity: 3, unit: 'cloves', note: null, matchedIngredientId: 'ing-1', matchedIngredientName: 'Garlic', matchScore: 1, isProposedNew: false },
      ],
      errorReason: null,
      recipeImportJobId: 'recipe-job-456',
    };

    service.getVideoTranscription('job-123').subscribe((job) => {
      expect(job).toEqual(dummyJob);
    });

    const req = http.expectOne('/api/tools/video-transcriptions/job-123');
    expect(req.request.method).toBe('GET');
    req.flush(dummyJob);
  });

  it('triggers continue-as-recipe action', () => {
    service.continueAsRecipe('job-123').subscribe((res) => {
      expect(res).toEqual({ id: 'recipe-job-456' });
    });

    const req = http.expectOne('/api/tools/video-transcriptions/job-123/continue-as-recipe');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush({ id: 'recipe-job-456' });
  });
});
