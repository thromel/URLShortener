# Multi-region infrastructure with automatic failover
terraform {
  required_version = ">= 1.5"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
      configuration_aliases = [
        aws.primary,
        aws.secondary,
        aws.tertiary
      ]
    }
  }
}

# Variables
variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
}

variable "project_name" {
  description = "Project name"
  type        = string
  default     = "url-shortener"
}

variable "primary_region" {
  description = "Primary AWS region"
  type        = string
  default     = "us-east-1"
}

variable "secondary_region" {
  description = "Secondary AWS region for failover"
  type        = string
  default     = "eu-west-1"
}

variable "tertiary_region" {
  description = "Tertiary AWS region for disaster recovery"
  type        = string
  default     = "ap-southeast-1"
}

variable "domain_name" {
  description = "Primary domain name"
  type        = string
}

variable "aurora_scaling_config" {
  description = "Aurora Serverless v2 scaling configuration"
  type = object({
    min_capacity = number
    max_capacity = number
  })
  default = {
    min_capacity = 0.5
    max_capacity = 64
  }
}

# Data sources
data "aws_availability_zones" "primary" {
  provider = aws.primary
  state    = "available"
}

data "aws_availability_zones" "secondary" {
  provider = aws.secondary
  state    = "available"
}

data "aws_availability_zones" "tertiary" {
  provider = aws.tertiary
  state    = "available"
}

# Route 53 Hosted Zone
resource "aws_route53_zone" "main" {
  provider = aws.primary
  name     = var.domain_name
  
  tags = {
    Environment = var.environment
    Project     = var.project_name
    Purpose     = "DNS management"
  }
}

# Health checks for each region
resource "aws_route53_health_check" "primary_region" {
  provider                        = aws.primary
  fqdn                           = "api-${var.primary_region}.${var.domain_name}"
  port                           = 443
  type                           = "HTTPS"
  resource_path                  = "/health"
  failure_threshold              = 3
  request_interval               = 30
  cloudwatch_logs_region         = var.primary_region
  cloudwatch_alarm_region        = var.primary_region
  measure_latency                = true
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-primary-health-check"
    Environment = var.environment
    Region      = var.primary_region
  }
}

resource "aws_route53_health_check" "secondary_region" {
  provider                        = aws.secondary
  fqdn                           = "api-${var.secondary_region}.${var.domain_name}"
  port                           = 443
  type                           = "HTTPS"
  resource_path                  = "/health"
  failure_threshold              = 3
  request_interval               = 30
  cloudwatch_logs_region         = var.secondary_region
  cloudwatch_alarm_region        = var.secondary_region
  measure_latency                = true
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-secondary-health-check"
    Environment = var.environment
    Region      = var.secondary_region
  }
}

# Primary region DNS record
resource "aws_route53_record" "api_primary" {
  provider = aws.primary
  zone_id  = aws_route53_zone.main.zone_id
  name     = "api.${var.domain_name}"
  type     = "A"
  
  set_identifier = "primary"
  
  failover_routing_policy {
    type = "PRIMARY"
  }
  
  health_check_id = aws_route53_health_check.primary_region.id
  
  alias {
    name                   = aws_lb.primary.dns_name
    zone_id               = aws_lb.primary.zone_id
    evaluate_target_health = true
  }
}

# Secondary region DNS record
resource "aws_route53_record" "api_secondary" {
  provider = aws.secondary
  zone_id  = aws_route53_zone.main.zone_id
  name     = "api.${var.domain_name}"
  type     = "A"
  
  set_identifier = "secondary"
  
  failover_routing_policy {
    type = "SECONDARY"
  }
  
  health_check_id = aws_route53_health_check.secondary_region.id
  
  alias {
    name                   = aws_lb.secondary.dns_name
    zone_id               = aws_lb.secondary.zone_id
    evaluate_target_health = true
  }
}

# Geolocation-based routing for performance optimization
resource "aws_route53_record" "api_us" {
  provider = aws.primary
  zone_id  = aws_route53_zone.main.zone_id
  name     = "api.${var.domain_name}"
  type     = "A"
  
  set_identifier = "us-users"
  
  geolocation_routing_policy {
    country = "US"
  }
  
  health_check_id = aws_route53_health_check.primary_region.id
  
  alias {
    name                   = aws_lb.primary.dns_name
    zone_id               = aws_lb.primary.zone_id
    evaluate_target_health = true
  }
}

