import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { homeGuard } from './core/home/home.guard';
import { guestGuard } from './core/auth/guest.guard';
import { authRoutes } from './features/auth/auth.routes';
import { mealPlannerRoutes } from './features/meal-planner/meal-planner.routes';
import { recipeRoutes } from './features/recipes/recipes.routes';
import { adminGuard } from './core/auth/admin.guard';

export const routes: Routes = [
  ...authRoutes,
  {
    path: '',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/landing/pages/landing-page/landing-page').then((m) => m.LandingPage),
  },
  ...recipeRoutes,
  ...mealPlannerRoutes,
  {
    path: 'dashboard',
    canActivate: [homeGuard],
    loadComponent: () =>
      import('./features/home/pages/home-page/home-page').then((m) => m.HomePage),
  },
  {
    path: 'home/setup',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/home/pages/home-setup-page/home-setup-page').then((m) => m.HomeSetupPage),
  },
  {
    path: 'home',
    canActivate: [homeGuard],
    loadComponent: () =>
      import('./features/home/pages/home-manage-page/home-manage-page').then((m) => m.HomeManagePage),
  },
  {
    path: 'shopping-list',
    canActivate: [homeGuard],
    loadComponent: () =>
      import('./features/shopping-list/pages/shopping-list-page/shopping-list-page').then((m) => m.ShoppingListPage),
  },
  {
    path: 'pantry',
    canActivate: [homeGuard],
    loadComponent: () =>
      import('./features/pantry/pages/pantry-page/pantry-page').then((m) => m.PantryPage),
  },
  {
    path: 'ingredients',
    canActivate: [homeGuard],
    loadComponent: () =>
      import('./features/ingredients/pages/ingredients-page/ingredients-page').then((m) => m.IngredientsPage),
  },
  {
    path: 'settings',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/settings/pages/settings-page/settings-page').then((m) => m.SettingsPage),
  },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./features/admin/pages/admin-page/admin-page').then((m) => m.AdminPage),
  },
  { path: '**', redirectTo: '' },
];
