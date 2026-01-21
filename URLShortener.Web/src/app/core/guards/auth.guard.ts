import { inject } from '@angular/core';
import { Router, CanActivateFn, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { map, take } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (
  route: ActivatedRouteSnapshot,
  state: RouterStateSnapshot
) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  // If already initialized, check immediately
  if (authService.isInitialized()) {
    if (authService.isAuthenticated()) {
      return true;
    }
    router.navigate(['/login'], {
      queryParams: { returnUrl: state.url }
    });
    return false;
  }

  // Wait for auth to initialize before checking
  return authService.ready$.pipe(
    take(1),
    map(() => {
      if (authService.isAuthenticated()) {
        return true;
      }
      router.navigate(['/login'], {
        queryParams: { returnUrl: state.url }
      });
      return false;
    })
  );
};

export const guestGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  // If already initialized, check immediately
  if (authService.isInitialized()) {
    if (!authService.isAuthenticated()) {
      return true;
    }
    router.navigate(['/dashboard']);
    return false;
  }

  // Wait for auth to initialize before checking
  return authService.ready$.pipe(
    take(1),
    map(() => {
      if (!authService.isAuthenticated()) {
        return true;
      }
      router.navigate(['/dashboard']);
      return false;
    })
  );
};

export const permissionGuard = (permission: string): CanActivateFn => {
  return (route: ActivatedRouteSnapshot) => {
    const authService = inject(AuthService);
    const router = inject(Router);

    if (!authService.isAuthenticated()) {
      router.navigate(['/login']);
      return false;
    }

    // Get organization ID from route params
    const organizationId = route.params['organizationId'] || route.params['orgId'];

    if (!organizationId) {
      console.error('Permission guard requires organizationId in route params');
      return false;
    }

    if (authService.hasPermission(organizationId, permission)) {
      return true;
    }

    // User doesn't have permission, redirect to unauthorized page or dashboard
    router.navigate(['/unauthorized']);
    return false;
  };
};

export const organizationMemberGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    router.navigate(['/login']);
    return false;
  }

  const organizationId = route.params['organizationId'] || route.params['orgId'];

  if (!organizationId) {
    console.error('Organization member guard requires organizationId in route params');
    return false;
  }

  if (authService.isMemberOf(organizationId)) {
    return true;
  }

  router.navigate(['/organizations']);
  return false;
};
