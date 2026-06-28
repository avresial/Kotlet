import { Routes } from '@angular/router';
import { homeGuard } from '../../core/home/home.guard';

export const mealPlannerRoutes: Routes = [
  {
    path: 'meal-planner',
    canActivate: [homeGuard],
    loadComponent: () =>
      import('./pages/meal-planner-page/meal-planner-page').then((m) => m.MealPlannerPage),
  },
];
