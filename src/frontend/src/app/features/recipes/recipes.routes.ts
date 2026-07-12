import { inject } from '@angular/core';
import { Router, Routes } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { homeGuard } from '../../core/home/home.guard';
import { AiProviderService } from '../settings/ai-provider.service';

const aiProviderGuard = async () => {
  const router = inject(Router);
  return await firstValueFrom(inject(AiProviderService).loadAvailability()) || router.createUrlTree(['/recipes']);
};

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
    path: 'recipes/import',
    canActivate: [homeGuard, aiProviderGuard],
    loadComponent: () =>
      import('./pages/recipe-import-page/recipe-import-page').then((m) => m.RecipeImportPage),
  },
  {
    path: 'recipes/:id',
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
