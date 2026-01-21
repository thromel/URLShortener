import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Organization {
  id: string;
  name: string;
  slug: string;
  ownerId: string;
  ownerName: string;
  createdAt: Date;
  isActive: boolean;
  memberCount: number;
  urlCount: number;
}

export interface OrganizationDetail extends Organization {
  settings: Record<string, unknown>;
  members: Member[];
  roles: Role[];
}

export interface Member {
  id: string;
  userId: string;
  email: string;
  displayName: string;
  roleId: string;
  roleName: string;
  joinedAt: Date;
  permissions: string[];
}

export interface Role {
  id: string;
  name: string;
  description: string;
  isSystem: boolean;
  permissions: string[];
  memberCount: number;
}

export interface CreateOrganizationRequest {
  name: string;
  slug: string;
}

export interface UpdateOrganizationRequest {
  name: string;
  settings?: Record<string, unknown>;
}

export interface InviteMemberRequest {
  email: string;
  roleId: string;
}

export interface UpdateMemberRoleRequest {
  roleId: string;
}

export interface CreateRoleRequest {
  name: string;
  description?: string;
  permissions: string[];
}

export interface UpdateRoleRequest {
  name?: string;
  description?: string;
  permissions?: string[];
}

@Injectable({
  providedIn: 'root'
})
export class OrganizationService {
  private readonly baseUrl = '/api/v1/organizations';

  constructor(private http: HttpClient) {}

  // Organization CRUD
  getMyOrganizations(): Observable<Organization[]> {
    return this.http.get<Organization[]>(this.baseUrl);
  }

  createOrganization(request: CreateOrganizationRequest): Observable<OrganizationDetail> {
    return this.http.post<OrganizationDetail>(this.baseUrl, request);
  }

  getOrganization(id: string): Observable<OrganizationDetail> {
    return this.http.get<OrganizationDetail>(`${this.baseUrl}/${id}`);
  }

  getOrganizationBySlug(slug: string): Observable<OrganizationDetail> {
    return this.http.get<OrganizationDetail>(`${this.baseUrl}/by-slug/${slug}`);
  }

  updateOrganization(id: string, request: UpdateOrganizationRequest): Observable<OrganizationDetail> {
    return this.http.put<OrganizationDetail>(`${this.baseUrl}/${id}`, request);
  }

  deleteOrganization(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  // Member management
  getMembers(orgId: string): Observable<Member[]> {
    return this.http.get<Member[]>(`${this.baseUrl}/${orgId}/members`);
  }

  addMember(orgId: string, request: InviteMemberRequest): Observable<Member> {
    return this.http.post<Member>(`${this.baseUrl}/${orgId}/members`, request);
  }

  updateMemberRole(orgId: string, memberId: string, request: UpdateMemberRoleRequest): Observable<Member> {
    return this.http.put<Member>(`${this.baseUrl}/${orgId}/members/${memberId}`, request);
  }

  removeMember(orgId: string, memberId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${orgId}/members/${memberId}`);
  }

  // Role management
  getRoles(orgId: string): Observable<Role[]> {
    return this.http.get<Role[]>(`${this.baseUrl}/${orgId}/roles`);
  }

  createRole(orgId: string, request: CreateRoleRequest): Observable<Role> {
    return this.http.post<Role>(`${this.baseUrl}/${orgId}/roles`, request);
  }

  updateRole(orgId: string, roleId: string, request: UpdateRoleRequest): Observable<Role> {
    return this.http.put<Role>(`${this.baseUrl}/${orgId}/roles/${roleId}`, request);
  }

  deleteRole(orgId: string, roleId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${orgId}/roles/${roleId}`);
  }

  // Get available permissions
  getAvailablePermissions(): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/permissions`);
  }

  // Helper to generate slug from name
  generateSlug(name: string): string {
    return name
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-+|-+$/g, '');
  }
}
