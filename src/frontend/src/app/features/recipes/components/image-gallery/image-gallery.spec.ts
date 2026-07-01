import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ImageGallery } from './image-gallery';
import { TranslationService } from '../../../../core/i18n/translation.service';

describe('ImageGallery', () => {
  let fixture: ComponentFixture<ImageGallery>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ImageGallery],
      providers: [{ provide: TranslationService, useValue: { translate: () => 'No recipe images yet.' } }],
    }).compileComponents();
    fixture = TestBed.createComponent(ImageGallery);
  });

  it('stabilizes when a recipe has no images', async () => {
    fixture.componentRef.setInput('images', []);
    fixture.detectChanges();

    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('No recipe images yet.');
  });
});
