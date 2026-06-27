import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { authRoutes } from './features/auth/auth.routes';

export const routes: Routes = [
  ...authRoutes,
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/home/pages/home-page/home-page').then((m) => m.HomePage),
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
