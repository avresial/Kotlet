import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RecipeForm } from './recipe-form';

describe('RecipeForm image picker', () => {
  let fixture: ComponentFixture<RecipeForm>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [RecipeForm] }).compileComponents();
    fixture = TestBed.createComponent(RecipeForm);
    fixture.componentRef.setInput('showImagePicker', true);
    fixture.detectChanges();
  });

  it('shows an Add image text button for recipe creation', () => {
    const button = fixture.nativeElement.querySelector('.image-picker button');
    expect(button.textContent.trim()).toBe('Add image');
  });

  it('emits a supported selected image', () => {
    const file = new File(['image'], 'soup.webp', { type: 'image/webp' });
    const emitted: Array<File | null> = [];
    fixture.componentInstance.imageSelected.subscribe(value => emitted.push(value));

    fixture.componentInstance.chooseImage({
      target: { files: [file], value: 'soup.webp' },
    } as unknown as Event);

    expect(fixture.componentInstance.selectedImage).toBe(file);
    expect(emitted).toEqual([file]);
  });
});