resource "aws_route53_record" "api_eu" {
  provider = aws.secondary
  zone_id  = aws_route53_zone.main.zone_id
  name     = "api.${var.domain_name}"
  type     = "A"
  
  set_identifier = "eu-users"
  
  geolocation_routing_policy {
    continent = "EU"
  }
  
  health_check_id = aws_route53_health_check.secondary_region.id
  
  alias {
    name                   = aws_lb.secondary.dns_name
    zone_id               = aws_lb.secondary.zone_id
    evaluate_target_health = true
  }
}

# KMS Keys for encryption
resource "aws_kms_key" "primary" {
  provider    = aws.primary
  description = "KMS key for ${var.project_name} ${var.environment} primary region"
  
  enable_key_rotation = true
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-primary-kms"
    Environment = var.environment
    Region      = var.primary_region
  }
}

resource "aws_kms_key" "secondary" {
  provider    = aws.secondary
  description = "KMS key for ${var.project_name} ${var.environment} secondary region"
  
  enable_key_rotation = true
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-secondary-kms"
    Environment = var.environment
    Region      = var.secondary_region
  }
}

# Aurora Global Database
resource "aws_rds_global_cluster" "main" {
  provider = aws.primary
  
  global_cluster_identifier   = "${var.project_name}-${var.environment}-global"
  engine                      = "aurora-postgresql"
  engine_version             = "15.4"
  database_name              = "urlshortener"
  storage_encrypted          = true
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-global-db"
    Environment = var.environment
  }
}

# Primary region Aurora cluster
resource "aws_rds_cluster" "primary" {
  provider = aws.primary
  
  cluster_identifier        = "${var.project_name}-${var.environment}-primary"
  global_cluster_identifier = aws_rds_global_cluster.main.id
  engine                   = aws_rds_global_cluster.main.engine
  engine_version           = aws_rds_global_cluster.main.engine_version
  database_name            = aws_rds_global_cluster.main.database_name
  
  vpc_security_group_ids = [aws_security_group.aurora_primary.id]
  db_subnet_group_name   = aws_db_subnet_group.primary.name
  
  serverlessv2_scaling_configuration {
    max_capacity = var.aurora_scaling_config.max_capacity
    min_capacity = var.aurora_scaling_config.min_capacity
  }
  
  # Backup configuration
  backup_retention_period = 30
  preferred_backup_window = "03:00-04:00"
  
  # Security
  storage_encrypted = true
  kms_key_id       = aws_kms_key.primary.arn
  
  # Monitoring
  enabled_cloudwatch_logs_exports = ["postgresql"]
  
  # Performance Insights
  performance_insights_enabled = true
  performance_insights_kms_key_id = aws_kms_key.primary.arn
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-primary-db"
    Environment = var.environment
    Region      = var.primary_region
  }
  
  lifecycle {
    ignore_changes = [engine_version]
  }
}

# Secondary region Aurora cluster (read replica)
resource "aws_rds_cluster" "secondary" {
  provider = aws.secondary
  
  cluster_identifier        = "${var.project_name}-${var.environment}-secondary"
  global_cluster_identifier = aws_rds_global_cluster.main.id
  engine                   = aws_rds_global_cluster.main.engine
  engine_version           = aws_rds_global_cluster.main.engine_version
  
  vpc_security_group_ids = [aws_security_group.aurora_secondary.id]
  db_subnet_group_name   = aws_db_subnet_group.secondary.name
  
  serverlessv2_scaling_configuration {
    max_capacity = var.aurora_scaling_config.max_capacity / 2
    min_capacity = var.aurora_scaling_config.min_capacity
  }
  
  # Security
  storage_encrypted = true
  kms_key_id       = aws_kms_key.secondary.arn
  
  # Monitoring
  enabled_cloudwatch_logs_exports = ["postgresql"]
  
  # Performance Insights
  performance_insights_enabled = true
  performance_insights_kms_key_id = aws_kms_key.secondary.arn
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-secondary-db"
    Environment = var.environment
    Region      = var.secondary_region
  }
  
  depends_on = [aws_rds_cluster.primary]
  
  lifecycle {
    ignore_changes = [engine_version]
  }
}

