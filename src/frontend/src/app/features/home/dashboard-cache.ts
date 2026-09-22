import { CurrentUser } from '../../core/auth/auth.models';
import { Ingredient } from '../ingredients/ingredient.models';
import { DailyMealPlan } from '../meal-planner/models/meal-planner.models';
import { PantryItem } from '../pantry/pantry.models';
import { RecipeAuditItem, RecipeSummary } from '../recipes/models/recipe.models';
import { ShoppingListItem } from '../shopping-list/shopping-list.models';
import { DashboardStats, HomeDetail } from './home.models';

export interface DashboardFact {
  text: string;
  source: string;
  source_url: string;
}

export interface DashboardCacheSnapshot {
  stats?: DashboardStats;
  menu?: { date: string; plan: DailyMealPlan };
  recipes?: RecipeSummary[];
  audit?: RecipeAuditItem[];
  shoppingItems?: ShoppingListItem[];
  lowStock?: PantryItem[];
  ingredients?: Ingredient[];
  home?: HomeDetail;
  fact?: DashboardFact;
}

interface StoredDashboardCache extends DashboardCacheSnapshot {
  version: number;
  savedAt: number;
}

const dashboardCachePrefix = 'kotlet.dashboard.';
const dashboardCacheVersion = 1;

export function dashboardCacheKey(
  user: Pick<CurrentUser, 'id' | 'activeHouseId'> | null,
): string | null {
  return user?.activeHouseId ? `${dashboardCachePrefix}${user.id}:${user.activeHouseId}` : null;
}

export function readDashboardCache(key: string): DashboardCacheSnapshot | null {
  try {
    const value = localStorage.getItem(key);
    if (!value) return null;

    const stored = JSON.parse(value) as StoredDashboardCache;
    if (!stored || stored.version !== dashboardCacheVersion) return null;

    const { savedAt: _savedAt, version: _version, ...snapshot } = stored;
    return snapshot;
  } catch {
    return null;
  }
}

export function writeDashboardCache(key: string, snapshot: DashboardCacheSnapshot): void {
  try {
    const stored: StoredDashboardCache = {
      ...snapshot,
      version: dashboardCacheVersion,
      savedAt: Date.now(),
    };
    localStorage.setItem(key, JSON.stringify(stored));
  } catch {
    // Storage can be unavailable or full; the dashboard remains usable without its cache.
  }
}
