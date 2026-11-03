/** Local-calendar helpers for the planner's `yyyy-MM-dd` date strings; no time zones are involved. */

export function weekStart(date: string): string {
  const value = parseLocalDate(date);
  value.setDate(value.getDate() - (value.getDay() + 6) % 7);
  return toDateString(value);
}

export function isValidDateString(value: string | null): value is string {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const [year, month, day] = value.split('-').map(Number);
  const date = new Date(year, month - 1, day);
  return date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day;
}

/** Returns the date string offset from the given date string by the supplied number of days. */
export function addDays(value: string, days: number): string {
  const date = parseLocalDate(value);
  date.setDate(date.getDate() + days);
  return toDateString(date);
}

export function todayString(): string {
  return toDateString(new Date());
}

export function toDateString(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export function parseLocalDate(value: string): Date {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}
