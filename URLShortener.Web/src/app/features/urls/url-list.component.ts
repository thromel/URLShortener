import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiClient } from '../../core/api/api-client.generated';

interface UrlItem {
  shortCode: string;
  originalUrl: string;
  clicks: number;
  createdAt: Date;
  status: string;
}

@Component({
  selector: 'app-url-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  template: `
    <div class="space-y-6">
      <!-- Header -->
      <div class="flex items-center justify-between">
        <div>
          <h1 class="text-2xl font-bold text-gray-900">My URLs</h1>
          <p class="text-gray-500 mt-1">Manage your shortened URLs</p>
        </div>
        <a routerLink="/urls/new" class="btn btn-primary">
          <svg class="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
          </svg>
          Create URL
        </a>
      </div>

      <!-- Filters -->
      <div class="card">
        <div class="card-body">
          <div class="flex flex-wrap items-center gap-4">
            <div class="flex-1 min-w-[200px]">
              <input type="text"
                     class="input"
                     placeholder="Search URLs..."
                     [(ngModel)]="searchTerm"
                     (input)="onSearch()">
            </div>
            <select class="input w-40" [(ngModel)]="statusFilter" (change)="loadUrls()">
              <option value="">All Status</option>
              <option value="Active">Active</option>
              <option value="Expired">Expired</option>
              <option value="Disabled">Disabled</option>
            </select>
            <select class="input w-32" [(ngModel)]="pageSize" (change)="loadUrls()">
              <option [value]="10">10</option>
              <option [value]="25">25</option>
              <option [value]="50">50</option>
            </select>
          </div>
        </div>
      </div>

      <!-- Bulk Actions (when items selected) -->
      @if (selectedUrls().length > 0) {
        <div class="bg-primary-50 border border-primary-200 rounded-lg p-4 flex items-center justify-between">
          <span class="text-primary-700 font-medium">{{ selectedUrls().length }} URL(s) selected</span>
          <div class="flex items-center gap-2">
            <button class="btn btn-secondary btn-sm" (click)="exportSelected()">
              <svg class="w-4 h-4 mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
              Export
            </button>
            <button class="btn btn-danger btn-sm" (click)="deleteSelected()">
              <svg class="w-4 h-4 mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
              </svg>
              Delete
            </button>
            <button class="btn btn-ghost btn-sm" (click)="clearSelection()">Clear</button>
          </div>
        </div>
      }

      <!-- Table -->
      <div class="card overflow-hidden">
        <table class="table">
          <thead>
            <tr>
              <th class="w-10">
                <input type="checkbox"
                       class="rounded border-gray-300"
                       [checked]="allSelected()"
                       (change)="toggleSelectAll()">
              </th>
              <th>Short Code</th>
              <th>Original URL</th>
              <th>Clicks</th>
              <th>Created</th>
              <th>Status</th>
              <th class="w-20">Actions</th>
            </tr>
          </thead>
          <tbody>
            @if (loading()) {
              @for (i of [1,2,3,4,5]; track i) {
                <tr class="animate-pulse">
                  <td><div class="h-4 w-4 bg-gray-200 rounded"></div></td>
                  <td><div class="h-4 bg-gray-200 rounded w-20"></div></td>
                  <td><div class="h-4 bg-gray-200 rounded w-48"></div></td>
                  <td><div class="h-4 bg-gray-200 rounded w-12"></div></td>
                  <td><div class="h-4 bg-gray-200 rounded w-24"></div></td>
                  <td><div class="h-4 bg-gray-200 rounded w-16"></div></td>
                  <td><div class="h-4 bg-gray-200 rounded w-8"></div></td>
                </tr>
              }
            } @else if (urls().length === 0) {
              <tr>
                <td colspan="7" class="text-center py-12">
                  <svg class="w-12 h-12 text-gray-300 mx-auto mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1" />
                  </svg>
                  <p class="text-gray-500 mb-4">No URLs found</p>
                  <a routerLink="/urls/new" class="btn btn-primary">Create your first URL</a>
                </td>
              </tr>
            } @else {
              @for (url of urls(); track url.shortCode) {
                <tr>
                  <td>
                    <input type="checkbox"
                           class="rounded border-gray-300"
                           [checked]="isSelected(url.shortCode)"
                           (change)="toggleSelect(url.shortCode)">
                  </td>
                  <td>
                    <a [routerLink]="['/urls', url.shortCode]" class="text-primary-600 hover:underline font-medium">
                      {{ url.shortCode }}
                    </a>
                  </td>
                  <td>
                    <div class="max-w-xs truncate" [title]="url.originalUrl">
                      {{ url.originalUrl }}
                    </div>
                  </td>
                  <td>{{ url.clicks | number }}</td>
                  <td>{{ url.createdAt | date:'mediumDate' }}</td>
                  <td>
                    <span class="badge"
                          [class.badge-success]="url.status === 'Active'"
                          [class.badge-warning]="url.status === 'Expired'"
                          [class.badge-danger]="url.status === 'Disabled'">
                      {{ url.status }}
                    </span>
                  </td>
                  <td>
                    <div class="flex items-center gap-1">
                      <button class="btn btn-ghost btn-sm p-1" title="Copy" (click)="copyUrl(url.shortCode)">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
                        </svg>
                      </button>
                      <a [routerLink]="['/urls', url.shortCode]" class="btn btn-ghost btn-sm p-1" title="View">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                        </svg>
                      </a>
                    </div>
                  </td>
                </tr>
              }
            }
          </tbody>
        </table>
      </div>

      <!-- Pagination -->
      @if (totalPages() > 1) {
        <div class="flex items-center justify-between">
          <p class="text-sm text-gray-500">
            Showing {{ (currentPage() - 1) * pageSize + 1 }} to {{ Math.min(currentPage() * pageSize, totalItems()) }} of {{ totalItems() }} results
          </p>
          <div class="flex items-center gap-2">
            <button class="btn btn-secondary btn-sm"
                    [disabled]="currentPage() === 1"
                    (click)="goToPage(currentPage() - 1)">
              Previous
            </button>
            @for (page of visiblePages(); track page) {
              <button class="btn btn-sm"
                      [class.btn-primary]="page === currentPage()"
                      [class.btn-ghost]="page !== currentPage()"
                      (click)="goToPage(page)">
                {{ page }}
              </button>
            }
            <button class="btn btn-secondary btn-sm"
                    [disabled]="currentPage() === totalPages()"
                    (click)="goToPage(currentPage() + 1)">
              Next
            </button>
          </div>
        </div>
      }
    </div>
  `
})
export class UrlListComponent implements OnInit {
  private api = inject(ApiClient);

  Math = Math;

  urls = signal<UrlItem[]>([]);
  loading = signal(true);
  selectedUrls = signal<string[]>([]);

  searchTerm = '';
  statusFilter = '';
  pageSize = 10;
  currentPage = signal(1);
  totalItems = signal(0);
  totalPages = signal(1);

  ngOnInit() {
    this.loadUrls();
  }

  loadUrls() {
    this.loading.set(true);
    const skip = (this.currentPage() - 1) * this.pageSize;
    this.api.getMyUrls('1', skip, this.pageSize).subscribe({
      next: (data) => {
        const urls: UrlItem[] = (data || []).map(u => ({
          shortCode: u.shortCode || '',
          originalUrl: u.originalUrl || '',
          clicks: u.accessCount || 0,
          createdAt: u.createdAt ? new Date(u.createdAt) : new Date(),
          status: this.mapStatus(u.status)
        }));
        this.urls.set(urls);
        this.totalItems.set(urls.length);
        this.totalPages.set(Math.ceil(urls.length / this.pageSize) || 1);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.urls.set([]);
      }
    });
  }

  private mapStatus(status: any): string {
    switch (status) {
      case 0: return 'Active';
      case 1: return 'Expired';
      case 2: return 'Disabled';
      case 3: return 'Deleted';
      default: return 'Active';
    }
  }

  onSearch() {
    // Debounce and filter locally for now
    this.loadUrls();
  }

  allSelected(): boolean {
    return this.urls().length > 0 && this.selectedUrls().length === this.urls().length;
  }

  isSelected(shortCode: string): boolean {
    return this.selectedUrls().includes(shortCode);
  }

  toggleSelectAll() {
    if (this.allSelected()) {
      this.selectedUrls.set([]);
    } else {
      this.selectedUrls.set(this.urls().map(u => u.shortCode));
    }
  }

  toggleSelect(shortCode: string) {
    const current = this.selectedUrls();
    if (current.includes(shortCode)) {
      this.selectedUrls.set(current.filter(c => c !== shortCode));
    } else {
      this.selectedUrls.set([...current, shortCode]);
    }
  }

  clearSelection() {
    this.selectedUrls.set([]);
  }

  async copyUrl(shortCode: string) {
    const url = `${window.location.origin}/r/${shortCode}`;
    await navigator.clipboard.writeText(url);
    // TODO: Show toast notification
  }

  exportSelected() {
    const selected = this.urls().filter(u => this.selectedUrls().includes(u.shortCode));
    const csv = [
      'Short Code,Original URL,Clicks,Status',
      ...selected.map(u => `${u.shortCode},${u.originalUrl},${u.clicks},${u.status}`)
    ].join('\n');

    const blob = new Blob([csv], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'urls-export.csv';
    a.click();
    URL.revokeObjectURL(url);
  }

  deleteSelected() {
    if (confirm(`Delete ${this.selectedUrls().length} URL(s)?`)) {
      // TODO: Implement bulk delete
      console.log('Deleting:', this.selectedUrls());
      this.clearSelection();
      this.loadUrls();
    }
  }

  goToPage(page: number) {
    this.currentPage.set(page);
    this.loadUrls();
  }

  visiblePages(): number[] {
    const total = this.totalPages();
    const current = this.currentPage();
    const pages: number[] = [];

    const start = Math.max(1, current - 2);
    const end = Math.min(total, current + 2);

    for (let i = start; i <= end; i++) {
      pages.push(i);
    }

    return pages;
  }
}
