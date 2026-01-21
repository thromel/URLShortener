import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <div class="auth-container">
      <div class="auth-card">
        <div class="auth-header">
          <h1>Create account</h1>
          <p>Get started with URL Shortener</p>
        </div>

        <form [formGroup]="registerForm" (ngSubmit)="onSubmit()" class="auth-form">
          @if (error()) {
            <div class="error-alert">
              {{ error() }}
            </div>
          }

          <div class="form-group">
            <label for="displayName">Display Name</label>
            <input
              type="text"
              id="displayName"
              formControlName="displayName"
              placeholder="John Doe"
              [class.invalid]="registerForm.get('displayName')?.touched && registerForm.get('displayName')?.invalid"
            />
            @if (registerForm.get('displayName')?.touched && registerForm.get('displayName')?.errors?.['required']) {
              <span class="field-error">Display name is required</span>
            }
            @if (registerForm.get('displayName')?.touched && registerForm.get('displayName')?.errors?.['maxlength']) {
              <span class="field-error">Maximum 100 characters</span>
            }
          </div>

          <div class="form-group">
            <label for="email">Email</label>
            <input
              type="email"
              id="email"
              formControlName="email"
              placeholder="you@example.com"
              [class.invalid]="registerForm.get('email')?.touched && registerForm.get('email')?.invalid"
            />
            @if (registerForm.get('email')?.touched && registerForm.get('email')?.errors?.['required']) {
              <span class="field-error">Email is required</span>
            }
            @if (registerForm.get('email')?.touched && registerForm.get('email')?.errors?.['email']) {
              <span class="field-error">Please enter a valid email</span>
            }
          </div>

          <div class="form-group">
            <label for="password">Password</label>
            <input
              type="password"
              id="password"
              formControlName="password"
              placeholder="Min 8 characters"
              [class.invalid]="registerForm.get('password')?.touched && registerForm.get('password')?.invalid"
            />
            @if (registerForm.get('password')?.touched && registerForm.get('password')?.errors?.['required']) {
              <span class="field-error">Password is required</span>
            }
            @if (registerForm.get('password')?.touched && registerForm.get('password')?.errors?.['minlength']) {
              <span class="field-error">Password must be at least 8 characters</span>
            }

            @if (registerForm.get('password')?.value) {
              <div class="password-strength">
                <div class="strength-bar" [class]="getPasswordStrength()"></div>
                <span class="strength-text">{{ getPasswordStrengthText() }}</span>
              </div>
            }
          </div>

          <div class="form-group">
            <label for="confirmPassword">Confirm Password</label>
            <input
              type="password"
              id="confirmPassword"
              formControlName="confirmPassword"
              placeholder="Repeat your password"
              [class.invalid]="registerForm.get('confirmPassword')?.touched && registerForm.get('confirmPassword')?.invalid"
            />
            @if (registerForm.get('confirmPassword')?.touched && registerForm.get('confirmPassword')?.errors?.['required']) {
              <span class="field-error">Please confirm your password</span>
            }
            @if (registerForm.get('confirmPassword')?.touched && registerForm.get('confirmPassword')?.errors?.['passwordMismatch']) {
              <span class="field-error">Passwords do not match</span>
            }
          </div>

          <button type="submit" class="btn-primary" [disabled]="registerForm.invalid || isLoading()">
            @if (isLoading()) {
              <span class="spinner"></span>
              Creating account...
            } @else {
              Create account
            }
          </button>
        </form>

        <div class="auth-footer">
          <p>Already have an account? <a routerLink="/login">Sign in</a></p>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .auth-container {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      padding: 1rem;
    }

    .auth-card {
      background: white;
      border-radius: 12px;
      box-shadow: 0 10px 40px rgba(0, 0, 0, 0.2);
      padding: 2.5rem;
      width: 100%;
      max-width: 420px;
    }

    .auth-header {
      text-align: center;
      margin-bottom: 2rem;
    }

    .auth-header h1 {
      margin: 0;
      font-size: 1.75rem;
      color: #1a202c;
    }

    .auth-header p {
      margin: 0.5rem 0 0;
      color: #718096;
    }

    .auth-form {
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
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

    .password-strength {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin-top: 0.25rem;
    }

    .strength-bar {
      flex: 1;
      height: 4px;
      background: #e5e7eb;
      border-radius: 2px;
      position: relative;
      overflow: hidden;
    }

    .strength-bar::after {
      content: '';
      position: absolute;
      left: 0;
      top: 0;
      height: 100%;
      border-radius: 2px;
      transition: width 0.3s, background 0.3s;
    }

    .strength-bar.weak::after {
      width: 33%;
      background: #ef4444;
    }

    .strength-bar.medium::after {
      width: 66%;
      background: #f59e0b;
    }

    .strength-bar.strong::after {
      width: 100%;
      background: #22c55e;
    }

    .strength-text {
      font-size: 0.75rem;
      color: #6b7280;
      min-width: 50px;
    }

    .btn-primary {
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
      border: none;
      padding: 0.875rem 1.5rem;
      border-radius: 8px;
      font-size: 1rem;
      font-weight: 500;
      cursor: pointer;
      transition: opacity 0.2s, transform 0.2s;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
    }

    .btn-primary:hover:not(:disabled) {
      opacity: 0.9;
      transform: translateY(-1px);
    }

    .btn-primary:disabled {
      opacity: 0.6;
      cursor: not-allowed;
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

    .auth-footer {
      text-align: center;
      margin-top: 1.5rem;
      padding-top: 1.5rem;
      border-top: 1px solid #e5e7eb;
    }

    .auth-footer p {
      margin: 0;
      color: #6b7280;
      font-size: 0.875rem;
    }

    .auth-footer a {
      color: #667eea;
      text-decoration: none;
      font-weight: 500;
    }

    .auth-footer a:hover {
      text-decoration: underline;
    }
  `]
})
export class RegisterComponent {
  registerForm: FormGroup;
  isLoading = signal(false);
  error = signal<string | null>(null);

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.registerForm = this.fb.group({
      displayName: ['', [Validators.required, Validators.maxLength(100)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', Validators.required]
    }, {
      validators: this.passwordMatchValidator
    });
  }

  passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
    const password = control.get('password');
    const confirmPassword = control.get('confirmPassword');

    if (password && confirmPassword && password.value !== confirmPassword.value) {
      confirmPassword.setErrors({ passwordMismatch: true });
      return { passwordMismatch: true };
    }

    return null;
  }

  getPasswordStrength(): string {
    const password = this.registerForm.get('password')?.value || '';

    if (password.length < 8) return 'weak';

    let strength = 0;
    if (password.length >= 8) strength++;
    if (password.length >= 12) strength++;
    if (/[A-Z]/.test(password)) strength++;
    if (/[a-z]/.test(password)) strength++;
    if (/[0-9]/.test(password)) strength++;
    if (/[^A-Za-z0-9]/.test(password)) strength++;

    if (strength <= 2) return 'weak';
    if (strength <= 4) return 'medium';
    return 'strong';
  }

  getPasswordStrengthText(): string {
    const strength = this.getPasswordStrength();
    switch (strength) {
      case 'weak': return 'Weak';
      case 'medium': return 'Medium';
      case 'strong': return 'Strong';
      default: return '';
    }
  }

  onSubmit(): void {
    if (this.registerForm.invalid) return;

    this.isLoading.set(true);
    this.error.set(null);

    const { displayName, email, password } = this.registerForm.value;

    this.authService.register({ displayName, email, password }).subscribe({
      next: () => {
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.error.set(err.error?.error || 'Failed to create account');
      }
    });
  }
}