# Aurora cluster instances
resource "aws_rds_cluster_instance" "primary" {
  provider = aws.primary
  count    = var.environment == "prod" ? 2 : 1
  
  identifier         = "${var.project_name}-${var.environment}-primary-${count.index + 1}"
  cluster_identifier = aws_rds_cluster.primary.id
  instance_class     = "db.serverless"
  engine             = aws_rds_cluster.primary.engine
  engine_version     = aws_rds_cluster.primary.engine_version
  
  performance_insights_enabled = true
  monitoring_interval          = 60
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-primary-instance-${count.index + 1}"
    Environment = var.environment
    Region      = var.primary_region
  }
}

resource "aws_rds_cluster_instance" "secondary" {
  provider = aws.secondary
  count    = var.environment == "prod" ? 2 : 1
  
  identifier         = "${var.project_name}-${var.environment}-secondary-${count.index + 1}"
  cluster_identifier = aws_rds_cluster.secondary.id
  instance_class     = "db.serverless"
  engine             = aws_rds_cluster.secondary.engine
  engine_version     = aws_rds_cluster.secondary.engine_version
  
  performance_insights_enabled = true
  monitoring_interval          = 60
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-secondary-instance-${count.index + 1}"
    Environment = var.environment
    Region      = var.secondary_region
  }
}

# VPC and networking for primary region
module "vpc_primary" {
  source = "../vpc"
  providers = {
    aws = aws.primary
  }
  
  environment     = var.environment
  project_name    = var.project_name
  region_name     = var.primary_region
  vpc_cidr        = "10.0.0.0/16"
  availability_zones = data.aws_availability_zones.primary.names
}

# VPC and networking for secondary region
module "vpc_secondary" {
  source = "../vpc"
  providers = {
    aws = aws.secondary
  }
  
  environment     = var.environment
  project_name    = var.project_name
  region_name     = var.secondary_region
  vpc_cidr        = "10.1.0.0/16"
  availability_zones = data.aws_availability_zones.secondary.names
}

# EKS clusters
module "eks_primary" {
  source = "../eks"
  providers = {
    aws = aws.primary
  }
  
  environment         = var.environment
  project_name        = var.project_name
  vpc_id             = module.vpc_primary.vpc_id
  private_subnet_ids = module.vpc_primary.private_subnet_ids
  kms_key_arn        = aws_kms_key.primary.arn
  
  node_groups = {
    api = {
      instance_types = ["m6i.large", "m6i.xlarge"]
      min_size      = var.environment == "prod" ? 3 : 1
      max_size      = var.environment == "prod" ? 20 : 5
      desired_size  = var.environment == "prod" ? 5 : 2
    }
    worker = {
      instance_types = ["m6i.medium", "m6i.large"]
      min_size      = var.environment == "prod" ? 2 : 1
      max_size      = var.environment == "prod" ? 10 : 3
      desired_size  = var.environment == "prod" ? 3 : 1
    }
  }
}

module "eks_secondary" {
  source = "../eks"
  providers = {
    aws = aws.secondary
  }
  
  environment         = var.environment
  project_name        = var.project_name
  vpc_id             = module.vpc_secondary.vpc_id
  private_subnet_ids = module.vpc_secondary.private_subnet_ids
  kms_key_arn        = aws_kms_key.secondary.arn
  
  node_groups = {
    api = {
      instance_types = ["m6i.large", "m6i.xlarge"]
      min_size      = var.environment == "prod" ? 2 : 1
      max_size      = var.environment == "prod" ? 15 : 3
      desired_size  = var.environment == "prod" ? 3 : 1
    }
    worker = {
      instance_types = ["m6i.medium", "m6i.large"]
      min_size      = var.environment == "prod" ? 1 : 1
      max_size      = var.environment == "prod" ? 8 : 2
      desired_size  = var.environment == "prod" ? 2 : 1
    }
  }
}

# Load Balancers
resource "aws_lb" "primary" {
  provider = aws.primary
  
  name               = "${var.project_name}-${var.environment}-primary-alb"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb_primary.id]
  subnets           = module.vpc_primary.public_subnet_ids
  
  enable_deletion_protection = var.environment == "prod"
  
  access_logs {
    bucket  = aws_s3_bucket.alb_logs_primary.bucket
    prefix  = "primary-alb"
    enabled = true
  }
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-primary-alb"
    Environment = var.environment
    Region      = var.primary_region
  }
}

