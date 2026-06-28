import { Routes } from '@angular/router';
import { homeGuard } from '../../core/home/home.guard';

export const recipeRoutes: Routes = [
  {
    path: 'recipes',
    canActivate: [homeGuard],
    loadComponent: () =>
      import('./pages/recipe-list-page/recipe-list-page').then((m) => m.RecipeListPage),
  },
  {
    path: 'recipes/new',
    canActivate: [homeGuard],
    loadComponent: () =>
      import('./pages/recipe-create-page/recipe-create-page').then((m) => m.RecipeCreatePage),
  },
  {
    path: 'recipes/:id',
    canActivate: [homeGuard],
    loadComponent: () =>
      import('./pages/recipe-detail-page/recipe-detail-page').then((m) => m.RecipeDetailPage),
  },
  {
    path: 'recipes/:id/edit',
    canActivate: [homeGuard],
    loadComponent: () =>
      import('./pages/recipe-edit-page/recipe-edit-page').then((m) => m.RecipeEditPage),
  },
];
