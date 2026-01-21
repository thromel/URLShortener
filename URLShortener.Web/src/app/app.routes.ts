import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full'
  },
  // Auth routes (guest only)
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent),
    canActivate: [guestGuard]
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register.component').then(m => m.RegisterComponent),
    canActivate: [guestGuard]
  },
  // Protected routes
  {
    path: 'dashboard',
    loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent),
    canActivate: [authGuard]
  },
  {
    path: 'urls',
    loadComponent: () => import('./features/urls/url-list.component').then(m => m.UrlListComponent),
    canActivate: [authGuard]
  },
  {
    path: 'urls/new',
    loadComponent: () => import('./features/urls/url-create.component').then(m => m.UrlCreateComponent),
    canActivate: [authGuard]
  },
  {
    path: 'urls/:shortCode',
    loadComponent: () => import('./features/urls/url-detail.component').then(m => m.UrlDetailComponent),
    canActivate: [authGuard]
  },
  {
    path: 'bulk',
    loadComponent: () => import('./features/bulk/bulk-create.component').then(m => m.BulkCreateComponent),
    canActivate: [authGuard]
  },
  // Organization routes
  {
    path: 'organizations',
    loadComponent: () => import('./features/organizations/organization-list.component').then(m => m.OrganizationListComponent),
    canActivate: [authGuard]
  },
  {
    path: 'organizations/new',
    loadComponent: () => import('./features/organizations/organization-create.component').then(m => m.OrganizationCreateComponent),
    canActivate: [authGuard]
  },
  {
    path: 'organizations/:slug',
    loadComponent: () => import('./features/organizations/organization-detail.component').then(m => m.OrganizationDetailComponent),
    canActivate: [authGuard]
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];
