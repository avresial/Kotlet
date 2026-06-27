import { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/auth.guard';

export const recipeRoutes: Routes = [
  {
    path: 'recipes',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/recipe-list-page/recipe-list-page').then((m) => m.RecipeListPage),
  },
  {
    path: 'recipes/new',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/recipe-create-page/recipe-create-page').then((m) => m.RecipeCreatePage),
  },
  {
    path: 'recipes/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/recipe-detail-page/recipe-detail-page').then((m) => m.RecipeDetailPage),
  },
  {
    path: 'recipes/:id/edit',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/recipe-edit-page/recipe-edit-page').then((m) => m.RecipeEditPage),
  },
];
