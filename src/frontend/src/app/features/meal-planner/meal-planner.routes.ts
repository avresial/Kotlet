import { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/auth.guard';

export const mealPlannerRoutes: Routes = [
  {
    path: 'meal-planner',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/meal-planner-page/meal-planner-page').then((m) => m.MealPlannerPage),
  },
];
