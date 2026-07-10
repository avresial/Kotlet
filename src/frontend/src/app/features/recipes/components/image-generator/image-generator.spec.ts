import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { RecipeImageCandidate, RecipeImageImportResult } from '../../models/recipe.models';
import { RecipeImageSearchService } from '../../services/recipe-image-search.service';
import { ImageGenerator } from './image-generator';

describe('ImageGenerator', () => {
  let fixture: ComponentFixture<ImageGenerator>;
  let search: {
    search: (query: string) => Observable<RecipeImageCandidate[]>;
    import: (candidate: RecipeImageCandidate) => Observable<RecipeImageImportResult>;
  };
  const candidate: RecipeImageCandidate = {
    provider: 'Pexels',
    externalImageId: '42',
    previewUrl: 'https://images.example/42.jpg',
    sourcePageUrl: 'https://www.pexels.com/photo/42',
    authorName: 'Ada',
    authorUrl: null,
    altText: 'Pasta',
    width: 1200,
    height: 800,
  };
  const imported: RecipeImageImportResult = {
    content: 'd2VicA==',
    contentType: 'image/webp',
    width: 1200,
    height: 800,
    provider: 'Pexels',
    externalImageId: '42',
    sourcePageUrl: 'https://www.pexels.com/photo/42',
    authorName: 'Ada',
    authorUrl: 'https://pexels.com/@ada',
    altText: 'Pasta',
  };

  beforeEach(async () => {
    search = { search: () => of([candidate]), import: () => of(imported) };
    await TestBed.configureTestingModule({
      imports: [ImageGenerator],
      providers: [
        { provide: RecipeImageSearchService, useValue: search },
        { provide: TranslationService, useValue: { translate: (key: string) => key } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(ImageGenerator);
    fixture.componentRef.setInput('title', 'Pasta');
    fixture.componentRef.setInput('ingredients', ['salt', 'basil']);
    fixture.detectChanges();
  });

  it('shows results and emits the single selected candidate', () => {
    const emitted: RecipeImageCandidate[] = [];
    const importedValues: RecipeImageImportResult[] = [];
    fixture.componentInstance.imageSelected.subscribe(value => emitted.push(value));
    fixture.componentInstance.imageImported.subscribe(value => importedValues.push(value));

    fixture.componentInstance.generate();
    fixture.detectChanges();
    fixture.nativeElement.querySelector('.select').click();

    expect(fixture.nativeElement.querySelector('img')).not.toBeNull();
    expect(emitted).toEqual([candidate]);
    expect(importedValues).toEqual([imported]);
    expect(fixture.componentInstance.selectedId()).toBe('42');
  });

  it('locks selection while importing and reports import failures', () => {
    const pending = new Subject<RecipeImageImportResult>();
    search.import = () => pending.asObservable();

    fixture.componentInstance.generate();
    fixture.componentInstance.select(candidate);
    fixture.detectChanges();

    expect(fixture.componentInstance.importing()).toBe(true);
    expect(fixture.nativeElement.querySelector('.select').disabled).toBe(true);
    pending.error(new Error('failed'));
    fixture.detectChanges();

    expect(fixture.componentInstance.importing()).toBe(false);
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('recipes.imageImportError');
  });

  it('shows a spinner and prevents a second request while searching', () => {
    const pending = new Subject<RecipeImageCandidate[]>();
    let calls = 0;
    search.search = () => { calls++; return pending.asObservable(); };

    fixture.componentInstance.generate();
    fixture.componentInstance.generate();
    fixture.detectChanges();

    expect(calls).toBe(1);
    expect(fixture.nativeElement.querySelector('.spinner')).not.toBeNull();
    pending.next([]);
    pending.complete();
  });

  it('navigates the carousel and keeps only the latest selection active', () => {
    const second = { ...candidate, externalImageId: '43', altText: 'Salad' };
    const emitted: RecipeImageCandidate[] = [];
    search.search = () => of([candidate, second]);
    fixture.componentInstance.imageSelected.subscribe(value => emitted.push(value));

    fixture.componentInstance.generate();
    fixture.componentInstance.next();
    fixture.componentInstance.select(fixture.componentInstance.activeCandidate()!);

    expect(fixture.componentInstance.activeIndex()).toBe(1);
    expect(fixture.componentInstance.selectedId()).toBe('43');
    expect(emitted).toEqual([second]);
  });

  it('shows empty and error states', () => {
    search.search = () => of([]);
    fixture.componentInstance.generate();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.empty')).not.toBeNull();

    search.search = () => throwError(() => new Error('failed'));
    fixture.componentInstance.generate();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
  });

  it('abandons a search when saving starts', () => {
    const pending = new Subject<RecipeImageCandidate[]>();
    search.search = () => pending.asObservable();
    fixture.componentInstance.generate();

    fixture.componentRef.setInput('isSaving', true);
    fixture.detectChanges();

    expect(fixture.componentInstance.searching()).toBe(false);
    expect(fixture.nativeElement.querySelector('.spinner')).toBeNull();
  });
});
