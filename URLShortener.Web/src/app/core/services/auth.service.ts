import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, BehaviorSubject, tap, catchError, throwError, of, firstValueFrom, ReplaySubject } from 'rxjs';

export interface User {
  id: string;
  email: string;
  displayName: string;
  createdAt: Date;
  organizations: OrganizationMembership[];
}

export interface OrganizationMembership {
  organizationId: string;
  organizationName: string;
  organizationSlug: string;
  roleName: string;
  permissions: string[];
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: Date;
  user: User;
}

export interface RegisterRequest {
  email: string;
  password: string;
  displayName: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly TOKEN_KEY = 'access_token';
  private readonly TOKEN_EXPIRY_KEY = 'token_expiry';

  private userSubject = new BehaviorSubject<User | null>(null);
  private tokenSubject = new BehaviorSubject<string | null>(null);

  // Signals for reactive state
  private readonly _isAuthenticated = signal(false);
  private readonly _currentUser = signal<User | null>(null);
  private readonly _isLoading = signal(false);
  private readonly _isInitialized = signal(false);

  // ReplaySubject to allow late subscribers to get the initialization result
  private readonly _ready$ = new ReplaySubject<boolean>(1);

  // Computed signals
  readonly isAuthenticated = computed(() => this._isAuthenticated());
  readonly currentUser = computed(() => this._currentUser());
  readonly isLoading = computed(() => this._isLoading());
  readonly isInitialized = computed(() => this._isInitialized());

  // Observable that resolves when auth initialization is complete
  readonly ready$ = this._ready$.asObservable();

  // Observable streams for components that need them
  readonly user$ = this.userSubject.asObservable();
  readonly token$ = this.tokenSubject.asObservable();

  constructor(
    private http: HttpClient,
    private router: Router
  ) {
    this.initializeAuth();
  }

  private initializeAuth(): void {
    const token = localStorage.getItem(this.TOKEN_KEY);
    const expiry = localStorage.getItem(this.TOKEN_EXPIRY_KEY);

    if (token && expiry) {
      const expiryDate = new Date(expiry);
      if (expiryDate > new Date()) {
        this.tokenSubject.next(token);
        this._isAuthenticated.set(true);
        // Load user and wait for completion before marking as initialized
        this.loadCurrentUser().subscribe({
          next: () => {
            this._isInitialized.set(true);
            this._ready$.next(true);
          },
          error: () => {
            // Token was invalid on server, clear it
            this.clearTokens();
            this._isAuthenticated.set(false);
            this._isInitialized.set(true);
            this._ready$.next(true);
          }
        });
      } else {
        this.clearTokens();
        this._isInitialized.set(true);
        this._ready$.next(true);
      }
    } else {
      // No token, auth is initialized as not authenticated
      this._isInitialized.set(true);
      this._ready$.next(true);
    }
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    this._isLoading.set(true);

    return this.http.post<AuthResponse>('/api/v1/auth/register', request, {
      withCredentials: true
    }).pipe(
      tap(response => this.handleAuthSuccess(response)),
      catchError(error => {
        this._isLoading.set(false);
        return throwError(() => error);
      })
    );
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    this._isLoading.set(true);

    return this.http.post<AuthResponse>('/api/v1/auth/login', request, {
      withCredentials: true
    }).pipe(
      tap(response => this.handleAuthSuccess(response)),
      catchError(error => {
        this._isLoading.set(false);
        return throwError(() => error);
      })
    );
  }

  refreshToken(): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/v1/auth/refresh', {}, {
      withCredentials: true
    }).pipe(
      tap(response => this.handleAuthSuccess(response)),
      catchError(error => {
        this.logout();
        return throwError(() => error);
      })
    );
  }

  logout(): void {
    this.http.post('/api/v1/auth/logout', {}, {
      withCredentials: true
    }).subscribe({
      complete: () => {
        this.clearSession();
      },
      error: () => {
        this.clearSession();
      }
    });
  }

  revokeAllTokens(): Observable<void> {
    return this.http.post<void>('/api/v1/auth/revoke-all', {}, {
      withCredentials: true
    }).pipe(
      tap(() => this.clearSession())
    );
  }

  loadCurrentUser(): Observable<User> {
    return this.http.get<User>('/api/v1/auth/me').pipe(
      tap(user => {
        this._currentUser.set(user);
        this.userSubject.next(user);
      }),
      catchError(error => {
        // Don't redirect here - let the caller decide what to do
        // This prevents redirect loops during initialization
        this._currentUser.set(null);
        this.userSubject.next(null);
        return throwError(() => error);
      })
    );
  }

  getToken(): string | null {
    return this.tokenSubject.getValue();
  }

  hasPermission(organizationId: string, permission: string): boolean {
    const user = this._currentUser();
    if (!user) return false;

    const membership = user.organizations.find(o => o.organizationId === organizationId);
    if (!membership) return false;

    return membership.permissions.includes(permission);
  }

  hasAnyPermission(organizationId: string, permissions: string[]): boolean {
    return permissions.some(p => this.hasPermission(organizationId, p));
  }

  hasAllPermissions(organizationId: string, permissions: string[]): boolean {
    return permissions.every(p => this.hasPermission(organizationId, p));
  }

  isMemberOf(organizationId: string): boolean {
    const user = this._currentUser();
    if (!user) return false;
    return user.organizations.some(o => o.organizationId === organizationId);
  }

  isOwnerOf(organizationId: string): boolean {
    return this.hasPermission(organizationId, 'org:delete');
  }

  private handleAuthSuccess(response: AuthResponse): void {
    this._isLoading.set(false);
    this._isAuthenticated.set(true);
    this._currentUser.set(response.user);

    this.tokenSubject.next(response.accessToken);
    this.userSubject.next(response.user);

    localStorage.setItem(this.TOKEN_KEY, response.accessToken);
    localStorage.setItem(this.TOKEN_EXPIRY_KEY, new Date(response.expiresAt).toISOString());

    // Schedule token refresh 1 minute before expiry
    this.scheduleTokenRefresh(response.expiresAt);
  }

  private scheduleTokenRefresh(expiresAt: Date): void {
    const expiryTime = new Date(expiresAt).getTime();
    const now = Date.now();
    const refreshTime = expiryTime - now - (60 * 1000); // 1 minute before expiry

    if (refreshTime > 0) {
      setTimeout(() => {
        if (this._isAuthenticated()) {
          this.refreshToken().subscribe({
            error: () => console.log('Token refresh failed')
          });
        }
      }, refreshTime);
    }
  }

  private clearSession(): void {
    this.clearTokens();
    this._isAuthenticated.set(false);
    this._currentUser.set(null);
    this.userSubject.next(null);
    this.tokenSubject.next(null);
    this.router.navigate(['/login']);
  }

  private clearTokens(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.TOKEN_EXPIRY_KEY);
  }
}
