import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { UrlService } from '../../services/url.service';
import { UrlRequest, UrlResponse } from '../../models/url.model';

@Component({
  selector: 'app-url-shortener',
  templateUrl: './url-shortener.component.html',
  styleUrls: ['./url-shortener.component.scss']
})
export class UrlShortenerComponent implements OnInit {
  urlForm: FormGroup;
  resultUrl: UrlResponse | null = null;
  isLoading = false;
  error: string | null = null;
  copied = false;

  constructor(
    private fb: FormBuilder,
    private urlService: UrlService
  ) {
    this.urlForm = this.fb.group({
      url: ['', [Validators.required, Validators.pattern('https?://.+')]],
      customAlias: ['']
    });
  }

  ngOnInit(): void {
  }

  onSubmit(): void {
    if (this.urlForm.invalid) {
      return;
    }

    this.isLoading = true;
    this.error = null;
    this.resultUrl = null;
    this.copied = false;

    const request: UrlRequest = {
      url: this.urlForm.value.url,
      customAlias: this.urlForm.value.customAlias || undefined
    };

    this.urlService.createShortUrl(request).subscribe({
      next: (response) => {
        this.resultUrl = response;
        this.isLoading = false;
      },
      error: (err) => {
        this.error = err.error || 'An error occurred while creating the short URL';
        this.isLoading = false;
      }
    });
  }

  copyToClipboard(): void {
    if (!this.resultUrl) return;
    
    navigator.clipboard.writeText(this.resultUrl.shortUrl).then(
      () => {
        this.copied = true;
        setTimeout(() => this.copied = false, 2000);
      },
      () => {
        this.error = 'Failed to copy to clipboard';
      }
    );
  }

  reset(): void {
    this.urlForm.reset();
    this.resultUrl = null;
    this.error = null;
    this.copied = false;
  }
}
