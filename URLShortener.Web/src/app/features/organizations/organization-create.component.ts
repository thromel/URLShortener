import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { OrganizationService } from '../../core/services/organization.service';

@Component({
  selector: 'app-organization-create',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <div class="container">
      <div class="form-card">
        <div class="header">
          <a routerLink="/organizations" class="back-link">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <line x1="19" y1="12" x2="5" y2="12"></line>
              <polyline points="12 19 5 12 12 5"></polyline>
            </svg>
            Back
          </a>
          <h1>Create Organization</h1>
          <p class="subtitle">Set up a new workspace for your team</p>
        </div>

        <form [formGroup]="form" (ngSubmit)="onSubmit()" class="form">
          @if (error()) {
            <div class="error-alert">
              {{ error() }}
            </div>
          }

          <div class="form-group">
            <label for="name">Organization Name</label>
            <input
              type="text"
              id="name"
              formControlName="name"
              placeholder="Acme Inc."
              (input)="onNameChange()"
              [class.invalid]="form.get('name')?.touched && form.get('name')?.invalid"
            />
            @if (form.get('name')?.touched && form.get('name')?.errors?.['required']) {
              <span class="field-error">Name is required</span>
            }
            @if (form.get('name')?.touched && form.get('name')?.errors?.['maxlength']) {
              <span class="field-error">Maximum 100 characters</span>
            }
          </div>

          <div class="form-group">
            <label for="slug">
              URL Slug
              <span class="label-hint">Used in URLs: /org/{{ form.get('slug')?.value || 'your-slug' }}</span>
            </label>
            <input
              type="text"
              id="slug"
              formControlName="slug"
              placeholder="acme-inc"
              [class.invalid]="form.get('slug')?.touched && form.get('slug')?.invalid"
            />
            @if (form.get('slug')?.touched && form.get('slug')?.errors?.['required']) {
              <span class="field-error">Slug is required</span>
            }
            @if (form.get('slug')?.touched && form.get('slug')?.errors?.['pattern']) {
              <span class="field-error">Slug must be lowercase letters, numbers, and hyphens only</span>
            }
            @if (form.get('slug')?.touched && form.get('slug')?.errors?.['maxlength']) {
              <span class="field-error">Maximum 50 characters</span>
            }
          </div>

          <div class="info-box">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="10"></circle>
              <line x1="12" y1="16" x2="12" y2="12"></line>
              <line x1="12" y1="8" x2="12.01" y2="8"></line>
            </svg>
            <div>
              <strong>What happens next?</strong>
              <p>You'll be the owner of this organization with full access. You can then invite team members and assign them roles.</p>
            </div>
          </div>

          <div class="actions">
            <a routerLink="/organizations" class="btn-secondary">Cancel</a>
            <button type="submit" class="btn-primary" [disabled]="form.invalid || isLoading()">
              @if (isLoading()) {
                <span class="spinner"></span>
                Creating...
              } @else {
                Create Organization
              }
            </button>
          </div>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .container {
      max-width: 600px;
      margin: 0 auto;
      padding: 2rem;
    }

    .form-card {
      background: white;
      border-radius: 12px;
      box-shadow: 0 4px 20px rgba(0, 0, 0, 0.08);
      padding: 2rem;
    }

    .header {
      margin-bottom: 2rem;
    }

    .back-link {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      color: #6b7280;
      text-decoration: none;
      font-size: 0.875rem;
      margin-bottom: 1rem;
    }

    .back-link:hover {
      color: #374151;
    }

    .header h1 {
      margin: 0;
      font-size: 1.5rem;
      color: #1a202c;
    }

    .subtitle {
      margin: 0.5rem 0 0;
      color: #6b7280;
    }

    .form {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }

    .form-group {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .form-group label {
      font-size: 0.875rem;
      font-weight: 500;
      color: #374151;
      display: flex;
      justify-content: space-between;
      align-items: baseline;
    }

    .label-hint {
      font-weight: 400;
      font-size: 0.75rem;
      color: #9ca3af;
    }

    .form-group input {
      padding: 0.75rem 1rem;
      border: 1px solid #d1d5db;
      border-radius: 8px;
      font-size: 1rem;
      transition: border-color 0.2s, box-shadow 0.2s;
    }

    .form-group input:focus {
      outline: none;
      border-color: #667eea;
      box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
    }

    .form-group input.invalid {
      border-color: #ef4444;
    }

    .field-error {
      font-size: 0.75rem;
      color: #ef4444;
    }

    .error-alert {
      background: #fef2f2;
      border: 1px solid #fecaca;
      color: #dc2626;
      padding: 0.75rem 1rem;
      border-radius: 8px;
      font-size: 0.875rem;
    }

    .info-box {
      display: flex;
      gap: 1rem;
      background: #f0f9ff;
      border: 1px solid #bae6fd;
      border-radius: 8px;
      padding: 1rem;
    }

    .info-box svg {
      color: #0284c7;
      flex-shrink: 0;
    }

    .info-box strong {
      display: block;
      color: #0c4a6e;
      font-size: 0.875rem;
    }

    .info-box p {
      margin: 0.25rem 0 0;
      font-size: 0.875rem;
      color: #0369a1;
    }

    .actions {
      display: flex;
      justify-content: flex-end;
      gap: 1rem;
      padding-top: 1rem;
      border-top: 1px solid #e5e7eb;
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
      cursor: pointer;
      transition: opacity 0.2s;
    }

    .btn-primary:hover:not(:disabled) {
      opacity: 0.9;
    }

    .btn-primary:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .btn-secondary {
      background: white;
      color: #374151;
      border: 1px solid #d1d5db;
      padding: 0.75rem 1.25rem;
      border-radius: 8px;
      font-size: 0.875rem;
      font-weight: 500;
      text-decoration: none;
      cursor: pointer;
    }

    .btn-secondary:hover {
      background: #f9fafb;
    }

    .spinner {
      width: 16px;
      height: 16px;
      border: 2px solid rgba(255, 255, 255, 0.3);
      border-top-color: white;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }
  `]
})
export class OrganizationCreateComponent {
  form: FormGroup;
  isLoading = signal(false);
  error = signal<string | null>(null);

  constructor(
    private fb: FormBuilder,
    private orgService: OrganizationService,
    private router: Router
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      slug: ['', [Validators.required, Validators.maxLength(50), Validators.pattern(/^[a-z0-9-]+$/)]]
    });
  }

  onNameChange(): void {
    const name = this.form.get('name')?.value || '';
    const currentSlug = this.form.get('slug')?.value || '';

    // Auto-generate slug if empty or was auto-generated before
    if (!currentSlug || currentSlug === this.orgService.generateSlug(name.slice(0, -1))) {
      this.form.patchValue({
        slug: this.orgService.generateSlug(name)
      });
    }
  }

  onSubmit(): void {
    if (this.form.invalid) return;

    this.isLoading.set(true);
    this.error.set(null);

    const { name, slug } = this.form.value;

    this.orgService.createOrganization({ name, slug }).subscribe({
      next: (org) => {
        this.router.navigate(['/organizations', org.slug]);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.error.set(err.error?.error || 'Failed to create organization');
      }
    });
  }
}