resource "aws_lb" "secondary" {
  provider = aws.secondary
  
  name               = "${var.project_name}-${var.environment}-secondary-alb"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb_secondary.id]
  subnets           = module.vpc_secondary.public_subnet_ids
  
  enable_deletion_protection = var.environment == "prod"
  
  access_logs {
    bucket  = aws_s3_bucket.alb_logs_secondary.bucket
    prefix  = "secondary-alb"
    enabled = true
  }
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-secondary-alb"
    Environment = var.environment
    Region      = var.secondary_region
  }
}

# ElastiCache Redis clusters for each region
module "redis_primary" {
  source = "../redis"
  providers = {
    aws = aws.primary
  }
  
  environment           = var.environment
  project_name         = var.project_name
  vpc_id              = module.vpc_primary.vpc_id
  private_subnet_ids  = module.vpc_primary.private_subnet_ids
  
  node_type           = var.environment == "prod" ? "cache.r7g.large" : "cache.r7g.medium"
  num_cache_nodes     = var.environment == "prod" ? 3 : 1
  
  # Cross-region replication
  replication_group_description = "Primary Redis cluster for ${var.project_name}"
  
  # Global replication for cache synchronization
  global_replication_group_id = "${var.project_name}-${var.environment}-global-redis"
}

module "redis_secondary" {
  source = "../redis"
  providers = {
    aws = aws.secondary
  }
  
  environment           = var.environment
  project_name         = var.project_name
  vpc_id              = module.vpc_secondary.vpc_id
  private_subnet_ids  = module.vpc_secondary.private_subnet_ids
  
  node_type           = var.environment == "prod" ? "cache.r7g.large" : "cache.r7g.medium"
  num_cache_nodes     = var.environment == "prod" ? 2 : 1
  
  # Cross-region replication
  replication_group_description = "Secondary Redis cluster for ${var.project_name}"
  
  # Global replication for cache synchronization
  global_replication_group_id = "${var.project_name}-${var.environment}-global-redis"
}

# CloudWatch for monitoring and alerting
resource "aws_cloudwatch_dashboard" "multi_region" {
  provider       = aws.primary
  dashboard_name = "${var.project_name}-${var.environment}-multi-region"
  
  dashboard_body = jsonencode({
    widgets = [
      {
        type   = "metric"
        x      = 0
        y      = 0
        width  = 12
        height = 6
        
        properties = {
          metrics = [
            ["AWS/Route53", "HealthCheckStatus", "HealthCheckId", aws_route53_health_check.primary_region.id],
            [".", ".", ".", aws_route53_health_check.secondary_region.id],
          ]
          view    = "timeSeries"
          stacked = false
          region  = var.primary_region
          title   = "Regional Health Checks"
          period  = 300
        }
      },
      {
        type   = "metric"
        x      = 12
        y      = 0
        width  = 12
        height = 6
        
        properties = {
          metrics = [
            ["AWS/RDS", "DatabaseConnections", "DBClusterIdentifier", aws_rds_cluster.primary.cluster_identifier],
            [".", ".", ".", aws_rds_cluster.secondary.cluster_identifier],
          ]
          view    = "timeSeries"
          stacked = false
          region  = var.primary_region
          title   = "Database Connections"
          period  = 300
        }
      }
    ]
  })
}

# CloudWatch Alarms for automatic failover triggers
resource "aws_cloudwatch_metric_alarm" "primary_region_health" {
  provider = aws.primary
  
  alarm_name          = "${var.project_name}-${var.environment}-primary-region-health"
  comparison_operator = "LessThanThreshold"
  evaluation_periods  = "2"
  metric_name         = "HealthCheckStatus"
  namespace           = "AWS/Route53"
  period              = "60"
  statistic           = "Minimum"
  threshold           = "1"
  alarm_description   = "This metric monitors primary region health"
  alarm_actions       = [aws_sns_topic.alerts_primary.arn]
  
  dimensions = {
    HealthCheckId = aws_route53_health_check.primary_region.id
  }
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-primary-health-alarm"
    Environment = var.environment
  }
}

# SNS topics for notifications
resource "aws_sns_topic" "alerts_primary" {
  provider = aws.primary
  name     = "${var.project_name}-${var.environment}-alerts-primary"
  
  tags = {
    Environment = var.environment
    Purpose     = "Monitoring alerts"
  }
}

