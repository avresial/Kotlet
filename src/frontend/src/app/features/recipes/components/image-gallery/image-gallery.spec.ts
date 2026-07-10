import { ComponentFixture, TestBed } from '@angular/core/testing';
import { EMPTY } from 'rxjs';
import { ImageGallery } from './image-gallery';
import { RecipeService } from '../../services/recipe.service';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { ImageSourceAttribution, RecipeImage } from '../../models/recipe.models';

describe('ImageGallery', () => {
  let fixture: ComponentFixture<ImageGallery>;

  const image = (source: ImageSourceAttribution | null | undefined): RecipeImage => ({
    id: 'image-1',
    recipeId: 'recipe-1',
    fileName: 'dish.webp',
    contentType: 'image/webp',
    fileSizeBytes: 123,
    altText: null,
    sortOrder: 0,
    contentUrl: '/api/recipes/recipe-1/images/image-1/content',
    createdAtUtc: '2026-07-10T00:00:00Z',
    source,
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ImageGallery],
      providers: [
        { provide: TranslationService, useValue: { translate: (key: string) => key === 'recipes.noImages' ? 'No recipe images yet.' : key } },
        { provide: RecipeService, useValue: { imageContent: () => EMPTY } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(ImageGallery);
  });

  it('stabilizes when a recipe has no images', async () => {
    fixture.componentRef.setInput('images', []);
    fixture.detectChanges();

    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('No recipe images yet.');
  });

  it('overlays the attribution icon when the cover image has a source url', () => {
    fixture.componentRef.setInput('images', [image({ provider: 'Pexels', authorName: 'Jane Doe', authorUrl: null, url: 'https://www.pexels.com/photo/1/' })]);
    fixture.detectChanges();

    const link = fixture.nativeElement.querySelector('.cover a.image-attribution') as HTMLAnchorElement;
    expect(link).not.toBeNull();
    expect(link.getAttribute('href')).toBe('https://www.pexels.com/photo/1/');
  });

  it('overlays the attribution icon on the immersive cover', () => {
    fixture.componentRef.setInput('immersive', true);
    fixture.componentRef.setInput('images', [image({ provider: 'Pexels', authorName: null, authorUrl: 'https://www.pexels.com/@jane/', url: null })]);
    fixture.detectChanges();

    const link = fixture.nativeElement.querySelector('.immersive-gallery a.image-attribution') as HTMLAnchorElement;
    expect(link).not.toBeNull();
    expect(link.getAttribute('href')).toBe('https://www.pexels.com/@jane/');
  });

  it('hides the attribution icon when the image has no source', () => {
    fixture.componentRef.setInput('images', [image(null)]);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('a.image-attribution')).toBeNull();
  });
});
