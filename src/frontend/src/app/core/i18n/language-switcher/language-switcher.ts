import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Language } from '../language';
import { TranslationService } from '../translation.service';
import { TranslatePipe } from '../translate.pipe';

@Component({
  selector: 'app-language-switcher',
  imports: [TranslatePipe],
  templateUrl: './language-switcher.html',
  styleUrl: './language-switcher.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LanguageSwitcher {
  readonly translations = inject(TranslationService);
  select(language: Language): void { void this.translations.setLanguage(language); }
}
