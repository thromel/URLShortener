import { Component, OnInit, inject, signal, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData } from 'chart.js';
import { ApiClient, TimeSeriesDataDto, DeviceBreakdownDto, GeographicHeatmapDto } from '../../core/api/api-client.generated';

@Component({
  selector: 'app-url-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, BaseChartDirective],
  template: `
    <div class="space-y-6">
      <!-- Header -->
      <div class="flex items-start justify-between">
        <div>
          <a routerLink="/urls" class="text-gray-500 hover:text-gray-700 text-sm flex items-center gap-1 mb-2">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
            </svg>
            Back to URLs
          </a>
          <h1 class="text-2xl font-bold text-gray-900 flex items-center gap-3">
            {{ shortCode() }}
            <span class="badge badge-success">Active</span>
          </h1>
          <p class="text-gray-500 mt-1 truncate max-w-lg">{{ originalUrl() }}</p>
        </div>
        <div class="flex items-center gap-2">
          <button class="btn btn-secondary" (click)="copyUrl()">
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
          <a [href]="shortUrl()" target="_blank" class="btn btn-primary">
            <svg class="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
            </svg>
            Open URL
          </a>
        </div>
      </div>

      <!-- Quick Stats -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="stat-card">
          <p class="stat-label">Total Clicks</p>
          <p class="stat-value">{{ stats()?.totalClicks | number }}</p>
        </div>
        <div class="stat-card">
          <p class="stat-label">Unique Visitors</p>
          <p class="stat-value">{{ stats()?.uniqueVisitors | number }}</p>
        </div>
        <div class="stat-card">
          <p class="stat-label">Countries</p>
          <p class="stat-value">{{ geographic()?.countriesReached | number }}</p>
        </div>
        <div class="stat-card">
          <p class="stat-label">Avg. Daily Clicks</p>
          <p class="stat-value">{{ avgDailyClicks() | number:'1.0-0' }}</p>
        </div>
      </div>

      <!-- Charts -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Clicks Over Time -->
        <div class="card">
          <div class="card-header flex items-center justify-between">
            <span>Clicks Over Time</span>
            <select class="input w-32 text-sm" [(value)]="selectedInterval" (change)="loadTimeSeries()">
              <option value="hour">Hourly</option>
              <option value="day">Daily</option>
              <option value="week">Weekly</option>
            </select>
          </div>
          <div class="card-body">
            @if (lineChartData) {
              <canvas baseChart
                [data]="lineChartData"
                [options]="lineChartOptions"
                [type]="'line'"
                style="height: 250px;">
              </canvas>
            } @else {
              <div class="h-64 flex items-center justify-center text-gray-400">
                Loading chart...
              </div>
            }
          </div>
        </div>

        <!-- Device Breakdown -->
        <div class="card">
          <div class="card-header">Device Breakdown</div>
          <div class="card-body">
            @if (doughnutChartData) {
              <canvas baseChart
                [data]="doughnutChartData"
                [options]="doughnutChartOptions"
                [type]="'doughnut'"
                style="height: 250px;">
              </canvas>
            } @else {
              <div class="h-64 flex items-center justify-center text-gray-400">
                Loading chart...
              </div>
            }
          </div>
        </div>
      </div>

      <!-- Geographic & Browser Data -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Top Countries -->
        <div class="card">
          <div class="card-header">Top Countries</div>
          <div class="card-body p-0">
            @if (geographic()?.countries?.length) {
              <table class="table">
                <thead>
                  <tr>
                    <th>Country</th>
                    <th>Clicks</th>
                    <th>%</th>
                  </tr>
                </thead>
                <tbody>
                  @for (country of geographic()!.countries!.slice(0, 10); track country.countryCode) {
                    <tr>
                      <td class="font-medium">{{ country.countryName }}</td>
                      <td>{{ country.clicks | number }}</td>
                      <td>
                        <div class="flex items-center gap-2">
                          <div class="w-16 h-2 bg-gray-200 rounded-full overflow-hidden">
                            <div class="h-full bg-primary-500 rounded-full" [style.width.%]="country.percentage"></div>
                          </div>
                          <span class="text-xs text-gray-500">{{ country.percentage | number:'1.1-1' }}%</span>
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            } @else {
              <div class="p-6 text-center text-gray-400">No geographic data available</div>
            }
          </div>
        </div>

        <!-- Top Browsers -->
        <div class="card">
          <div class="card-header">Top Browsers</div>
          <div class="card-body p-0">
            @if (devices()?.browsers?.length) {
              <table class="table">
                <thead>
                  <tr>
                    <th>Browser</th>
                    <th>Users</th>
                    <th>%</th>
                  </tr>
                </thead>
                <tbody>
                  @for (browser of devices()!.browsers!.slice(0, 10); track browser.name) {
                    <tr>
                      <td class="font-medium">{{ browser.name }}</td>
                      <td>{{ browser.count | number }}</td>
                      <td>
                        <div class="flex items-center gap-2">
                          <div class="w-16 h-2 bg-gray-200 rounded-full overflow-hidden">
                            <div class="h-full bg-green-500 rounded-full" [style.width.%]="browser.percentage"></div>
                          </div>
                          <span class="text-xs text-gray-500">{{ browser.percentage | number:'1.1-1' }}%</span>
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            } @else {
              <div class="p-6 text-center text-gray-400">No browser data available</div>
            }
          </div>
        </div>
      </div>

      <!-- Danger Zone -->
      <div class="card border-danger-200">
        <div class="card-header text-danger-600">Danger Zone</div>
        <div class="card-body">
          <div class="flex items-center justify-between">
            <div>
              <p class="font-medium text-gray-900">Disable this URL</p>
              <p class="text-sm text-gray-500">The URL will stop redirecting but data will be preserved</p>
            </div>
            <button class="btn btn-secondary">Disable</button>
          </div>
          <hr class="my-4">
          <div class="flex items-center justify-between">
            <div>
              <p class="font-medium text-gray-900">Delete this URL</p>
              <p class="text-sm text-gray-500">Permanently delete this URL and all its analytics data</p>
            </div>
            <button class="btn btn-danger">Delete</button>
          </div>
        </div>
      </div>
    </div>
  `
})
export class UrlDetailComponent implements OnInit {
  shortCode = input.required<string>();

