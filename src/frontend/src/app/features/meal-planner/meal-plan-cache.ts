import { CurrentUser } from '../../core/auth/auth.models';
import { DailyMealPlan, MealSlot } from './models/meal-planner.models';
import { isValidDateString, weekStart } from './meal-plan-dates';

/**
 * Best-effort localStorage cache of the visible week's daily plans, so a revisited day renders
 * instantly while the fresh copy loads. Scoped per user and household; holds one week at a time.
 */

const mealPlanCacheVersion = 1;
const mealPlanCacheStoragePrefix = 'kotlet.mealPlanner';
const mealPlanSlots: MealSlot[] = ['breakfast', 'second-breakfast', 'dinner', 'snack', 'supper'];

interface MealPlanCacheRecord {
  version: number;
  userKey: string;
  weekStart: string;
  plans: Record<string, DailyMealPlan>;
}

export function mealPlannerCacheKey(user: Pick<CurrentUser, 'id' | 'activeHouseId'> | null): string | null {
  return user?.activeHouseId ? `${user.id}:${user.activeHouseId}` : null;
}

export function mealPlannerCacheStorageKey(cacheKey: string): string {
  return `${mealPlanCacheStoragePrefix}:${cacheKey}`;
}

export function readCachedPlan(cacheKey: string | null, date: string): DailyMealPlan | null {
  if (!cacheKey) return null;
  const storage = getStorage();
  if (!storage) return null;

  try {
    const record = readCacheRecord(storage, cacheKey);
    if (!record) return null;

    const visibleWeek = weekStart(date);
    if (record.weekStart !== visibleWeek) {
      storage.removeItem(mealPlannerCacheStorageKey(cacheKey));
      return null;
    }

    const cachedPlan = record.plans[date];
    return isDailyMealPlan(cachedPlan)
      && cachedPlan.date === date
      && weekStart(cachedPlan.date) === visibleWeek
      ? cachedPlan
      : null;
  } catch {
    return null;
  }
}

export function cachePlan(cacheKey: string | null, date: string, plan: DailyMealPlan): void {
  if (!cacheKey || plan.date !== date) return;
  const storage = getStorage();
  if (!storage) return;

  try {
    const visibleWeek = weekStart(date);
    const current = readCacheRecord(storage, cacheKey);
    if (current?.weekStart === visibleWeek && JSON.stringify(current.plans[date]) === JSON.stringify(plan)) return;

    const plans = current?.weekStart === visibleWeek ? current.plans : {};
    storage.setItem(mealPlannerCacheStorageKey(cacheKey), JSON.stringify({
      version: mealPlanCacheVersion,
      userKey: cacheKey,
      weekStart: visibleWeek,
      plans: { ...plans, [date]: plan },
    } satisfies MealPlanCacheRecord));
  } catch {
    // Persistence is best-effort; the planner remains fully online when storage is unavailable.
  }
}

function readCacheRecord(storage: Storage, cacheKey: string): MealPlanCacheRecord | null {
  const storageKey = mealPlannerCacheStorageKey(cacheKey);
  const raw = storage.getItem(storageKey);
  if (!raw) return null;

  try {
    const record: unknown = JSON.parse(raw);
    if (!isMealPlanCacheRecord(record) || record.userKey !== cacheKey) {
      storage.removeItem(storageKey);
      return null;
    }
    return record;
  } catch {
    storage.removeItem(storageKey);
    return null;
  }
}

function getStorage(): Storage | null {
  try {
    return globalThis.localStorage ?? null;
  } catch {
    return null;
  }
}

function isDailyMealPlan(value: unknown): value is DailyMealPlan {
  if (!value || typeof value !== 'object') return false;
  const candidate = value as Partial<DailyMealPlan>;
  return typeof candidate.date === 'string'
    && !!candidate.meals
    && mealPlanSlots.every((slot) => Array.isArray(candidate.meals?.[slot]));
}

function isMealPlanCacheRecord(value: unknown): value is MealPlanCacheRecord {
  if (!value || typeof value !== 'object') return false;
  const candidate = value as Partial<MealPlanCacheRecord>;
  const week = candidate.weekStart;
  return candidate.version === mealPlanCacheVersion
    && typeof candidate.userKey === 'string'
    && typeof week === 'string'
    && isValidDateString(week)
    && !!candidate.plans
    && typeof candidate.plans === 'object';
}
