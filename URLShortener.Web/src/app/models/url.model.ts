export interface UrlRequest {
  url: string;
  customAlias?: string;
}

export interface UrlResponse {
  shortUrl: string;
  originalUrl: string;
  shortCode: string;
  createdAt: string;
  expiresAt?: string;
  clickCount: number;
}
