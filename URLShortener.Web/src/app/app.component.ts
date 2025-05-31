import { Component } from '@angular/core';

@Component({
  selector: 'app-root',
  template: `
    <nav class="navbar navbar-expand-lg navbar-dark bg-primary">
      <div class="container">
        <a class="navbar-brand" href="#">
          <i class="bi bi-link-45deg"></i> URL Shortener
        </a>
      </div>
    </nav>
    
    <router-outlet></router-outlet>
    
    <footer class="footer mt-5 py-3 bg-light">
      <div class="container text-center">
        <span class="text-muted">URL Shortener &copy; 2025 | Built with .NET 8 and Angular</span>
      </div>
    </footer>
  `,
  styles: [`
    .footer {
      position: absolute;
      bottom: 0;
      width: 100%;
    }
  `]
})
export class AppComponent {
  title = 'URL Shortener';
}
