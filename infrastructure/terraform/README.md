# URL Shortener AWS Infrastructure

This Terraform configuration deploys a complete AWS infrastructure for the URL Shortener application.

## Architecture

The infrastructure includes:

- **VPC**: Multi-AZ VPC with public, private, and database subnets
- **EKS**: Managed Kubernetes cluster with worker nodes
- **RDS**: PostgreSQL database with enhanced monitoring
- **ElastiCache**: Redis cluster for caching
- **CloudFront**: CDN for global content delivery
- **ALB**: Application Load Balancer with SSL termination
- **ACM**: SSL/TLS certificates (optional)
- **Secrets Manager**: Secure storage for application secrets
- **IAM**: Roles and policies for secure access

## Prerequisites

1. AWS CLI configured with appropriate credentials
2. Terraform >= 1.0 installed
3. kubectl installed (for EKS management)

## Deployment

### 1. Initialize Terraform

```bash
cd infrastructure/terraform
terraform init
```

### 2. Review and Customize Variables

Copy and customize the variables:

```bash
cp terraform.tfvars.example terraform.tfvars
```

Edit `terraform.tfvars` with your specific values:

```hcl
aws_region = "us-west-2"
project_name = "urlshortener"
environment = "dev"

# Optional: For custom domain
domain_name = "yourdomain.com"
route53_zone_id = "Z1234567890ABC"

# Customize instance sizes as needed
rds_instance_class = "db.t3.micro"
redis_node_type = "cache.t3.micro"
```

### 3. Plan the Deployment

```bash
terraform plan
```

### 4. Deploy the Infrastructure

```bash
terraform apply
```

### 5. Configure kubectl

After deployment, configure kubectl to connect to the EKS cluster:

```bash
aws eks update-kubeconfig --region us-west-2 --name urlshortener-dev-cluster
```

## Modules

### VPC Module (`modules/vpc/`)
- Creates VPC with public, private, and database subnets
- Sets up NAT gateways for private subnet internet access
- Configures route tables and security groups

### EKS Module (`modules/eks/`)
- Creates EKS cluster with managed node groups
- Sets up OIDC provider for service account authentication
- Installs essential add-ons (VPC CNI, CoreDNS, kube-proxy, EBS CSI)

### RDS Module (`modules/rds/`)
- PostgreSQL database with encryption at rest
- Enhanced monitoring and performance insights
- Automated backups and maintenance windows

### Redis Module (`modules/redis/`)
- ElastiCache Redis cluster with replication
- Encryption at rest enabled
- Multi-AZ deployment for high availability

### CloudFront Module (`modules/cloudfront/`)
- Global CDN with custom cache behaviors
- S3 bucket for access logs
- Custom error pages for SPA support

### ALB Module (`modules/alb/`)
- Application Load Balancer with SSL termination
- Health checks and target groups
- HTTP to HTTPS redirect

### ACM Module (`modules/acm/`)
- SSL/TLS certificate provisioning
- DNS validation with Route53
- Wildcard certificate support

## Outputs

After deployment, Terraform provides important outputs:

- **EKS cluster endpoint and configuration**
- **RDS endpoint** (sensitive)
- **Redis endpoint** (sensitive)
- **CloudFront distribution details**
- **ALB DNS name**
- **Secrets Manager ARN**

## Security

- All databases are in private subnets
- Encryption at rest enabled for RDS and Redis
- Security groups restrict access between components
- IAM roles follow least privilege principle
- Secrets stored in AWS Secrets Manager

## Monitoring

- RDS Enhanced Monitoring enabled
- CloudFront access logs stored in S3
- EKS cluster logging can be enabled
- Performance Insights for RDS

## Scaling

- EKS node groups support auto-scaling
- RDS supports read replicas (can be added)
- Redis supports cluster mode (can be enabled)
- CloudFront provides global edge locations

## Cost Optimization

- Uses t3.micro instances for development
- EBS GP3 storage for cost efficiency
- CloudFront PriceClass_100 for cost control
- Can be scaled up for production workloads

## Cleanup

To destroy the infrastructure:

```bash
terraform destroy
```

**Warning**: This will permanently delete all resources and data.

## Troubleshooting

### Common Issues

1. **EKS cluster creation timeout**: Increase timeout in provider configuration
2. **RDS subnet group errors**: Ensure subnets are in different AZs
3. **Certificate validation**: Ensure Route53 zone is properly configured

### Useful Commands

```bash
# Check EKS cluster status
aws eks describe-cluster --name urlshortener-dev-cluster

# Get RDS endpoint
aws rds describe-db-instances --db-instance-identifier urlshortener-dev-postgres

# List secrets
aws secretsmanager list-secrets
```

## Next Steps

After infrastructure deployment:

1. Deploy the URL Shortener application to EKS
2. Configure DNS records to point to CloudFront/ALB
3. Set up monitoring and alerting
4. Configure CI/CD pipelines
5. Implement backup and disaster recovery procedures 