resource "aws_sns_topic" "alerts_secondary" {
  provider = aws.secondary
  name     = "${var.project_name}-${var.environment}-alerts-secondary"
  
  tags = {
    Environment = var.environment
    Purpose     = "Monitoring alerts"
  }
}

# S3 buckets for ALB logs
resource "aws_s3_bucket" "alb_logs_primary" {
  provider = aws.primary
  bucket   = "${var.project_name}-${var.environment}-alb-logs-primary-${random_id.bucket_suffix.hex}"
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-alb-logs-primary"
    Environment = var.environment
    Region      = var.primary_region
  }
}

resource "aws_s3_bucket" "alb_logs_secondary" {
  provider = aws.secondary
  bucket   = "${var.project_name}-${var.environment}-alb-logs-secondary-${random_id.bucket_suffix.hex}"
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-alb-logs-secondary"
    Environment = var.environment
    Region      = var.secondary_region
  }
}

resource "random_id" "bucket_suffix" {
  byte_length = 4
}

# DB subnet groups
resource "aws_db_subnet_group" "primary" {
  provider = aws.primary
  name     = "${var.project_name}-${var.environment}-primary-db-subnet"
  subnet_ids = module.vpc_primary.private_subnet_ids
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-primary-db-subnet"
    Environment = var.environment
  }
}

resource "aws_db_subnet_group" "secondary" {
  provider = aws.secondary
  name     = "${var.project_name}-${var.environment}-secondary-db-subnet"
  subnet_ids = module.vpc_secondary.private_subnet_ids
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-secondary-db-subnet"
    Environment = var.environment
  }
}

# Security Groups (simplified - full implementation would include detailed rules)
resource "aws_security_group" "aurora_primary" {
  provider = aws.primary
  name     = "${var.project_name}-${var.environment}-aurora-primary"
  vpc_id   = module.vpc_primary.vpc_id
  
  ingress {
    from_port   = 5432
    to_port     = 5432
    protocol    = "tcp"
    cidr_blocks = [module.vpc_primary.vpc_cidr]
  }
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-aurora-primary"
    Environment = var.environment
  }
}

resource "aws_security_group" "aurora_secondary" {
  provider = aws.secondary
  name     = "${var.project_name}-${var.environment}-aurora-secondary"
  vpc_id   = module.vpc_secondary.vpc_id
  
  ingress {
    from_port   = 5432
    to_port     = 5432
    protocol    = "tcp"
    cidr_blocks = [module.vpc_secondary.vpc_cidr]
  }
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-aurora-secondary"
    Environment = var.environment
  }
}

resource "aws_security_group" "alb_primary" {
  provider = aws.primary
  name     = "${var.project_name}-${var.environment}-alb-primary"
  vpc_id   = module.vpc_primary.vpc_id
  
  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
  
  ingress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
  
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-alb-primary"
    Environment = var.environment
  }
}

resource "aws_security_group" "alb_secondary" {
  provider = aws.secondary
  name     = "${var.project_name}-${var.environment}-alb-secondary"
  vpc_id   = module.vpc_secondary.vpc_id
  
  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
  
  ingress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
  
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
  
  tags = {
    Name        = "${var.project_name}-${var.environment}-alb-secondary"
    Environment = var.environment
  }
}

# Outputs
output "global_cluster_identifier" {
  description = "Aurora Global Cluster identifier"
  value       = aws_rds_global_cluster.main.global_cluster_identifier
}

output "primary_cluster_endpoint" {
  description = "Primary Aurora cluster endpoint"
  value       = aws_rds_cluster.primary.endpoint
}

output "secondary_cluster_endpoint" {
  description = "Secondary Aurora cluster endpoint"
  value       = aws_rds_cluster.secondary.endpoint
}

output "route53_zone_id" {
  description = "Route 53 hosted zone ID"
  value       = aws_route53_zone.main.zone_id
}

output "primary_load_balancer_dns" {
  description = "Primary region load balancer DNS name"
  value       = aws_lb.primary.dns_name
}

output "secondary_load_balancer_dns" {
  description = "Secondary region load balancer DNS name"
  value       = aws_lb.secondary.dns_name
}

output "primary_eks_cluster_name" {
  description = "Primary EKS cluster name"
  value       = module.eks_primary.cluster_name
}

output "secondary_eks_cluster_name" {
  description = "Secondary EKS cluster name"
  value       = module.eks_secondary.cluster_name
} 