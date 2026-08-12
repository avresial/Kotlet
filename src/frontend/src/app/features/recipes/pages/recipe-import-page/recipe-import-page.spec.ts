import '@angular/compiler';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { BrowserTestingModule, platformBrowserTesting } from '@angular/platform-browser/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { RecipeImportPage } from './recipe-import-page';
import { RecipeImportJob, RecipeImportStatus } from '../../models/recipe.models';
import { IngredientService } from '../../../ingredients/ingredient.service';

try {
  TestBed.initTestEnvironment(BrowserTestingModule, platformBrowserTesting());
} catch {
  // Environment already initialized.
}

describe('RecipeImportPage', () => {
  let http: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [RecipeImportPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: IngredientService,
          useValue: {
            getAll: () => of([]),
          },
        },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: new Map<string, string>([['jobId', 'import-job-1']]),
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

  it('loads job by jobId on init', () => {
    const fixture = TestBed.createComponent(RecipeImportPage);
    const comp = fixture.componentInstance;
    comp.ngOnInit();

    const mockJob: RecipeImportJob = {
      id: 'import-job-1',
      status: RecipeImportStatus.ReadyForReview,
      draft: {
        title: 'Guacamole',
        servings: 4,
        instructionsMarkdown: 'Mash avocados.',
        gaps: [],
        ingredients: [
          { name: 'Avocado', quantity: 2, unit: 'pcs', note: null, ingredientId: 'ing-1', matchedName: 'Avocado', matchScore: 1, isProposedNew: false },
        ],
        duplicateMatches: [],
      },
      errorReason: null,
    };

    const req = http.expectOne('/api/recipes/import/import-job-1');
    expect(req.request.method).toBe('GET');
    req.flush(mockJob);

    expect(comp.job()).toEqual(mockJob);
    expect(comp.draft()?.title).toBe('Guacamole');
  });

  it('accepts and saves edited draft', () => {
    const fixture = TestBed.createComponent(RecipeImportPage);
    const comp = fixture.componentInstance;
    const navigateSpy = vi.spyOn(router, 'navigate');

    const mockJob: RecipeImportJob = {
      id: 'import-job-1',
      status: RecipeImportStatus.ReadyForReview,
      draft: {
        title: 'Guacamole',
        servings: 4,
        instructionsMarkdown: 'Mash avocados.',
        gaps: [],
        ingredients: [
          { name: 'Avocado', quantity: 2, unit: 'pcs', note: null, ingredientId: 'ing-1', matchedName: 'Avocado', matchScore: 1, isProposedNew: false },
        ],
        duplicateMatches: [],
      },
      errorReason: null,
    };

    comp.job.set(mockJob);
    comp.save();

    const req = http.expectOne('/api/recipes/import/import-job-1/accept');
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'recipe-123' });

    expect(navigateSpy).toHaveBeenCalledWith(['/recipes', 'recipe-123'], { state: { justCreated: true } });
  });

  it('shows no job selected state when jobId is absent', () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [RecipeImportPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: IngredientService,
          useValue: { getAll: () => of([]) },
        },
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

    const fixture = TestBed.createComponent(RecipeImportPage);
    const comp = fixture.componentInstance;
    comp.ngOnInit();

    expect(comp.jobId()).toBeNull();
    expect(comp.job()).toBeNull();
  });
});
