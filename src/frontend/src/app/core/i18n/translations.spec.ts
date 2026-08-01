import { readFileSync } from 'node:fs';
import { supportedLanguages } from './language';

// The bundles are fetched at runtime and copied verbatim into the build output, so a malformed or
// duplicated entry never fails a build — it only shows up in the browser as keys rendering as
// their own names, or as one translation silently overwriting another.
// Specs are bundled, so `import.meta.url` is virtual; the runner's cwd is the frontend project.
const source = (language: string) => readFileSync(`${process.cwd()}/public/i18n/${language}.json`, 'utf8');

const parse = (language: string): Record<string, unknown> => {
  const bundle: unknown = JSON.parse(source(language));
  expect(typeof bundle).toBe('object');
  return bundle as Record<string, unknown>;
};

describe('translation bundles', () => {
  for (const language of supportedLanguages) {
    it(`${language}.json is a flat map of string values`, () => {
      const entries = Object.entries(parse(language));
      expect(entries.length).toBeGreaterThan(0);
      expect(entries.filter(([, value]) => typeof value !== 'string').map(([key]) => key)).toEqual([]);
    });

    it(`${language}.json declares every key once`, () => {
      // JSON.parse keeps the last of a repeated key, so compare against the raw source instead.
      const text = source(language);
      const declaredTwice = Object.keys(parse(language)).filter(key => text.split(`"${key}":`).length > 2);
      expect(declaredTwice).toEqual([]);
    });
  }

  it('defines the same keys in every language', () => {
    const referenceKeys = Object.keys(parse(supportedLanguages[0])).sort();
    for (const language of supportedLanguages) {
      expect([language, Object.keys(parse(language)).sort()]).toEqual([language, referenceKeys]);
    }
  });
});
