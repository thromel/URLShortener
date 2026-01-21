import { Component, signal, OnInit, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { OrganizationService, OrganizationDetail, Member, Role } from '../../core/services/organization.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-organization-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  template: `
    <div class="container">
      @if (isLoading()) {
        <div class="loading">
          <div class="spinner-large"></div>
          <p>Loading organization...</p>
        </div>
      } @else if (error()) {
        <div class="error-state">
          <p>{{ error() }}</p>
          <a routerLink="/organizations" class="btn-secondary">Back to Organizations</a>
        </div>
      } @else if (organization()) {
        <div class="header">
          <a routerLink="/organizations" class="back-link">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <line x1="19" y1="12" x2="5" y2="12"></line>
              <polyline points="12 19 5 12 12 5"></polyline>
            </svg>
            Organizations
          </a>
          <div class="org-header">
            <div class="org-avatar">{{ getInitials(organization()!.name) }}</div>
            <div>
              <h1>{{ organization()!.name }}</h1>
              <span class="org-slug">/{{ organization()!.slug }}</span>
            </div>
          </div>
        </div>

        <div class="stats-row">
          <div class="stat-card">
            <span class="stat-value">{{ organization()!.members.length }}</span>
            <span class="stat-label">Members</span>
          </div>
          <div class="stat-card">
            <span class="stat-value">{{ organization()!.roles.length }}</span>
            <span class="stat-label">Roles</span>
          </div>
          <div class="stat-card">
            <span class="stat-value">{{ urlCount() }}</span>
            <span class="stat-label">URLs</span>
          </div>
        </div>

        <div class="tabs">
          <button
            [class.active]="activeTab() === 'members'"
            (click)="activeTab.set('members')">
            Members
          </button>
          <button
            [class.active]="activeTab() === 'roles'"
            (click)="activeTab.set('roles')">
            Roles
          </button>
          <button
            [class.active]="activeTab() === 'settings'"
            (click)="activeTab.set('settings')"
            *ngIf="canManageSettings()">
            Settings
          </button>
        </div>

        <div class="tab-content">
          @if (activeTab() === 'members') {
            <div class="section-header">
              <h2>Members ({{ organization()!.members.length }})</h2>
              @if (canInviteMembers()) {
                <button class="btn-primary btn-sm" (click)="showInviteModal = true">
                  Invite Member
                </button>
              }
            </div>

            <div class="members-list">
              @for (member of organization()!.members; track member.id) {
                <div class="member-card">
                  <div class="member-avatar">{{ getInitials(member.displayName) }}</div>
                  <div class="member-info">
                    <span class="member-name">{{ member.displayName }}</span>
                    <span class="member-email">{{ member.email }}</span>
                  </div>
                  <div class="member-role">
                    @if (canManageRoles() && member.roleName !== 'Owner') {
                      <select
                        [value]="member.roleId"
                        (change)="updateMemberRole(member.id, $event)">
                        @for (role of organization()!.roles; track role.id) {
                          @if (role.name !== 'Owner') {
                            <option [value]="role.id">{{ role.name }}</option>
                          }
                        }
                      </select>
                    } @else {
                      <span class="role-badge" [class.owner]="member.roleName === 'Owner'">
                        {{ member.roleName }}
                      </span>
                    }
                  </div>
                  @if (canRemoveMembers() && member.roleName !== 'Owner') {
                    <button class="btn-icon danger" (click)="removeMember(member.id)">
                      <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="3 6 5 6 21 6"></polyline>
                        <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
                      </svg>
                    </button>
                  }
                </div>
              }
            </div>
          }

          @if (activeTab() === 'roles') {
            <div class="section-header">
              <h2>Roles ({{ organization()!.roles.length }})</h2>
              @if (canManageRoles()) {
                <button class="btn-primary btn-sm" (click)="showRoleModal = true">
                  Create Role
                </button>
              }
            </div>

            <div class="roles-list">
              @for (role of organization()!.roles; track role.id) {
                <div class="role-card" [class.system]="role.isSystem">
                  <div class="role-header">
                    <h3>{{ role.name }}</h3>
                    @if (role.isSystem) {
                      <span class="system-badge">System</span>
                    }
                  </div>
                  <p class="role-description">{{ role.description }}</p>
                  <div class="role-meta">
                    <span>{{ role.memberCount }} members</span>
                    <span>{{ role.permissions.length }} permissions</span>
                  </div>
                  <div class="permissions-preview">
                    @for (perm of role.permissions.slice(0, 3); track perm) {
                      <span class="perm-badge">{{ formatPermission(perm) }}</span>
                    }
                    @if (role.permissions.length > 3) {
                      <span class="perm-more">+{{ role.permissions.length - 3 }} more</span>
                    }
                  </div>
                </div>
              }
            </div>
          }

          @if (activeTab() === 'settings') {
            <div class="settings-section">
              <h2>Organization Settings</h2>

              <div class="form-group">
                <label>Organization Name</label>
                <input type="text" [(ngModel)]="editName" />
              </div>

              <div class="actions">
                <button
                  class="btn-primary"
                  (click)="saveSettings()"
                  [disabled]="isSaving()">
                  @if (isSaving()) {
                    Saving...
                  } @else {
                    Save Changes
                  }
                </button>
              </div>

              @if (isOwner()) {
                <div class="danger-zone">
                  <h3>Danger Zone</h3>
                  <p>Deleting this organization will deactivate it and remove access for all members.</p>
                  <button class="btn-danger" (click)="confirmDelete()">Delete Organization</button>
                </div>
              }
            </div>
          }
        </div>
      }

      @if (showInviteModal) {
        <div class="modal-overlay" (click)="showInviteModal = false">
          <div class="modal" (click)="$event.stopPropagation()">
            <h2>Invite Member</h2>
            <div class="form-group">
              <label>Email Address</label>
              <input type="email" [(ngModel)]="inviteEmail" placeholder="user@example.com" />
            </div>
            <div class="form-group">
              <label>Role</label>
              <select [(ngModel)]="inviteRoleId">
                @for (role of organization()?.roles; track role.id) {
                  @if (role.name !== 'Owner') {
                    <option [value]="role.id">{{ role.name }}</option>
                  }
                }
              </select>
            </div>
            <div class="modal-actions">
              <button class="btn-secondary" (click)="showInviteModal = false">Cancel</button>
              <button class="btn-primary" (click)="inviteMember()" [disabled]="!inviteEmail || !inviteRoleId">
                Send Invite
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .container {
      max-width: 1000px;
      margin: 0 auto;
      padding: 2rem;
    }

    .loading, .error-state {
      text-align: center;
      padding: 4rem 2rem;
    }

    .spinner-large {
      width: 40px;
      height: 40px;
      border: 3px solid #e5e7eb;
      border-top-color: #667eea;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
      margin: 0 auto 1rem;
    }

    @keyframes spin { to { transform: rotate(360deg); } }

    .header { margin-bottom: 2rem; }

    .back-link {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      color: #6b7280;
      text-decoration: none;
      font-size: 0.875rem;
      margin-bottom: 1rem;
    }

    .org-header {
      display: flex;
      align-items: center;
      gap: 1rem;
    }

    .org-avatar {
      width: 64px;
      height: 64px;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
      color: white;
      font-size: 1.5rem;
      font-weight: 600;
    }

    .org-header h1 { margin: 0; font-size: 1.75rem; }
    .org-slug { color: #6b7280; font-size: 0.875rem; }

    .stats-row {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 1rem;
      margin-bottom: 2rem;
    }

    .stat-card {
      background: white;
      border: 1px solid #e5e7eb;
      border-radius: 8px;
      padding: 1.5rem;
      text-align: center;
    }

    .stat-value { display: block; font-size: 2rem; font-weight: 600; color: #1a202c; }
    .stat-label { font-size: 0.875rem; color: #6b7280; }

    .tabs {
      display: flex;
      gap: 0.5rem;
      border-bottom: 1px solid #e5e7eb;
      margin-bottom: 1.5rem;
    }

    .tabs button {
      padding: 0.75rem 1.5rem;
      border: none;
      background: none;
      color: #6b7280;
      font-size: 0.875rem;
      font-weight: 500;
      cursor: pointer;
      border-bottom: 2px solid transparent;
      margin-bottom: -1px;
    }

    .tabs button.active {
      color: #667eea;
      border-bottom-color: #667eea;
    }

    .section-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1rem;
    }

    .section-header h2 { margin: 0; font-size: 1.25rem; }

    .btn-primary {
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
      border: none;
      padding: 0.625rem 1rem;
      border-radius: 6px;
      font-size: 0.875rem;
      cursor: pointer;
    }

    .btn-sm { padding: 0.5rem 0.875rem; font-size: 0.8125rem; }
    .btn-secondary { background: white; border: 1px solid #d1d5db; color: #374151; padding: 0.625rem 1rem; border-radius: 6px; cursor: pointer; }
    .btn-danger { background: #ef4444; color: white; border: none; padding: 0.625rem 1rem; border-radius: 6px; cursor: pointer; }
    .btn-icon { background: none; border: none; padding: 0.5rem; cursor: pointer; color: #6b7280; border-radius: 6px; }
    .btn-icon:hover { background: #f3f4f6; }
    .btn-icon.danger:hover { background: #fef2f2; color: #ef4444; }

    .members-list { display: flex; flex-direction: column; gap: 0.75rem; }

    .member-card {
      display: flex;
      align-items: center;
      gap: 1rem;
      background: white;
      border: 1px solid #e5e7eb;
      border-radius: 8px;
      padding: 1rem;
    }

    .member-avatar {
      width: 40px;
      height: 40px;
      background: #e5e7eb;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 500;
      color: #374151;
    }

    .member-info { flex: 1; }
    .member-name { display: block; font-weight: 500; }
    .member-email { font-size: 0.875rem; color: #6b7280; }

    .member-role select {
      padding: 0.375rem 0.75rem;
      border: 1px solid #d1d5db;
      border-radius: 6px;
      font-size: 0.875rem;
    }

    .role-badge {
      display: inline-block;
      padding: 0.25rem 0.75rem;
      background: #f3f4f6;
      border-radius: 999px;
      font-size: 0.8125rem;
    }

    .role-badge.owner { background: #fef3c7; color: #92400e; }

    .roles-list {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 1rem;
    }

    .role-card {
      background: white;
      border: 1px solid #e5e7eb;
      border-radius: 8px;
      padding: 1rem;
    }

    .role-card.system { border-color: #fde68a; }
    .role-header { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.5rem; }
    .role-header h3 { margin: 0; font-size: 1rem; }
    .system-badge { background: #fef3c7; color: #92400e; padding: 0.125rem 0.5rem; border-radius: 4px; font-size: 0.75rem; }
    .role-description { margin: 0; font-size: 0.875rem; color: #6b7280; }
    .role-meta { margin: 0.75rem 0; font-size: 0.8125rem; color: #9ca3af; display: flex; gap: 1rem; }

    .permissions-preview { display: flex; flex-wrap: wrap; gap: 0.25rem; }
    .perm-badge { background: #f0f9ff; color: #0369a1; padding: 0.125rem 0.5rem; border-radius: 4px; font-size: 0.75rem; }
    .perm-more { color: #9ca3af; font-size: 0.75rem; padding: 0.125rem 0.5rem; }

    .settings-section { max-width: 500px; }
    .form-group { margin-bottom: 1.5rem; }
    .form-group label { display: block; font-size: 0.875rem; font-weight: 500; margin-bottom: 0.5rem; }
    .form-group input, .form-group select { width: 100%; padding: 0.625rem; border: 1px solid #d1d5db; border-radius: 6px; font-size: 1rem; }

    .danger-zone {
      margin-top: 3rem;
      padding: 1.5rem;
      border: 1px solid #fecaca;
      border-radius: 8px;
      background: #fef2f2;
    }

    .danger-zone h3 { margin: 0 0 0.5rem; color: #dc2626; }
    .danger-zone p { margin: 0 0 1rem; font-size: 0.875rem; color: #7f1d1d; }

    .modal-overlay {
      position: fixed;
      inset: 0;
      background: rgba(0,0,0,0.5);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 50;
    }

    .modal {
      background: white;
      border-radius: 12px;
      padding: 1.5rem;
      width: 100%;
      max-width: 400px;
    }

    .modal h2 { margin: 0 0 1.5rem; }
    .modal-actions { display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 1.5rem; }
  `]
})
export class OrganizationDetailComponent implements OnInit {
  @Input() slug!: string;

  organization = signal<OrganizationDetail | null>(null);
  isLoading = signal(true);
  error = signal<string | null>(null);
  activeTab = signal<'members' | 'roles' | 'settings'>('members');
  isSaving = signal(false);
  urlCount = signal(0);

  editName = '';
  showInviteModal = false;
  showRoleModal = false;
  inviteEmail = '';
  inviteRoleId = '';

  constructor(
    private orgService: OrganizationService,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    this.loadOrganization();
  }

  loadOrganization(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.orgService.getOrganizationBySlug(this.slug).subscribe({
      next: (org) => {
        this.organization.set(org);
        this.editName = org.name;
        // Set default role for invite
        const memberRole = org.roles.find(r => r.name === 'Member');
        if (memberRole) this.inviteRoleId = memberRole.id;
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.error || 'Failed to load organization');
        this.isLoading.set(false);
      }
    });
  }

  getInitials(name: string): string {
    return name.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2);
  }

  formatPermission(perm: string): string {
    return perm.split(':').map(p => p.charAt(0).toUpperCase() + p.slice(1)).join(' ');
  }

  canInviteMembers(): boolean {
    return this.hasPermission('members:invite');
  }

  canRemoveMembers(): boolean {
    return this.hasPermission('members:remove');
  }

  canManageRoles(): boolean {
    return this.hasPermission('members:manage-roles');
  }

  canManageSettings(): boolean {
    return this.hasPermission('org:settings');
  }

  isOwner(): boolean {
    const org = this.organization();
    if (!org) return false;
    return this.authService.isOwnerOf(org.id);
  }

  private hasPermission(permission: string): boolean {
    const org = this.organization();
    if (!org) return false;
    return this.authService.hasPermission(org.id, permission);
  }

  updateMemberRole(memberId: string, event: Event): void {
    const roleId = (event.target as HTMLSelectElement).value;
    const org = this.organization();
    if (!org) return;

    this.orgService.updateMemberRole(org.id, memberId, { roleId }).subscribe({
      next: () => this.loadOrganization(),
      error: (err) => alert(err.error?.error || 'Failed to update role')
    });
  }

  removeMember(memberId: string): void {
    if (!confirm('Remove this member from the organization?')) return;

    const org = this.organization();
    if (!org) return;

    this.orgService.removeMember(org.id, memberId).subscribe({
      next: () => this.loadOrganization(),
      error: (err) => alert(err.error?.error || 'Failed to remove member')
    });
  }

  inviteMember(): void {
    const org = this.organization();
    if (!org || !this.inviteEmail || !this.inviteRoleId) return;

    this.orgService.addMember(org.id, {
      email: this.inviteEmail,
      roleId: this.inviteRoleId
    }).subscribe({
      next: () => {
        this.showInviteModal = false;
        this.inviteEmail = '';
        this.loadOrganization();
      },
      error: (err) => alert(err.error?.error || 'Failed to invite member')
    });
  }

  saveSettings(): void {
    const org = this.organization();
    if (!org) return;

    this.isSaving.set(true);

    this.orgService.updateOrganization(org.id, { name: this.editName }).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.loadOrganization();
      },
      error: (err) => {
        this.isSaving.set(false);
        alert(err.error?.error || 'Failed to save settings');
      }
    });
  }

  confirmDelete(): void {
    const org = this.organization();
    if (!org) return;

    if (confirm(`Are you sure you want to delete "${org.name}"? This action cannot be undone.`)) {
      this.orgService.deleteOrganization(org.id).subscribe({
        next: () => window.location.href = '/organizations',
        error: (err) => alert(err.error?.error || 'Failed to delete organization')
      });
    }
  }
}
