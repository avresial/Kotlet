export const supportedLanguages = ['en', 'pl'] as const;
export type Language = (typeof supportedLanguages)[number];

export function isLanguage(value: string | null): value is Language {
  return supportedLanguages.includes(value as Language);
}
