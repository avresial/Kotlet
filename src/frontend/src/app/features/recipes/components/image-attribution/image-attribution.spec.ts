import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { ImageAttribution } from './image-attribution';
import { ImageSourceAttribution } from '../../models/recipe.models';

describe('ImageAttribution', () => {
  let fixture: ComponentFixture<ImageAttribution>;

  const source = (overrides: Partial<ImageSourceAttribution> = {}): ImageSourceAttribution => ({
    provider: 'Pexels',
    authorName: 'Jane Doe',
    authorUrl: 'https://www.pexels.com/@jane/',
    url: 'https://www.pexels.com/photo/1/',
    ...overrides,
  });

  const render = (value: ImageSourceAttribution | null) => {
    fixture.componentRef.setInput('source', value);
    fixture.detectChanges();
    return fixture.nativeElement.querySelector('a.image-attribution') as HTMLAnchorElement | null;
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ImageAttribution],
      providers: [{ provide: TranslationService, useValue: { translate: (key: string) => key } }],
    }).compileComponents();
    fixture = TestBed.createComponent(ImageAttribution);
  });

  it('renders nothing without a source', () => {
    expect(render(null)).toBeNull();
  });

  it('renders nothing when the source has no usable url', () => {
    expect(render(source({ authorUrl: null, url: null }))).toBeNull();
  });

  it('links to the author url when available', () => {
    const link = render(source())!;
    expect(link.getAttribute('href')).toBe('https://www.pexels.com/@jane/');
  });

  it('falls back to the source url without an author url', () => {
    const link = render(source({ authorUrl: null }))!;
    expect(link.getAttribute('href')).toBe('https://www.pexels.com/photo/1/');
  });

  it('opens in a new tab with safe link attributes and an accessible label', () => {
    const link = render(source())!;
    expect(link.getAttribute('target')).toBe('_blank');
    expect(link.getAttribute('rel')).toBe('noopener noreferrer');
    expect(link.getAttribute('aria-label')).toBe('recipes.viewImageSource');
  });

  it('shows author and provider in the tooltip', () => {
    const link = render(source())!;
    expect(link.querySelector('[role="tooltip"]')!.textContent).toBe('Jane Doe · Pexels');
  });

  it('shows only the provider when there is no author', () => {
    const link = render(source({ authorName: null }))!;
    expect(link.querySelector('[role="tooltip"]')!.textContent).toBe('Pexels');
  });
});
