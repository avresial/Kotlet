import { readFileSync, readdirSync } from 'node:fs';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AppHeader } from '../../layout/app-header/app-header';
import { AuthService } from '../auth/auth.service';
import { CurrentUser } from '../auth/auth.models';
import { supportedLanguages } from './language';
import { TranslationService } from './translation.service';

/**
 * A translation that never resolves renders as its own key, which is what the user sees when a
 * bundle fails to load or a template asks for a key nobody defined. These tests drive the real
 * pipeline — service load, `t` pipe, template — and fail if any key reaches the DOM.
 */

// Specs are bundled, so `import.meta.url` is virtual; the runner's cwd is the frontend project.
const projectRoot = process.cwd();

// Read on demand rather than at import: a malformed bundle then fails a named test instead of
// taking the whole file down during collection.
const bundles = new Map<string, Record<string, string>>();
const bundle = (language: string): Record<string, string> => {
  if (!bundles.has(language)) {
    bundles.set(language, JSON.parse(readFileSync(`${projectRoot}/public/i18n/${language}.json`, 'utf8')));
  }
  return bundles.get(language)!;
};

let namespaceCache: Set<string> | null = null;
const namespaces = () =>
  (namespaceCache ??= new Set(Object.keys(bundle(supportedLanguages[0])).map(key => key.split('.')[0])));

/** Dotted identifiers under a namespace the bundles define — i.e. text shaped like our own keys. */
const keyShaped = (text: string): string[] => (text.match(/\b[a-zA-Z][a-zA-Z0-9]*(?:\.[a-zA-Z0-9]+)+\b/g) ?? [])
  .filter(token => namespaces().has(token.split('.')[0]));

const labelledAttributes = ['aria-label', 'title', 'placeholder', 'alt'];

/**
 * Everything the user can read, one string per text node. Scanning `textContent` instead would
 * run neighbouring elements together ("…mealsnav.mealPlanner…") and hide the key inside a token
 * that no longer starts at a namespace.
 */
const readableText = (root: HTMLElement): string[] => [root, ...root.querySelectorAll('*')].flatMap(element => [
  ...[...element.childNodes].filter(node => node.nodeType === Node.TEXT_NODE).map(node => node.nodeValue ?? ''),
  ...labelledAttributes.map(attribute => element.getAttribute(attribute) ?? ''),
]);

const user: CurrentUser = {
  id: 'user-1', email: 'cook@kotlet.local', displayName: 'Cook', preferredLanguage: null, theme: 'auto',
  createdAtUtc: '2026-01-01T00:00:00Z', lastLoginAtUtc: null, defaultHouseId: null, activeHouseId: null,
  hasHome: false, roles: ['Admin'],
};

describe('localized UI', () => {
  for (const language of supportedLanguages) {
    describe(language, () => {
      let http: HttpTestingController;

      beforeEach(async () => {
        localStorage.setItem('kotlet.language', language);
        TestBed.configureTestingModule({
          providers: [
            provideHttpClient(),
            provideHttpClientTesting(),
            provideRouter([]),
            { provide: AuthService, useValue: { currentUser: signal(user), logout: () => of(undefined) } },
          ],
        });
        http = TestBed.inject(HttpTestingController);
        const loaded = TestBed.inject(TranslationService).initialize();
        http.expectOne(`i18n/${language}.json`).flush(bundle(language));
        await loaded;
      });

      afterEach(() => localStorage.removeItem('kotlet.language'));

      it('renders the navigation in the loaded language, not its keys', async () => {
        const fixture = TestBed.createComponent(AppHeader);
        await fixture.whenStable();
        const header = fixture.nativeElement as HTMLElement;

        // Guards against a vacuous pass: an empty header would leak no keys either.
        const navigation = [...header.querySelectorAll('.nav-item span')].map(item => item.textContent?.trim());
        expect(navigation).toContain(bundle(language)['nav.recipes']);
        expect(navigation).toContain(bundle(language)['nav.shoppingList']);

        expect(readableText(header).flatMap(keyShaped)).toEqual([]);
      });
    });
  }
});

/**
 * The rendering test only covers what it renders. This walks every template instead, so a key
 * added to a page without a matching bundle entry fails here rather than in front of a user.
 */
describe('translation keys used by templates', () => {
  const sourceFiles = (directory: string): string[] =>
    readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
      const path = `${directory}/${entry.name}`;
      if (entry.isDirectory()) return sourceFiles(path);
      return entry.name.endsWith('.html') || (entry.name.endsWith('.ts') && !entry.name.endsWith('.spec.ts')) ? [path] : [];
    });

  // Both the `| t` pipe and `translate(…)` take the key as a literal, sometimes built from a
  // prefix (`'meal.slot.' + slot`), so a trailing dot is checked as a prefix instead.
  const referencedKeys = () => new Set(sourceFiles(`${projectRoot}/src/app`).flatMap(path =>
    (readFileSync(path, 'utf8').match(/'[a-zA-Z][a-zA-Z0-9]*(?:\.[a-zA-Z0-9]*)+'/g) ?? [])
      .map(literal => literal.slice(1, -1))
      .filter(key => namespaces().has(key.split('.')[0]))));

  for (const language of supportedLanguages) {
    it(`${language}.json defines every key the templates ask for`, () => {
      const keys = Object.keys(bundle(language));
      const missing = [...referencedKeys()].filter(key =>
        key.endsWith('.') ? !keys.some(candidate => candidate.startsWith(key)) : !keys.includes(key));
      expect(missing).toEqual([]);
    });
  }

  it('finds the keys it is meant to check', () => {
    const referenced = referencedKeys();
    expect(referenced.has('nav.recipes')).toBe(true);
    expect(referenced.size).toBeGreaterThan(100);
  });
});
