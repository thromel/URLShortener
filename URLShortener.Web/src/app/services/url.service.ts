import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { UrlRequest, UrlResponse } from '../models/url.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class UrlService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) { }

  /**
   * Create a short URL
   * @param request URL shortening request
   * @returns Observable with shortened URL details
   */
  createShortUrl(request: UrlRequest): Observable<UrlResponse> {
    return this.http.post<UrlResponse>(`${this.apiUrl}/api/shorten`, request);
  }

  /**
   * Get original URL information by short code
   * @param shortCode The short code to look up
   * @returns Observable with URL information
   */
  getUrlInfo(shortCode: string): Observable<UrlResponse> {
    return this.http.get<UrlResponse>(`${this.apiUrl}/api/${shortCode}`);
  }
}
