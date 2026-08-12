import '@angular/compiler';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { BrowserTestingModule, platformBrowserTesting } from '@angular/platform-browser/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { VideoTranscriptionPage } from './video-transcription-page';
import {
  DetectedIngredient,
  VideoTranscriptionJob,
  VideoTranscriptionStatus,
} from '../../models/tools.models';

try {
  TestBed.initTestEnvironment(BrowserTestingModule, platformBrowserTesting());
} catch {
  // Environment already initialized.
}

describe('VideoTranscriptionPage', () => {
  let http: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [VideoTranscriptionPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: new Map<string, string>(),
              queryParamMap: new Map<string, string>(),
            },
          },
        },
      ],
    });
    await TestBed.compileComponents();
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => http.verify());

  it('validates youtube and tiktok video URLs', () => {
    const fixture = TestBed.createComponent(VideoTranscriptionPage);
    const comp = fixture.componentInstance;

    expect(comp.isSupportedVideoUrl('https://www.youtube.com/watch?v=abc')).toBe(true);
    expect(comp.isSupportedVideoUrl('https://youtu.be/abc')).toBe(true);
    expect(comp.isSupportedVideoUrl('https://tiktok.com/@user/video/123')).toBe(true);
    expect(comp.isSupportedVideoUrl('https://vimeo.com/123')).toBe(false);
  });

  it('categorizes ingredient confidence states correctly', () => {
    const fixture = TestBed.createComponent(VideoTranscriptionPage);
    const comp = fixture.componentInstance;

    const confidentItem: DetectedIngredient = {
      sourceName: 'Garlic',
      quantity: 2,
      unit: 'cloves',
      note: null,
      matchedIngredientId: 'ing-1',
      matchedIngredientName: 'Garlic',
      matchScore: 0.95,
      isProposedNew: false,
    };
    const uncertainItem: DetectedIngredient = {
      sourceName: 'Spice Blend',
      quantity: 1,
      unit: 'tsp',
      note: null,
      matchedIngredientId: 'ing-2',
      matchedIngredientName: 'Curry Powder',
      matchScore: 0.76,
      isProposedNew: false,
    };
    const newItem: DetectedIngredient = {
      sourceName: 'Dragonfruit Extract',
      quantity: 1,
      unit: 'tbsp',
      note: null,
      matchedIngredientId: null,
      matchedIngredientName: null,
      matchScore: 0.2,
      isProposedNew: true,
    };

    expect(comp.getConfidenceState(confidentItem)).toBe('confident');
    expect(comp.getConfidenceState(uncertainItem)).toBe('uncertain');
    expect(comp.getConfidenceState(newItem)).toBe('new');
  });

  it('triggers continue-as-recipe and navigates to recipe import page', () => {
    const fixture = TestBed.createComponent(VideoTranscriptionPage);
    const comp = fixture.componentInstance;
    const navigateSpy = vi.spyOn(router, 'navigate');

    const mockJob: VideoTranscriptionJob = {
      id: 'trans-job-1',
      status: VideoTranscriptionStatus.Ready,
      transcript: 'Mix garlic with butter.',
      title: 'Garlic Butter',
      author: 'Chef',
      platform: 'YouTube',
      language: 'en',
      sourceUrl: 'https://youtube.com/watch?v=abc',
      detectedIngredients: [],
      errorReason: null,
      recipeImportJobId: null,
    };

    comp.job.set(mockJob);
    comp.continueAsRecipe();

    const req = http.expectOne('/api/tools/video-transcriptions/trans-job-1/continue-as-recipe');
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'recipe-job-999' });

    expect(navigateSpy).toHaveBeenCalledWith(['/recipes/import', 'recipe-job-999']);
  });

  it('copies transcript text to clipboard', async () => {
    const fixture = TestBed.createComponent(VideoTranscriptionPage);
    const comp = fixture.componentInstance;

    const writeTextSpy = vi.fn().mockResolvedValue(undefined);
    Object.assign(navigator, {
      clipboard: {
        writeText: writeTextSpy,
      },
    });

    comp.job.set({
      id: 'trans-job-1',
      status: VideoTranscriptionStatus.Ready,
      transcript: 'Full transcript text content.',
      title: 'Sample Video',
      author: 'Chef',
      platform: 'YouTube',
      language: 'en',
      sourceUrl: 'https://youtube.com/watch?v=abc',
      detectedIngredients: [],
      errorReason: null,
      recipeImportJobId: null,
    });

    comp.copyTranscript();
    expect(writeTextSpy).toHaveBeenCalledWith('Full transcript text content.');
  });
});
