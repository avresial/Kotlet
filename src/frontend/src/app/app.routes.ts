import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { authRoutes } from './features/auth/auth.routes';
import { mealPlannerRoutes } from './features/meal-planner/meal-planner.routes';
import { recipeRoutes } from './features/recipes/recipes.routes';

export const routes: Routes = [
  ...authRoutes,
  ...recipeRoutes,
  ...mealPlannerRoutes,
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/home/pages/home-page/home-page').then((m) => m.HomePage),
  },
  {
    path: 'shopping-list',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/shopping-list/pages/shopping-list-page/shopping-list-page').then((m) => m.ShoppingListPage),
  },
  {
    path: 'pantry',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/pantry/pages/pantry-page/pantry-page').then((m) => m.PantryPage),
  },
  {
    path: 'ingredients',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/ingredients/pages/ingredients-page/ingredients-page').then((m) => m.IngredientsPage),
  },
  { path: '**', redirectTo: '' },
];
