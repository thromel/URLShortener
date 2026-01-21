import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiClient, CreateUrlDto } from '../../core/api/api-client.generated';

@Component({
  selector: 'app-url-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="max-w-2xl mx-auto space-y-6">
      <!-- Header -->
      <div>
        <a routerLink="/urls" class="text-gray-500 hover:text-gray-700 text-sm flex items-center gap-1 mb-2">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
          </svg>
          Back to URLs
        </a>
        <h1 class="text-2xl font-bold text-gray-900">Create Short URL</h1>
        <p class="text-gray-500 mt-1">Shorten a long URL and track its performance</p>
      </div>

      <!-- Form -->
      <div class="card">
        <div class="card-body space-y-6">
          <!-- Original URL -->
          <div>
            <label class="label">Original URL <span class="text-danger-500">*</span></label>
            <input type="url"
                   class="input"
                   [class.input-error]="urlError"
                   placeholder="https://example.com/your-long-url"
                   [(ngModel)]="originalUrl"
                   (blur)="validateUrl()">
            @if (urlError) {
              <p class="text-danger-500 text-sm mt-1">{{ urlError }}</p>
            }
          </div>

          <!-- Custom Alias -->
          <div>
            <label class="label">
              Custom Alias
              <span class="text-gray-400 font-normal">(optional)</span>
            </label>
            <div class="flex items-center gap-2">
              <span class="text-gray-500">{{ baseUrl }}/</span>
              <input type="text"
                     class="input flex-1"
                     [class.input-error]="aliasError"
                     placeholder="my-custom-alias"
                     [(ngModel)]="customAlias"
                     (blur)="checkAvailability()">
            </div>
            @if (checkingAlias()) {
              <p class="text-gray-400 text-sm mt-1">Checking availability...</p>
            } @else if (aliasAvailable() === true) {
              <p class="text-success-600 text-sm mt-1 flex items-center gap-1">
                <svg class="w-4 h-4" fill="currentColor" viewBox="0 0 20 20">
                  <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
                </svg>
                Available!
              </p>
            } @else if (aliasAvailable() === false) {
              <p class="text-danger-500 text-sm mt-1">{{ aliasError }}</p>
            }
          </div>

          <!-- Expiration -->
          <div>
            <label class="label">
              Expiration Date
              <span class="text-gray-400 font-normal">(optional)</span>
            </label>
            <input type="datetime-local"
                   class="input"
                   [(ngModel)]="expiresAt"
                   [min]="minDate">
            <p class="text-gray-400 text-sm mt-1">Leave empty for no expiration</p>
          </div>

          <!-- Preview -->
          @if (originalUrl && !urlError) {
            <div class="bg-gray-50 rounded-lg p-4 border border-gray-200">
              <p class="text-sm text-gray-500 mb-2">Preview</p>
              <div class="flex items-center gap-2">
                <span class="text-primary-600 font-medium">
                  {{ baseUrl }}/{{ customAlias || 'abc123' }}
                </span>
                <svg class="w-4 h-4 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 7l5 5m0 0l-5 5m5-5H6" />
                </svg>
                <span class="text-gray-600 truncate max-w-xs">{{ originalUrl }}</span>
              </div>
            </div>
          }

          <!-- Actions -->
          <div class="flex items-center justify-end gap-3 pt-4 border-t border-gray-200">
            <a routerLink="/urls" class="btn btn-secondary">Cancel</a>
            <button class="btn btn-primary"
                    [disabled]="!canSubmit() || submitting()"
                    (click)="submit()">
              @if (submitting()) {
                <svg class="animate-spin -ml-1 mr-2 h-4 w-4 text-white" fill="none" viewBox="0 0 24 24">
                  <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                  <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                </svg>
                Creating...
              } @else {
                Create Short URL
              }
            </button>
          </div>
        </div>
      </div>

      <!-- Success Modal -->
      @if (createdUrl()) {
        <div class="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div class="bg-white rounded-xl shadow-xl max-w-md w-full mx-4 animate-fade-in">
            <div class="p-6 text-center">
              <div class="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
                <svg class="w-8 h-8 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                </svg>
              </div>
              <h2 class="text-xl font-bold text-gray-900 mb-2">URL Created!</h2>
              <p class="text-gray-500 mb-6">Your short URL is ready to use</p>

              <div class="bg-gray-50 rounded-lg p-4 mb-6">
                <p class="text-lg font-medium text-primary-600 break-all">{{ createdUrl() }}</p>
              </div>

              <div class="flex items-center justify-center gap-3">
                <button class="btn btn-secondary" (click)="copyCreatedUrl()">
                  @if (copied()) {
                    <svg class="w-5 h-5 mr-2 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                    </svg>
                    Copied!
                  } @else {
                    <svg class="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
                    </svg>
                    Copy URL
                  }
                </button>
                <a routerLink="/urls" class="btn btn-primary">View All URLs</a>
              </div>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class UrlCreateComponent {
  private api = inject(ApiClient);
  private router = inject(Router);

  originalUrl = '';
  customAlias = '';
  expiresAt = '';

  urlError = '';
  aliasError = '';

  checkingAlias = signal(false);
  aliasAvailable = signal<boolean | null>(null);
  submitting = signal(false);
  createdUrl = signal<string | null>(null);
  copied = signal(false);

  baseUrl = window.location.origin + '/r';
  minDate = new Date().toISOString().slice(0, 16);

  validateUrl() {
    this.urlError = '';
    if (!this.originalUrl) {
      return;
    }

    try {
      new URL(this.originalUrl);
      if (!this.originalUrl.startsWith('http://') && !this.originalUrl.startsWith('https://')) {
        this.urlError = 'URL must start with http:// or https://';
      }
    } catch {
      this.urlError = 'Please enter a valid URL';
    }
  }

  checkAvailability() {
    if (!this.customAlias) {
      this.aliasAvailable.set(null);
      this.aliasError = '';
      return;
    }

    // Validate alias format
    if (!/^[a-zA-Z0-9-_]+$/.test(this.customAlias)) {
      this.aliasAvailable.set(false);
      this.aliasError = 'Alias can only contain letters, numbers, hyphens, and underscores';
      return;
    }

    if (this.customAlias.length < 3) {
      this.aliasAvailable.set(false);
      this.aliasError = 'Alias must be at least 3 characters';
      return;
    }

    this.checkingAlias.set(true);
    this.aliasError = '';

    this.api.checkAvailability(this.customAlias, '1').subscribe({
      next: (response) => {
        this.checkingAlias.set(false);
        if (response.available) {
          this.aliasAvailable.set(true);
        } else {
          this.aliasAvailable.set(false);
          this.aliasError = 'This alias is already taken';
        }
      },
      error: () => {
        this.checkingAlias.set(false);
        this.aliasAvailable.set(false);
        this.aliasError = 'Failed to check availability';
      }
    });
  }

  canSubmit(): boolean {
    return !!this.originalUrl &&
           !this.urlError &&
           !this.aliasError &&
           (this.aliasAvailable() !== false || !this.customAlias);
  }

  submit() {
    if (!this.canSubmit()) return;

    this.submitting.set(true);

    const dto = new CreateUrlDto({
      originalUrl: this.originalUrl,
      customAlias: this.customAlias || undefined,
      expiresAt: this.expiresAt ? new Date(this.expiresAt) : undefined
    });

    this.api.createShortUrl('1', dto).subscribe({
      next: (response) => {
        this.createdUrl.set(response.shortUrl || `${this.baseUrl}/${response.shortCode}`);
        this.submitting.set(false);
      },
      error: (err) => {
        console.error('Failed to create URL:', err);
        this.urlError = err.error?.error || 'Failed to create short URL. Please try again.';
        this.submitting.set(false);
      }
    });
  }

  async copyCreatedUrl() {
    if (this.createdUrl()) {
      await navigator.clipboard.writeText(this.createdUrl()!);
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    }
  }
}
