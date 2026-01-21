import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiClient, BulkCreateRequest, CreateUrlDto } from '../../core/api/api-client.generated';

interface BulkUrlEntry {
  id: number;
  originalUrl: string;
  customAlias: string;
  status: 'pending' | 'processing' | 'success' | 'error';
  result?: string;
  error?: string;
}

@Component({
  selector: 'app-bulk-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="space-y-6">
      <!-- Header -->
      <div class="flex items-center justify-between">
        <div>
          <a routerLink="/urls" class="text-gray-500 hover:text-gray-700 text-sm flex items-center gap-1 mb-2">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
            </svg>
            Back to URLs
          </a>
          <h1 class="text-2xl font-bold text-gray-900">Bulk Create URLs</h1>
          <p class="text-gray-500 mt-1">Create multiple short URLs at once</p>
        </div>
      </div>

      <!-- Input Methods -->
      <div class="card">
        <div class="card-body">
          <div class="flex border-b border-gray-200 mb-4">
            <button
              class="px-4 py-2 font-medium text-sm border-b-2 transition-colors"
              [class.border-primary-500]="inputMode() === 'manual'"
              [class.text-primary-600]="inputMode() === 'manual'"
              [class.border-transparent]="inputMode() !== 'manual'"
              [class.text-gray-500]="inputMode() !== 'manual'"
              (click)="inputMode.set('manual')">
              Manual Entry
            </button>
            <button
              class="px-4 py-2 font-medium text-sm border-b-2 transition-colors"
              [class.border-primary-500]="inputMode() === 'paste'"
              [class.text-primary-600]="inputMode() === 'paste'"
              [class.border-transparent]="inputMode() !== 'paste'"
              [class.text-gray-500]="inputMode() !== 'paste'"
              (click)="inputMode.set('paste')">
              Paste URLs
            </button>
            <button
              class="px-4 py-2 font-medium text-sm border-b-2 transition-colors"
              [class.border-primary-500]="inputMode() === 'csv'"
              [class.text-primary-600]="inputMode() === 'csv'"
              [class.border-transparent]="inputMode() !== 'csv'"
              [class.text-gray-500]="inputMode() !== 'csv'"
              (click)="inputMode.set('csv')">
              CSV Upload
            </button>
          </div>

          <!-- Manual Entry Mode -->
          <ng-container *ngIf="inputMode() === 'manual'">
            <div class="space-y-4">
              <div *ngFor="let entry of entries(); let i = index" class="flex items-start gap-4 p-4 bg-gray-50 rounded-lg">
                <div class="flex-1 grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label class="label">Original URL</label>
                    <input
                      type="url"
                      class="input"
                      placeholder="https://example.com/long-url"
                      [(ngModel)]="entry.originalUrl">
                  </div>
                  <div>
                    <label class="label">Custom Alias (optional)</label>
                    <input
                      type="text"
                      class="input"
                      placeholder="my-custom-alias"
                      [(ngModel)]="entry.customAlias">
                  </div>
                </div>
                <button
                  class="btn btn-ghost p-2 text-gray-400 hover:text-danger-500"
                  (click)="removeEntry(i)"
                  [disabled]="entries().length === 1">
                  <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                  </svg>
                </button>
              </div>

              <button class="btn btn-secondary w-full" (click)="addEntry()">
                <svg class="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
                </svg>
                Add Another URL
              </button>
            </div>
          </ng-container>

          <!-- Paste Mode -->
          <ng-container *ngIf="inputMode() === 'paste'">
            <div class="space-y-4">
              <div>
                <label class="label">Paste URLs (one per line)</label>
                <textarea
                  class="input min-h-[200px] font-mono text-sm"
                  placeholder="https://example.com/page1&#10;https://example.com/page2&#10;https://example.com/page3"
                  [(ngModel)]="pasteContent"
                  (ngModelChange)="parseUrls()">
                </textarea>
              </div>
              <p *ngIf="entries().length > 0" class="text-sm text-gray-500">
                <span class="font-medium text-gray-700">{{ entries().length }}</span> URLs detected
              </p>
            </div>
          </ng-container>

          <!-- CSV Upload Mode -->
          <ng-container *ngIf="inputMode() === 'csv'">
            <div class="space-y-4">
              <div
                class="border-2 border-dashed border-gray-300 rounded-lg p-8 text-center hover:border-primary-400 transition-colors"
                (dragover)="$event.preventDefault()"
                (drop)="handleFileDrop($event)">
                <svg class="w-12 h-12 text-gray-400 mx-auto mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
                </svg>
                <p class="text-gray-600 mb-2">Drop your CSV file here, or</p>
                <label class="btn btn-secondary cursor-pointer">
                  Browse Files
                  <input type="file" class="hidden" accept=".csv" (change)="handleFileSelect($event)">
                </label>
                <p class="text-xs text-gray-400 mt-4">
                  CSV format: original_url, custom_alias (optional)
                </p>
              </div>

              <div *ngIf="entries().length > 0" class="bg-green-50 border border-green-200 rounded-lg p-4">
                <p class="text-green-700 font-medium">
                  <svg class="w-5 h-5 inline mr-2" fill="currentColor" viewBox="0 0 20 20">
                    <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
                  </svg>
                  {{ entries().length }} URLs loaded from CSV
                </p>
              </div>
            </div>
          </ng-container>
        </div>
      </div>

      <!-- Preview Table -->
      <div *ngIf="showPreview()" class="card">
        <div class="card-header flex items-center justify-between">
          <span>Preview ({{ validEntries().length }} valid URLs)</span>
          <button class="btn btn-ghost btn-sm text-danger-500" (click)="clearAll()">
            Clear All
          </button>
        </div>
        <div class="overflow-x-auto">
          <table class="table">
            <thead>
              <tr>
                <th class="w-10">#</th>
                <th>Original URL</th>
                <th>Custom Alias</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              <ng-container *ngFor="let entry of entries(); let i = index">
                <tr *ngIf="entry.originalUrl">
                  <td class="text-gray-500">{{ i + 1 }}</td>
                  <td>
                    <div class="max-w-xs truncate" [title]="entry.originalUrl">
                      {{ entry.originalUrl }}
                    </div>
                  </td>
                  <td>
                    <span *ngIf="entry.customAlias" class="font-mono text-sm">{{ entry.customAlias }}</span>
                    <span *ngIf="!entry.customAlias" class="text-gray-400">Auto-generated</span>
                  </td>
                  <td>
                    <span *ngIf="entry.status === 'pending'" class="badge badge-secondary">Pending</span>
                    <span *ngIf="entry.status === 'processing'" class="badge badge-warning">Processing...</span>
                    <span *ngIf="entry.status === 'success'" class="badge badge-success">Created</span>
                    <span *ngIf="entry.status === 'error'" class="badge badge-danger" [title]="entry.error || ''">Failed</span>
                  </td>
                </tr>
              </ng-container>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Actions -->
      <div class="flex items-center justify-end gap-3">
        <a routerLink="/urls" class="btn btn-secondary">Cancel</a>
        <button
          class="btn btn-primary"
          [disabled]="validEntries().length === 0 || processing()"
          (click)="createUrls()">
          <ng-container *ngIf="processing()">
            <svg class="animate-spin -ml-1 mr-2 h-4 w-4 text-white" fill="none" viewBox="0 0 24 24">
              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
            </svg>
            Creating {{ validEntries().length }} URLs...
          </ng-container>
          <ng-container *ngIf="!processing()">
            <svg class="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
            </svg>
            Create {{ validEntries().length }} URLs
          </ng-container>
        </button>
      </div>

      <!-- Results Summary -->
      <div *ngIf="completed()" class="card border-2"
           [class.border-success-300]="failedCount() === 0"
           [class.border-warning-300]="failedCount() > 0 && successCount() > 0"
           [class.border-danger-300]="successCount() === 0">
        <div class="card-body">
          <div class="flex items-start gap-4">
            <div *ngIf="failedCount() === 0" class="w-12 h-12 bg-green-100 rounded-full flex items-center justify-center flex-shrink-0">
              <svg class="w-6 h-6 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
              </svg>
            </div>
            <div *ngIf="failedCount() > 0" class="w-12 h-12 bg-yellow-100 rounded-full flex items-center justify-center flex-shrink-0">
              <svg class="w-6 h-6 text-yellow-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
              </svg>
            </div>
            <div class="flex-1">
              <h3 class="text-lg font-semibold text-gray-900">
                <ng-container *ngIf="failedCount() === 0">All URLs created successfully!</ng-container>
                <ng-container *ngIf="failedCount() > 0 && successCount() > 0">Partially completed</ng-container>
                <ng-container *ngIf="successCount() === 0 && failedCount() > 0">Failed to create URLs</ng-container>
              </h3>
              <p class="text-gray-500 mt-1">
                {{ successCount() }} created, {{ failedCount() }} failed
              </p>
              <div class="mt-4 flex gap-3">
                <a routerLink="/urls" class="btn btn-primary btn-sm">View All URLs</a>
                <button *ngIf="failedCount() > 0" class="btn btn-secondary btn-sm" (click)="retryFailed()">
                  Retry Failed ({{ failedCount() }})
                </button>
                <button class="btn btn-ghost btn-sm" (click)="startOver()">
                  Create More
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class BulkCreateComponent {
  private api = inject(ApiClient);

  inputMode = signal<'manual' | 'paste' | 'csv'>('manual');
  entries = signal<BulkUrlEntry[]>([{ id: 1, originalUrl: '', customAlias: '', status: 'pending' }]);
  pasteContent = '';
  processing = signal(false);
  completed = signal(false);

  private nextId = 2;

  showPreview(): boolean {
    const currentEntries = this.entries();
    if (currentEntries.length === 0) return false;
    if (this.inputMode() !== 'manual') return true;
    return currentEntries.some(e => !!e.originalUrl);
  }

  validEntries(): BulkUrlEntry[] {
    return this.entries().filter(e => e.originalUrl && this.isValidUrl(e.originalUrl));
  }

  successCount(): number {
    return this.entries().filter(e => e.status === 'success').length;
  }

  failedCount(): number {
    return this.entries().filter(e => e.status === 'error').length;
  }

  addEntry() {
    this.entries.update(entries => [
      ...entries,
      { id: this.nextId++, originalUrl: '', customAlias: '', status: 'pending' }
    ]);
  }

  removeEntry(index: number) {
    this.entries.update(entries => entries.filter((_, i) => i !== index));
  }

  parseUrls() {
    const lines = this.pasteContent
      .split('\n')
      .map(line => line.trim())
      .filter(line => line && this.isValidUrl(line));

    this.entries.set(lines.map((url, i) => ({
      id: i + 1,
      originalUrl: url,
      customAlias: '',
      status: 'pending' as const
    })));
    this.nextId = lines.length + 1;
  }

  handleFileDrop(event: DragEvent) {
    event.preventDefault();
    const file = event.dataTransfer?.files[0];
    if (file && file.name.endsWith('.csv')) {
      this.parseCSV(file);
    }
  }

  handleFileSelect(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) {
      this.parseCSV(file);
    }
  }

  private parseCSV(file: File) {
    const reader = new FileReader();
    reader.onload = (e) => {
      const content = e.target?.result as string;
      const lines = content.split('\n').slice(1); // Skip header

      const entries: BulkUrlEntry[] = [];
      lines.forEach((line, i) => {
        const [originalUrl, customAlias] = line.split(',').map(s => s.trim().replace(/"/g, ''));
        if (originalUrl && this.isValidUrl(originalUrl)) {
          entries.push({
            id: i + 1,
            originalUrl,
            customAlias: customAlias || '',
            status: 'pending'
          });
        }
      });

      this.entries.set(entries);
      this.nextId = entries.length + 1;
    };
    reader.readAsText(file);
  }

  clearAll() {
    this.entries.set([{ id: 1, originalUrl: '', customAlias: '', status: 'pending' }]);
    this.pasteContent = '';
    this.nextId = 2;
    this.completed.set(false);
  }

  createUrls() {
    if (this.validEntries().length === 0) return;

    this.processing.set(true);
    this.completed.set(false);

    // Mark all as processing
    this.entries.update(entries =>
      entries.map(e => e.originalUrl ? { ...e, status: 'processing' as const } : e)
    );

    // Build the bulk request with valid entries
    const validEntries = this.validEntries();
    const urls = validEntries.map(entry => new CreateUrlDto({
      originalUrl: entry.originalUrl,
      customAlias: entry.customAlias || undefined
    }));

    const request = new BulkCreateRequest({ urls });

    this.api.createBulkUrls('1', request).subscribe({
      next: (response) => {
        // Update entries based on results
        if (response.results) {
          response.results.forEach((result, index) => {
            const entryId = validEntries[index]?.id;
            if (entryId !== undefined) {
              this.entries.update(entries =>
                entries.map(e =>
                  e.id === entryId
                    ? {
                        ...e,
                        status: result.success ? 'success' as const : 'error' as const,
                        result: result.shortCode || undefined,
                        error: result.error || undefined
                      }
                    : e
                )
              );
            }
          });
        }
        this.processing.set(false);
        this.completed.set(true);
      },
      error: (err) => {
        console.error('Bulk create failed:', err);
        // Mark all as error
        this.entries.update(entries =>
          entries.map(e =>
            e.status === 'processing'
              ? { ...e, status: 'error' as const, error: 'API request failed' }
              : e
          )
        );
        this.processing.set(false);
        this.completed.set(true);
      }
    });
  }

  retryFailed() {
    this.entries.update(entries =>
      entries.map(e =>
        e.status === 'error' ? { ...e, status: 'pending' as const, error: undefined } : e
      )
    );
    this.completed.set(false);
    this.createUrls();
  }

  startOver() {
    this.clearAll();
    this.inputMode.set('manual');
  }

  private isValidUrl(url: string): boolean {
    try {
      const parsed = new URL(url);
      return parsed.protocol === 'http:' || parsed.protocol === 'https:';
    } catch {
      return false;
    }
  }
}
