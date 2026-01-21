# Authentication & Multi-Tenant Authorization Design

## Overview

Self-contained JWT authentication with multi-tenant organization support and granular role-based permissions.

## Key Decisions

- **Auth Type**: Self-contained JWT (no external provider)
- **Tenant Model**: Multiple organizations per user
- **Permission Model**: Granular permissions with custom roles

---

## Data Model

### Core Entities

```
User
├── Id (GUID)
├── Email (unique)
├── PasswordHash
├── DisplayName
├── CreatedAt
├── LastLoginAt
└── IsActive

Organization
├── Id (GUID)
├── Name
├── Slug (unique, for URLs)
├── CreatedAt
├── OwnerId (FK → User)
└── Settings (JSON)

OrganizationMember
├── OrganizationId (FK)
├── UserId (FK)
├── RoleId (FK)
└── JoinedAt

Role
├── Id (GUID)
├── OrganizationId (FK, nullable for system roles)
├── Name
├── IsSystem (Owner, Admin, Member defaults)
└── Permissions (JSON array)

RefreshToken
├── Id (GUID)
├── UserId (FK)
├── Token (hashed)
├── ExpiresAt
├── CreatedAt
├── RevokedAt (nullable)
└── ReplacedByTokenId (nullable, for rotation)
```

### Permission Types

```
URL Operations:
- url:create
- url:read
- url:update
- url:delete

Analytics:
- analytics:view
- analytics:export

Member Management:
- members:view
- members:invite
- members:remove
- members:manage-roles

Organization:
- org:settings
- org:billing
- org:delete
```

### URL Ownership

- URLs have `UserId` (creator) AND `OrganizationId` (nullable)
- Personal URLs: `OrganizationId = null`
- Org URLs: `OrganizationId` set, visible to members with `url:read`

---

## API Endpoints

### Auth Endpoints (`/api/v1/auth`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /register | Create account, return tokens |
| POST | /login | Email/password, return tokens |
| POST | /refresh | Refresh token → new access token |
| POST | /logout | Invalidate refresh token |
| GET | /me | Current user profile |
| PUT | /me | Update profile |
| POST | /change-password | Update password |

### Organization Endpoints (`/api/v1/organizations`)

| Method | Endpoint | Permission Required |
|--------|----------|---------------------|
| GET | / | (authenticated) |
| POST | / | (authenticated) |
| GET | /{slug} | (member) |
| PUT | /{slug} | org:settings |
| DELETE | /{slug} | (owner only) |
| GET | /{slug}/members | members:view |
| POST | /{slug}/members | members:invite |
| PUT | /{slug}/members/{id} | members:manage-roles |
| DELETE | /{slug}/members/{id} | members:remove |
| GET | /{slug}/roles | members:view |
| POST | /{slug}/roles | members:manage-roles |
| PUT | /{slug}/roles/{id} | members:manage-roles |
| DELETE | /{slug}/roles/{id} | members:manage-roles |

### URL Endpoints (Modified)

- Add `?orgId={slug}` query param to scope to organization
- Without `orgId`, operates on personal URLs
- Permission checks based on org membership and role
- `X-Organization: {slug}` header for org context

---

## Frontend Architecture

### Services

```typescript
AuthService
├── login(email, password) → tokens
├── register(email, password, name) → tokens
├── logout()
├── refreshToken()
├── currentUser$ (Observable)
└── isAuthenticated$ (Observable)

OrganizationService
├── list() → user's orgs
├── create(name) → new org
├── switchContext(orgSlug) → set active org
├── activeOrg$ (Observable)
└── permissions$ (Observable<string[]>)
```

### New Routes

| Route | Component | Guard |
|-------|-----------|-------|
| /auth/login | LoginComponent | Guest only |
| /auth/register | RegisterComponent | Guest only |
| /settings/profile | ProfileComponent | AuthGuard |
| /settings/orgs | OrgsListComponent | AuthGuard |
| /org/{slug}/settings | OrgSettingsComponent | PermissionGuard(org:settings) |
| /org/{slug}/members | MembersComponent | PermissionGuard(members:view) |
| /org/{slug}/roles | RolesComponent | PermissionGuard(members:manage-roles) |

### Infrastructure

- `AuthInterceptor` - Adds Bearer token, handles 401 → refresh
- `AuthGuard` - Protects authenticated routes
- `PermissionGuard` - Checks specific permissions
- `OrgContextGuard` - Ensures org context is set
- `*hasPermission` directive - Conditional UI rendering

### UI Changes

- Sidebar: Organization switcher dropdown
- Header: User avatar/menu with logout
- Permission-based visibility for actions

---

## Security

### Token Strategy

**Access Token (JWT)**
- Expires: 15 minutes
- Contains: userId, email, activeOrgId, permissions[]
- Storage: Memory only (lost on page refresh - intentional)

**Refresh Token**
- Expires: 7 days
- Storage: HttpOnly cookie
- One-time use with rotation
- Revocable (stored in database)

### Auth Flows

**Login:**
1. Submit email/password
2. Backend validates, returns access token + sets refresh cookie
3. Frontend stores access token in memory
4. Redirect to dashboard

**Page Refresh:**
1. Access token lost (memory cleared)
2. Silent refresh call using HttpOnly cookie
3. New access token returned
4. Resume authenticated state

**API Request:**
1. Interceptor adds `Authorization: Bearer {token}`
2. On 401 → attempt refresh
3. Refresh fails → redirect to login

### Security Measures

- Password hashing: bcrypt, cost factor 12
- Rate limiting: 5 failed logins → 15 min lockout
- Refresh tokens invalidated on password change
- CORS restricted to frontend origin
- CSRF protection via SameSite cookies

---

## Implementation Order

### Phase 1: Backend Auth
1. Create User, RefreshToken entities
2. Add EF migrations
3. Create AuthController (register, login, refresh, logout)
4. JWT token generation service
5. Auth middleware configuration

### Phase 2: Backend Multi-Tenant
1. Create Organization, OrganizationMember, Role entities
2. Add EF migrations
3. Create OrganizationsController
4. Permission checking middleware
5. Update URL endpoints with org context

### Phase 3: Frontend Auth
1. AuthService with token management
2. HTTP interceptor
3. Login/Register pages
4. Auth guards
5. Update app layout with user menu

### Phase 4: Frontend Multi-Tenant
1. OrganizationService
2. Org switcher component
3. Org settings pages
4. Member management pages
5. Permission directive

---

## Default System Roles

| Role | Permissions |
|------|-------------|
| Owner | All permissions + org:delete |
| Admin | All except org:delete |
| Member | url:create, url:read, url:update (own), analytics:view |
