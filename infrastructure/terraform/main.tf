terraform {
  required_version = ">= 1.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.23"
    }
    helm = {
      source  = "hashicorp/helm"
      version = "~> 2.10"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.1"
    }
  }
}

provider "aws" {
  region = var.aws_region
  
  default_tags {
    tags = var.tags
  }
}

provider "kubernetes" {
  host                   = module.eks.cluster_endpoint
  cluster_ca_certificate = base64decode(module.eks.cluster_certificate_authority_data)
  
  exec {
    api_version = "client.authentication.k8s.io/v1beta1"
    command     = "aws"
    args        = ["eks", "get-token", "--cluster-name", module.eks.cluster_name]
  }
}

provider "helm" {
  kubernetes {
    host                   = module.eks.cluster_endpoint
    cluster_ca_certificate = base64decode(module.eks.cluster_certificate_authority_data)
    
    exec {
      api_version = "client.authentication.k8s.io/v1beta1"
      command     = "aws"
      args        = ["eks", "get-token", "--cluster-name", module.eks.cluster_name]
    }
  }
}

# Local values for resource naming
locals {
  name_prefix = "${var.project_name}-${var.environment}"
}

# VPC Module
module "vpc" {
  source = "./modules/vpc"
  
  name_prefix        = local.name_prefix
  cidr               = var.vpc_cidr
  availability_zones = var.availability_zones
  tags               = var.tags
}

# EKS Module
module "eks" {
  source = "./modules/eks"
  
  cluster_name    = "${local.name_prefix}-cluster"
  cluster_version = var.kubernetes_version
  
  vpc_id          = module.vpc.vpc_id
  subnet_ids      = module.vpc.private_subnets
  
  node_groups = var.eks_node_groups
  tags        = var.tags
  
  depends_on = [module.vpc]
}

# RDS PostgreSQL Module
module "rds" {
  source = "./modules/rds"
  
  name_prefix = local.name_prefix
  
  vpc_id     = module.vpc.vpc_id
  subnet_ids = module.vpc.database_subnets
  
  allowed_security_group_ids = [module.eks.worker_security_group_id]
  
  instance_class    = var.rds_instance_class
  allocated_storage = var.rds_allocated_storage
  
  database_name = var.database_name
  database_user = var.database_user
  
  backup_retention_period = var.rds_backup_retention_period
  backup_window          = var.rds_backup_window
  maintenance_window     = var.rds_maintenance_window
  
  tags = var.tags
}

# ElastiCache Redis Module
module "redis" {
  source = "./modules/redis"
  
  name_prefix = local.name_prefix
  
  vpc_id     = module.vpc.vpc_id
  subnet_ids = module.vpc.private_subnets
  
  allowed_security_group_ids = [module.eks.worker_security_group_id]
  
  node_type               = var.redis_node_type
  num_cache_nodes        = var.redis_num_nodes
  parameter_group_name   = var.redis_parameter_group
  
  tags = var.tags
}

# CloudFront Distribution Module
module "cloudfront" {
  source = "./modules/cloudfront"
  
  name_prefix = local.name_prefix
  
  # We'll point this to our ALB once the application is deployed
  origin_domain_name = module.alb.dns_name
  
  tags = var.tags
}

# Application Load Balancer Module
module "alb" {
  source = "./modules/alb"
  
  name_prefix = local.name_prefix
  
  vpc_id     = module.vpc.vpc_id
  subnet_ids = module.vpc.public_subnets
  
  certificate_arn = module.acm.certificate_arn
  
  tags = var.tags
}

# ACM Certificate Module
module "acm" {
  source = "./modules/acm"
  
  domain_name = var.domain_name
  zone_id     = var.route53_zone_id
  
  tags = var.tags
}

# Secrets Manager for application secrets
resource "aws_secretsmanager_secret" "app_secrets" {
  name_prefix = "${local.name_prefix}-secrets"
  description = "Application secrets for URL Shortener"
  
  tags = var.tags
}

resource "aws_secretsmanager_secret_version" "app_secrets" {
  secret_id = aws_secretsmanager_secret.app_secrets.id
  secret_string = jsonencode({
    postgres_connection_string = "Host=${module.rds.endpoint};Database=${var.database_name};Username=${var.database_user};Password=${module.rds.password}"
    redis_connection_string   = "${module.redis.endpoint}:${module.redis.port}"
    jwt_secret_key           = random_password.jwt_secret.result
    cloudfront_distribution_id = module.cloudfront.distribution_id
  })
}

# Random password for JWT secret
resource "random_password" "jwt_secret" {
  length  = 32
  special = true
}

# IAM role for the application pods
resource "aws_iam_role" "app_role" {
  name_prefix = "${local.name_prefix}-app-role"
  
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRoleWithWebIdentity"
        Effect = "Allow"
        Principal = {
          Federated = module.eks.oidc_provider_arn
        }
        Condition = {
          StringEquals = {
            "${replace(module.eks.cluster_oidc_issuer_url, "https://", "")}:sub" = "system:serviceaccount:default:urlshortener-app"
            "${replace(module.eks.cluster_oidc_issuer_url, "https://", "")}:aud" = "sts.amazonaws.com"
          }
        }
      }
    ]
  })
  
  tags = var.tags
}

# IAM policy for the application
resource "aws_iam_role_policy" "app_policy" {
  name_prefix = "${local.name_prefix}-app-policy"
  role        = aws_iam_role.app_role.id
  
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue",
          "secretsmanager:DescribeSecret"
        ]
        Resource = aws_secretsmanager_secret.app_secrets.arn
      },
      {
        Effect = "Allow"
        Action = [
          "cloudfront:CreateInvalidation",
          "cloudfront:GetInvalidation",
          "cloudfront:ListInvalidations"
        ]
        Resource = module.cloudfront.distribution_arn
      },
      {
        Effect = "Allow"
        Action = [
          "logs:CreateLogGroup",
          "logs:CreateLogStream",
          "logs:PutLogEvents",
          "logs:DescribeLogStreams",
          "logs:DescribeLogGroups"
        ]
        Resource = "arn:aws:logs:${var.aws_region}:*:*"
      }
    ]
  })
}
