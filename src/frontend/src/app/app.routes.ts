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
  { path: '**', redirectTo: '' },
];