  private api = inject(ApiClient);

  originalUrl = signal('https://example.com/very-long-url-that-was-shortened');
  stats = signal<{ totalClicks: number; uniqueVisitors: number } | null>(null);
  timeSeries = signal<TimeSeriesDataDto | null>(null);
  devices = signal<DeviceBreakdownDto | null>(null);
  geographic = signal<GeographicHeatmapDto | null>(null);

  selectedInterval = 'day';
  copied = signal(false);

  lineChartData: ChartData<'line'> | null = null;
  lineChartOptions: ChartConfiguration<'line'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: {
      x: { grid: { display: false } },
      y: { beginAtZero: true, grid: { color: '#f3f4f6' } }
    }
  };

  doughnutChartData: ChartData<'doughnut'> | null = null;
  doughnutChartOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { position: 'bottom' } }
  };

  shortUrl(): string {
    return `${window.location.origin}/r/${this.shortCode()}`;
  }

  avgDailyClicks(): number {
    const points = this.timeSeries()?.points || [];
    if (points.length === 0) return 0;
    const total = points.reduce((sum, p) => sum + (p.clicks || 0), 0);
    return total / points.length;
  }

  ngOnInit() {
    this.loadTimeSeries();
    this.loadDevices();
    this.loadGeographic();
  }

  loadTimeSeries() {
    const endDate = new Date();
    const startDate = new Date();
    startDate.setDate(startDate.getDate() - 30);

    this.api.timeseries(this.shortCode(), startDate, endDate, this.selectedInterval).subscribe({
      next: (data) => {
        this.timeSeries.set(data);
        this.stats.set({
          totalClicks: data.aggregates?.totalClicks || 0,
          uniqueVisitors: data.aggregates?.totalUniqueVisitors || 0
        });
        this.updateLineChart(data);
      },
      error: (err) => console.error('Failed to load time series', err)
    });
  }

  loadDevices() {
    this.api.devices(this.shortCode()).subscribe({
      next: (data) => {
        this.devices.set(data);
        this.updateDoughnutChart(data);
      },
      error: (err) => console.error('Failed to load devices', err)
    });
  }

  loadGeographic() {
    this.api.geographic(this.shortCode()).subscribe({
      next: (data) => this.geographic.set(data),
      error: (err) => console.error('Failed to load geographic', err)
    });
  }

  private updateLineChart(data: TimeSeriesDataDto) {
    const labels = data.points?.map(p => {
      const date = new Date(p.timestamp!);
      return this.selectedInterval === 'hour'
        ? date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
        : date.toLocaleDateString([], { month: 'short', day: 'numeric' });
    }) || [];

    this.lineChartData = {
      labels,
      datasets: [{
        data: data.points?.map(p => p.clicks || 0) || [],
        borderColor: '#3b82f6',
        backgroundColor: 'rgba(59, 130, 246, 0.1)',
        fill: true,
        tension: 0.4
      }]
    };
  }

  private updateDoughnutChart(data: DeviceBreakdownDto) {
    const mv = data.mobileVsDesktop;
    if (!mv) return;

    this.doughnutChartData = {
      labels: ['Desktop', 'Mobile', 'Tablet'],
      datasets: [{
        data: [mv.desktopCount || 0, mv.mobileCount || 0, mv.tabletCount || 0],
        backgroundColor: ['#3b82f6', '#10b981', '#f59e0b'],
        borderWidth: 0
      }]
    };
  }

  async copyUrl() {
    await navigator.clipboard.writeText(this.shortUrl());
    this.copied.set(true);
    setTimeout(() => this.copied.set(false), 2000);
  }
}
