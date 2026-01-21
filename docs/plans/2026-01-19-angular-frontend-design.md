# Angular Frontend Design

## Overview
Full management dashboard for URLShortener API with analytics, URL CRUD, and bulk operations.

## Tech Stack
- Angular 17 (standalone components, signals)
- Tailwind CSS (utility-first styling)
- Chart.js + ng2-charts (visualizations)
- NSwag (TypeScript client generation from OpenAPI)

## Project Structure
```
URLShortener.Web/
├── src/app/
│   ├── core/api/api-client.ts       # NSwag-generated
│   ├── features/
│   │   ├── dashboard/               # Analytics dashboard
│   │   ├── urls/                    # URL management
│   │   └── bulk/                    # Bulk operations
│   └── shared/components/           # Reusable UI components
├── nswag.json
└── tailwind.config.js
```

## Features

### Dashboard
- Overview cards (Total URLs, Clicks, Active, Visitors)
- Time series chart (clicks over time)
- Device breakdown doughnut chart
- Country table
- Recent activity feed

### URL Management
- Searchable, sortable data table
- Create URL with custom alias and expiration
- URL detail view with individual stats
- Edit/Delete/Disable actions

### Bulk Operations
- Bulk create from text/CSV
- Bulk delete, disable, export from selection

## API Integration
NSwag generates typed TypeScript client from `/swagger/v1/swagger.json`

## Implementation Order
1. Setup (Tailwind, NSwag)
2. Shared UI components
3. Dashboard
4. URL List
5. URL Create/Detail
6. Bulk Operations
