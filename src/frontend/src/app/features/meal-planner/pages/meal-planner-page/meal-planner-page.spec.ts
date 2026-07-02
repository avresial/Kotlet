import { describe, expect, it } from 'vitest';
import { weekStart } from './meal-planner-page';

describe('weekStart', () => {
  it.each([
    ['2026-07-02', '2026-06-29'],
    ['2026-07-05', '2026-06-29'],
    ['2026-07-06', '2026-07-06'],
  ])('maps %s to Monday %s', (date, monday) => expect(weekStart(date)).toBe(monday));
});
