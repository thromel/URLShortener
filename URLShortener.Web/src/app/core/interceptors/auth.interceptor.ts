import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpErrorResponse, HttpClient } from '@angular/common/http';
import { inject, Injector, INJECTOR } from '@angular/core';
import { catchError, switchMap, throwError, from, Observable } from 'rxjs';

// Track if we're currently refreshing to avoid multiple refresh requests
let isRefreshing = false;

// Token key must match AuthService
const TOKEN_KEY = 'access_token';

export const authInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn) => {
  const injector = inject(Injector);

  // Skip auth header for auth endpoints (except /me, logout, revoke-all)
  const isAuthEndpoint = req.url.includes('/api/v1/auth/') &&
    !req.url.includes('/api/v1/auth/me') &&
    !req.url.includes('/api/v1/auth/logout') &&
    !req.url.includes('/api/v1/auth/revoke-all');

  if (isAuthEndpoint) {
    return next(req);
  }

  // Get token directly from localStorage to avoid circular dependency
  const token = localStorage.getItem(TOKEN_KEY);
  let authReq = req;

  if (token) {
    authReq = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });
  }

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      // Handle 401 Unauthorized - attempt token refresh
      if (error.status === 401 && !isAuthEndpoint && !isRefreshing) {
        isRefreshing = true;

        // Use dynamic import to avoid circular dependency
        return from(import('../services/auth.service')).pipe(
          switchMap(module => {
            const authService = injector.get(module.AuthService);
            return authService.refreshToken().pipe(
              switchMap(response => {
                isRefreshing = false;
                // Retry the original request with the new token
                const retryReq = req.clone({
                  setHeaders: {
                    Authorization: `Bearer ${response.accessToken}`
                  }
                });
                return next(retryReq);
              }),
              catchError(refreshError => {
                isRefreshing = false;
                // Refresh failed, logout user
                authService.logout();
                return throwError(() => refreshError);
              })
            );
          })
        );
      }

      return throwError(() => error);
    })
  );
};
