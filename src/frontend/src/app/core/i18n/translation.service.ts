import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { isLanguage, Language } from './language';

const storageKey = 'kotlet.language';

@Injectable({ providedIn: 'root' })
export class TranslationService {
  private readonly http = inject(HttpClient);
  private readonly document = inject(DOCUMENT);
  private readonly translations = signal<Record<string, string>>({});
  readonly language = signal<Language>(this.initialLanguage());

  initialize(): Promise<void> {
    return this.load(this.language()).catch(() => undefined);
  }

  async setLanguage(language: Language): Promise<void> {
    if (language !== this.language()) {
      await this.load(language);
      this.language.set(language);
    }
    this.document.documentElement.lang = language;
    this.document.defaultView?.localStorage.setItem(storageKey, language);
  }

  translate(key: string): string {
    return this.translations()[key] ?? key;
  }

  private async load(language: Language): Promise<void> {
    const translations = await firstValueFrom(this.http.get<Record<string, string>>(`i18n/${language}.json`));
    this.translations.set(translations);
    this.document.documentElement.lang = language;
  }

  private initialLanguage(): Language {
    const window = this.document.defaultView;
    const stored = window?.localStorage.getItem(storageKey) ?? null;
    if (isLanguage(stored)) return stored;
    return window?.navigator.language.toLowerCase().startsWith('pl') ? 'pl' : 'en';
  }
}
