import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData } from 'chart.js';
import { ApiClient, DashboardOverviewDto, TimeSeriesDataDto, DeviceBreakdownDto } from '../../core/api/api-client.generated';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, BaseChartDirective],
  template: `
    <div class="space-y-6">
      <!-- Header -->
      <div class="flex items-center justify-between">
        <div>
          <h1 class="text-2xl font-bold text-gray-900">Dashboard</h1>
          <p class="text-gray-500 mt-1">Overview of your URL performance</p>
        </div>
        <div class="flex items-center gap-2">
          <select class="input w-40" [(value)]="selectedInterval" (change)="loadTimeSeries()">
            <option value="hour">Hourly</option>
            <option value="day">Daily</option>
            <option value="week">Weekly</option>
          </select>
        </div>
      </div>

      <!-- Stats Cards -->
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        @if (overview()) {
          <div class="stat-card">
            <div class="flex items-center justify-between">
              <div>
                <p class="stat-label">Total URLs</p>
                <p class="stat-value">{{ overview()!.totalUrls?.value | number }}</p>
              </div>
              <div class="w-12 h-12 bg-primary-100 rounded-full flex items-center justify-center">
                <svg class="w-6 h-6 text-primary-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101" />
                </svg>
              </div>
            </div>
            <div class="stat-change" [class.stat-change-up]="(overview()!.totalUrls?.percentChange || 0) > 0" [class.stat-change-down]="(overview()!.totalUrls?.percentChange || 0) < 0">
              @if ((overview()!.totalUrls?.percentChange || 0) > 0) {
                <svg class="w-4 h-4" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M5.293 9.707a1 1 0 010-1.414l4-4a1 1 0 011.414 0l4 4a1 1 0 01-1.414 1.414L11 7.414V15a1 1 0 11-2 0V7.414L6.707 9.707a1 1 0 01-1.414 0z" clip-rule="evenodd" /></svg>
              } @else if ((overview()!.totalUrls?.percentChange || 0) < 0) {
                <svg class="w-4 h-4" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M14.707 10.293a1 1 0 010 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 111.414-1.414L9 12.586V5a1 1 0 012 0v7.586l2.293-2.293a1 1 0 011.414 0z" clip-rule="evenodd" /></svg>
              }
              <span>{{ overview()!.totalUrls?.percentChange | number:'1.1-1' }}% from previous</span>
            </div>
          </div>

          <div class="stat-card">
            <div class="flex items-center justify-between">
              <div>
                <p class="stat-label">Total Clicks</p>
                <p class="stat-value">{{ overview()!.totalClicks?.value | number }}</p>
              </div>
              <div class="w-12 h-12 bg-green-100 rounded-full flex items-center justify-center">
                <svg class="w-6 h-6 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 15l-2 5L9 9l11 4-5 2zm0 0l5 5M7.188 2.239l.777 2.897M5.136 7.965l-2.898-.777M13.95 4.05l-2.122 2.122m-5.657 5.656l-2.12 2.122" />
                </svg>
              </div>
            </div>
            <div class="stat-change" [class.stat-change-up]="(overview()!.totalClicks?.percentChange || 0) > 0" [class.stat-change-down]="(overview()!.totalClicks?.percentChange || 0) < 0">
              <span>{{ overview()!.totalClicks?.percentChange | number:'1.1-1' }}% from previous</span>
            </div>
          </div>

          <div class="stat-card">
            <div class="flex items-center justify-between">
              <div>
                <p class="stat-label">Active URLs</p>
                <p class="stat-value">{{ overview()!.activeUrls?.value | number }}</p>
              </div>
              <div class="w-12 h-12 bg-yellow-100 rounded-full flex items-center justify-center">
                <svg class="w-6 h-6 text-yellow-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
              </div>
            </div>
            <div class="stat-change" [class.stat-change-up]="(overview()!.activeUrls?.percentChange || 0) > 0" [class.stat-change-down]="(overview()!.activeUrls?.percentChange || 0) < 0">
              <span>{{ overview()!.activeUrls?.percentChange | number:'1.1-1' }}% from previous</span>
            </div>
          </div>

          <div class="stat-card">
            <div class="flex items-center justify-between">
              <div>
                <p class="stat-label">Unique Visitors</p>
                <p class="stat-value">{{ overview()!.uniqueVisitors?.value | number }}</p>
              </div>
              <div class="w-12 h-12 bg-purple-100 rounded-full flex items-center justify-center">
                <svg class="w-6 h-6 text-purple-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
                </svg>
              </div>
            </div>
            <div class="stat-change" [class.stat-change-up]="(overview()!.uniqueVisitors?.percentChange || 0) > 0" [class.stat-change-down]="(overview()!.uniqueVisitors?.percentChange || 0) < 0">
              <span>{{ overview()!.uniqueVisitors?.percentChange | number:'1.1-1' }}% from previous</span>
            </div>
          </div>
        } @else {
          @for (i of [1,2,3,4]; track i) {
            <div class="stat-card animate-pulse">
              <div class="h-4 bg-gray-200 rounded w-24 mb-2"></div>
              <div class="h-8 bg-gray-200 rounded w-16"></div>
            </div>
          }
        }
      </div>

      <!-- Charts Row -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Time Series Chart -->
        <div class="card">
          <div class="card-header">Clicks Over Time</div>
          <div class="card-body">
            @if (lineChartData) {
              <canvas baseChart
                [data]="lineChartData"
                [options]="lineChartOptions"
                [type]="'line'">
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
                [type]="'doughnut'">
              </canvas>
            } @else {
              <div class="h-64 flex items-center justify-center text-gray-400">
                Loading chart...
              </div>
            }
          </div>
        </div>
      </div>

      <!-- Top URLs & Recent Activity -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Top URLs -->
        <div class="card">
          <div class="card-header">Top URLs</div>
          <div class="card-body p-0">
            @if (overview()?.topUrls?.length) {
              <table class="table">
                <thead>
                  <tr>
                    <th>Short Code</th>
                    <th>Clicks</th>
                    <th>% of Total</th>
                  </tr>
                </thead>
                <tbody>
                  @for (url of overview()!.topUrls; track url.shortCode) {
                    <tr>
                      <td>
                        <a href="/r/{{ url.shortCode }}" target="_blank" class="text-primary-600 hover:underline font-medium">
                          {{ url.shortCode }}
                        </a>
                      </td>
                      <td>{{ url.clicks | number }}</td>
                      <td>
                        <div class="flex items-center gap-2">
                          <div class="flex-1 h-2 bg-gray-200 rounded-full overflow-hidden">
                            <div class="h-full bg-primary-500 rounded-full" [style.width.%]="url.percentOfTotal"></div>
                          </div>
                          <span class="text-xs text-gray-500 w-12">{{ url.percentOfTotal | number:'1.1-1' }}%</span>
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            } @else {
              <div class="p-6 text-center text-gray-400">
                No URLs yet. Create your first short URL!
              </div>
            }
          </div>
        </div>

        <!-- Recent Activity -->
        <div class="card">
          <div class="card-header">Recent Activity</div>
          <div class="card-body p-0">
            @if (overview()?.recentActivity?.length) {
              <div class="divide-y divide-gray-100">
                @for (activity of overview()!.recentActivity; track activity.timestamp) {
                  <div class="px-4 py-3 flex items-center gap-3">
                    <div class="w-8 h-8 rounded-full flex items-center justify-center"
                         [class.bg-green-100]="activity.type === 'clicked'"
                         [class.bg-blue-100]="activity.type === 'created'">
                      @if (activity.type === 'clicked') {
                        <svg class="w-4 h-4 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 15l-2 5L9 9l11 4-5 2z" />
                        </svg>
                      } @else {
                        <svg class="w-4 h-4 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
                        </svg>
                      }
                    </div>
                    <div class="flex-1 min-w-0">
                      <p class="text-sm text-gray-900">
                        <span class="font-medium">{{ activity.shortCode }}</span>
                        {{ activity.type === 'clicked' ? 'was clicked' : 'was created' }}
                      </p>
                      <p class="text-xs text-gray-500 truncate">{{ activity.details }}</p>
                    </div>
                    <div class="text-xs text-gray-400">
                      {{ activity.timestamp | date:'short' }}
                    </div>
                  </div>
                }
              </div>
            } @else {
              <div class="p-6 text-center text-gray-400">
                No recent activity
              </div>
            }
          </div>
        </div>
      </div>

      <!-- System Health -->
      @if (overview()?.systemHealth) {
        <div class="card">
          <div class="card-header flex items-center justify-between">
            <span>System Health</span>
            <span class="badge" [class.badge-success]="overview()!.systemHealth?.status === 'healthy'"
                  [class.badge-warning]="overview()!.systemHealth?.status === 'degraded'"
                  [class.badge-danger]="overview()!.systemHealth?.status === 'unhealthy'">
              {{ overview()!.systemHealth?.status }}
            </span>
          </div>
          <div class="card-body">
            <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
              <div>
                <p class="text-sm text-gray-500 mb-1">Cache Hit Rate</p>
                <div class="flex items-center gap-2">
                  <div class="flex-1 h-2 bg-gray-200 rounded-full overflow-hidden">
                    <div class="h-full bg-green-500 rounded-full" [style.width.%]="(overview()!.systemHealth?.cacheHitRate || 0) * 100"></div>
                  </div>
                  <span class="text-sm font-medium">{{ (overview()!.systemHealth?.cacheHitRate || 0) * 100 | number:'1.0-0' }}%</span>
                </div>
              </div>
              <div>
                <p class="text-sm text-gray-500 mb-1">Avg Response Time</p>
                <p class="text-lg font-semibold text-gray-900">{{ overview()!.systemHealth?.avgResponseTimeMs | number:'1.0-0' }}ms</p>
              </div>
              <div>
                <p class="text-sm text-gray-500 mb-1">Active Connections</p>
                <p class="text-lg font-semibold text-gray-900">{{ overview()!.systemHealth?.activeConnections | number }}</p>
              </div>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class DashboardComponent implements OnInit {
  private api = inject(ApiClient);

  overview = signal<DashboardOverviewDto | null>(null);
  timeSeries = signal<TimeSeriesDataDto | null>(null);
  devices = signal<DeviceBreakdownDto | null>(null);

  selectedInterval = 'hour';

  lineChartData: ChartData<'line'> | null = null;
  lineChartOptions: ChartConfiguration<'line'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false }
    },
    scales: {
      x: {
        grid: { display: false }
      },
      y: {
        beginAtZero: true,
        grid: { color: '#f3f4f6' }
      }
    }
  };

  doughnutChartData: ChartData<'doughnut'> | null = null;
  doughnutChartOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { position: 'bottom' }
    }
  };

  ngOnInit() {
    this.loadOverview();
    this.loadTimeSeries();
    this.loadDevices();
  }

  loadOverview() {
    this.api.overview().subscribe({
      next: (data) => this.overview.set(data),
      error: (err) => console.error('Failed to load overview', err)
    });
  }

  loadTimeSeries() {
    const endDate = new Date();
    const startDate = new Date();
    startDate.setDate(startDate.getDate() - 7);

    this.api.timeseries('', startDate, endDate, this.selectedInterval).subscribe({
      next: (data) => {
        this.timeSeries.set(data);
        this.updateLineChart(data);
      },
      error: (err) => console.error('Failed to load time series', err)
    });
  }

  loadDevices() {
    this.api.devices().subscribe({
      next: (data) => {
        this.devices.set(data);
        this.updateDoughnutChart(data);
      },
      error: (err) => console.error('Failed to load devices', err)
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
        tension: 0.4,
        pointRadius: 0,
        pointHoverRadius: 6
      }]
    };
  }

  private updateDoughnutChart(data: DeviceBreakdownDto) {
    const mobileVsDesktop = data.mobileVsDesktop;
    if (!mobileVsDesktop) return;

    this.doughnutChartData = {
      labels: ['Desktop', 'Mobile', 'Tablet'],
      datasets: [{
        data: [
          mobileVsDesktop.desktopCount || 0,
          mobileVsDesktop.mobileCount || 0,
          mobileVsDesktop.tabletCount || 0
        ],
        backgroundColor: ['#3b82f6', '#10b981', '#f59e0b'],
        borderWidth: 0
      }]
    };
  }
}
