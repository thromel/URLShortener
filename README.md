# Modern URL Shortener

A scalable, cloud-native URL shortening service built with .NET 8.0, Angular, and AWS infrastructure.

## 🚀 Features

- Short URL generation with custom aliases
- High-performance redirects with Redis caching
- URL expiration and analytics
- CQRS architecture with domain-driven design
- Multi-layer caching strategy
- Responsive Angular frontend with NgRx state management
- Kubernetes orchestration and cloud-native design
- Horizontal scaling support

## 🛠️ Tech Stack

### Backend
- .NET 8.0 with ASP.NET Core
- Entity Framework Core with PostgreSQL
- Redis for distributed caching
- CQRS pattern for command/query separation
- Domain-Driven Design (DDD)

### Frontend
- Angular 17+ with TypeScript
- NgRx for state management
- Bootstrap 5 for responsive UI
- RxJS for reactive programming

### Infrastructure
- AWS EKS (Kubernetes)
- Aurora PostgreSQL
- ElastiCache (Redis)
- Terraform for Infrastructure as Code
- GitHub Actions for CI/CD

## 🏗️ Prerequisites

- .NET 8.0 SDK
- Node.js and npm
- Docker and Docker Compose
- PostgreSQL and Redis (or use Docker containers)

## 🚦 Getting Started

### Local Development

1. Clone the repository
   ```bash
   git clone https://github.com/thromel/URLShortener.git
   cd URLShortener
   ```

2. Run with Docker Compose (easiest option)
   ```bash
   docker-compose up
   ```
   This will start the API, PostgreSQL, Redis, and Angular frontend.
### Manual Setup (without Docker)

1. Set up the database
   ```bash
   # Start PostgreSQL locally or connect to an existing instance
   # Create a database named 'urlshortener'
   ```

2. Set up Redis
   ```bash
   # Start Redis locally or connect to an existing instance
   ```

3. Build and run the API
   ```bash
   cd URLShortener
   dotnet restore
   dotnet build
   cd URLShortener.API
   dotnet run
   ```

4. Run the Angular frontend
   ```bash
   cd URLShortener.Web
   npm install
   npm start
   ```

## 🧪 Testing the Application

1. API Endpoints:
   - Create Short URL: POST https://localhost:5001/api/shorten
   - Get URL Info: GET https://localhost:5001/api/{shortCode}
   - Redirect: GET https://localhost:5001/{shortCode}

2. Use Swagger UI at https://localhost:5001/swagger to test the API endpoints

3. Access the Angular frontend at http://localhost:4200

## 🏛️ Architecture

### Domain-Driven Design

The application follows DDD principles with these key components:

- **Domain Layer** (Core): Contains the business logic, entities, and domain services
- **Application Layer** (Core/CQRS): Implements CQRS pattern with commands and queries
- **Infrastructure Layer**: Provides implementations for repositories and external services
- **API Layer**: Exposes RESTful endpoints and handles HTTP requests

### Caching Strategy

The application uses a multi-layer caching approach:

1. **Redis Distributed Cache**: Stores frequently accessed URLs for fast retrieval
2. **Database**: Serves as the persistent storage for all short URLs

### Scalability Considerations

- **Horizontal Scaling**: Containerized services can scale horizontally behind a load balancer
- **Database Partitioning**: PostgreSQL can be sharded for high-volume scenarios
- **Redis Clustering**: Redis can be clustered for high-availability and throughput

## 🚀 Deployment

### AWS Deployment

1. Set up AWS EKS cluster with Terraform
   ```bash
   cd infrastructure/terraform
   terraform init
   terraform plan
   terraform apply
   ```

2. Configure kubectl to use the EKS cluster
   ```bash
   aws eks update-kubeconfig --name urlshortener-cluster --region us-west-2
   ```

3. Deploy application using Kubernetes manifests
   ```bash
   kubectl apply -f infrastructure/kubernetes/
   ```

## 📦 Project Structure

```
URLShortener/
├── URLShortener.API/          # API controllers and configuration
├── URLShortener.Core/         # Domain models, interfaces, CQRS components
├── URLShortener.Infrastructure/ # Repository implementations, DB context
├── URLShortener.Web/          # Angular frontend
├── infrastructure/            # IaC and Kubernetes manifests
└── docker-compose.yml         # Local development setup
```

## 🛠️ Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📜 License

Distributed under the MIT License. See `LICENSE` for more information.

## 📝 Infrastructure Details

The application is deployed to AWS using Terraform and Kubernetes. The infrastructure includes:

- EKS Kubernetes cluster for container orchestration
- Aurora PostgreSQL for the database
- ElastiCache Redis for distributed caching
- Application Load Balancer for traffic distribution
- Auto-scaling node groups for horizontal scaling
- VPC with public and private subnets

To deploy the infrastructure to AWS:

1. Configure AWS CLI with your credentials
   ```bash
   aws configure
   ```

2. Initialize and apply Terraform configuration
   ```bash
   cd infrastructure/terraform
   terraform init
   terraform plan
   terraform apply
   ```

3. Configure kubectl to work with your EKS cluster
   ```bash
   aws eks update-kubeconfig --name urlshortener-cluster --region us-west-2
   ```

4. Deploy the application to Kubernetes
   ```bash
   kubectl apply -f infrastructure/kubernetes/
   ```

5. Get the load balancer URL
   ```bash
   kubectl get ingress urlshortener-ingress
   ```


You can customize the infrastructure by modifying the Terraform files in the `infrastructure/terraform` directory.
