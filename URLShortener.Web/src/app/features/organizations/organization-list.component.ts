import { Component, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { OrganizationService, Organization } from '../../core/services/organization.service';

@Component({
  selector: 'app-organization-list',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="container">
      <div class="header">
        <div>
          <h1>Organizations</h1>
          <p class="subtitle">Manage your teams and collaborations</p>
        </div>
        <a routerLink="/organizations/new" class="btn-primary">
          <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="12" y1="5" x2="12" y2="19"></line>
            <line x1="5" y1="12" x2="19" y2="12"></line>
          </svg>
          New Organization
        </a>
      </div>

      @if (isLoading()) {
        <div class="loading">
          <div class="spinner-large"></div>
          <p>Loading organizations...</p>
        </div>
      } @else if (error()) {
        <div class="error-state">
          <p>{{ error() }}</p>
          <button (click)="loadOrganizations()" class="btn-secondary">Retry</button>
        </div>
      } @else if (organizations().length === 0) {
        <div class="empty-state">
          <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1">
            <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
            <circle cx="9" cy="7" r="4"></circle>
            <path d="M23 21v-2a4 4 0 0 0-3-3.87"></path>
            <path d="M16 3.13a4 4 0 0 1 0 7.75"></path>
          </svg>
          <h2>No organizations yet</h2>
          <p>Create your first organization to start collaborating with your team.</p>
          <a routerLink="/organizations/new" class="btn-primary">Create Organization</a>
        </div>
      } @else {
        <div class="org-grid">
          @for (org of organizations(); track org.id) {
            <a [routerLink]="['/organizations', org.slug]" class="org-card">
              <div class="org-avatar">
                {{ getInitials(org.name) }}
              </div>
              <div class="org-info">
                <h3>{{ org.name }}</h3>
                <span class="org-slug">{{ org.slug }}</span>
              </div>
              <div class="org-stats">
                <div class="stat">
                  <span class="stat-value">{{ org.memberCount }}</span>
                  <span class="stat-label">Members</span>
                </div>
                <div class="stat">
                  <span class="stat-value">{{ org.urlCount }}</span>
                  <span class="stat-label">URLs</span>
                </div>
              </div>
              <div class="org-meta">
                <span class="owner-badge" *ngIf="org.ownerId">
                  Owner: {{ org.ownerName }}
                </span>
              </div>
            </a>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .container {
      max-width: 1200px;
      margin: 0 auto;
      padding: 2rem;
    }

    .header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 2rem;
    }

    .header h1 {
      margin: 0;
      font-size: 1.75rem;
      color: #1a202c;
    }

    .subtitle {
      margin: 0.5rem 0 0;
      color: #6b7280;
    }

    .btn-primary {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
      border: none;
      padding: 0.75rem 1.25rem;
      border-radius: 8px;
      font-size: 0.875rem;
      font-weight: 500;
      text-decoration: none;
      cursor: pointer;
      transition: opacity 0.2s;
    }

    .btn-primary:hover {
      opacity: 0.9;
    }

    .btn-secondary {
      background: white;
      color: #374151;
      border: 1px solid #d1d5db;
      padding: 0.75rem 1.25rem;
      border-radius: 8px;
      font-size: 0.875rem;
      font-weight: 500;
      cursor: pointer;
    }

    .loading, .empty-state, .error-state {
      text-align: center;
      padding: 4rem 2rem;
      background: #f9fafb;
      border-radius: 12px;
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

    @keyframes spin {
      to { transform: rotate(360deg); }
    }

    .empty-state svg {
      color: #9ca3af;
      margin-bottom: 1rem;
    }

    .empty-state h2 {
      margin: 0 0 0.5rem;
      color: #374151;
    }

    .empty-state p {
      margin: 0 0 1.5rem;
      color: #6b7280;
    }

    .error-state {
      color: #dc2626;
    }

    .org-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
      gap: 1.5rem;
    }

    .org-card {
      background: white;
      border: 1px solid #e5e7eb;
      border-radius: 12px;
      padding: 1.5rem;
      text-decoration: none;
      color: inherit;
      transition: transform 0.2s, box-shadow 0.2s;
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .org-card:hover {
      transform: translateY(-2px);
      box-shadow: 0 8px 25px rgba(0, 0, 0, 0.1);
    }

    .org-avatar {
      width: 56px;
      height: 56px;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
      color: white;
      font-size: 1.25rem;
      font-weight: 600;
    }

    .org-info h3 {
      margin: 0;
      font-size: 1.125rem;
      color: #1a202c;
    }

    .org-slug {
      font-size: 0.875rem;
      color: #6b7280;
    }

    .org-stats {
      display: flex;
      gap: 2rem;
      padding: 1rem 0;
      border-top: 1px solid #f3f4f6;
      border-bottom: 1px solid #f3f4f6;
    }

    .stat {
      display: flex;
      flex-direction: column;
    }

    .stat-value {
      font-size: 1.25rem;
      font-weight: 600;
      color: #1a202c;
    }

    .stat-label {
      font-size: 0.75rem;
      color: #6b7280;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .org-meta {
      font-size: 0.875rem;
      color: #6b7280;
    }

    .owner-badge {
      background: #f3f4f6;
      padding: 0.25rem 0.5rem;
      border-radius: 4px;
    }
  `]
})
export class OrganizationListComponent implements OnInit {
  organizations = signal<Organization[]>([]);
  isLoading = signal(true);
  error = signal<string | null>(null);

  constructor(private orgService: OrganizationService) {}

  ngOnInit(): void {
    this.loadOrganizations();
  }

  loadOrganizations(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.orgService.getMyOrganizations().subscribe({
      next: (orgs) => {
        this.organizations.set(orgs);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.error || 'Failed to load organizations');
        this.isLoading.set(false);
      }
    });
  }

  getInitials(name: string): string {
    return name
      .split(' ')
      .map(word => word[0])
      .join('')
      .toUpperCase()
      .slice(0, 2);
  }
}
