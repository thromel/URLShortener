module "vpc" {
  source  = "terraform-aws-modules/vpc/aws"
  version = "~> 3.0"
  
  name = "${var.project_name}-vpc"
  cidr = var.vpc_cidr
  
  azs              = var.azs
  private_subnets  = [for k, v in var.azs : cidrsubnet(var.vpc_cidr, 4, k)]
  public_subnets   = [for k, v in var.azs : cidrsubnet(var.vpc_cidr, 8, k + 48)]
  database_subnets = [for k, v in var.azs : cidrsubnet(var.vpc_cidr, 8, k + 56)]
  
  create_database_subnet_group = true
  
  enable_nat_gateway   = true
  single_nat_gateway   = true
  enable_dns_hostnames = true
  enable_dns_support   = true
  
  # Add required tags for EKS
  public_subnet_tags = {
    "kubernetes.io/cluster/${var.cluster_name}" = "shared"
    "kubernetes.io/role/elb"                    = "1"
  }
  
  private_subnet_tags = {
    "kubernetes.io/cluster/${var.cluster_name}" = "shared"
    "kubernetes.io/role/internal-elb"           = "1"
  }
  
  tags = var.tags
}

# ElastiCache subnet group
resource "aws_elasticache_subnet_group" "redis" {
  name       = "${var.project_name}-redis-subnet-group"
  subnet_ids = module.vpc.private_subnets
  
  tags = var.tags
}

# Security group for Redis
resource "aws_security_group" "redis" {
  name        = "${var.project_name}-redis-sg"
  description = "Allow Redis inbound traffic from EKS"
  vpc_id      = module.vpc.vpc_id
  
  ingress {
    description     = "Redis from EKS"
    from_port       = 6379
    to_port         = 6379
    protocol        = "tcp"
    security_groups = [module.eks.node_security_group_id]
  }
  
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
  
  tags = var.tags
}

# DB parameter group
resource "aws_db_parameter_group" "postgres" {
  name   = "${var.project_name}-postgres-params"
  family = "aurora-postgresql13"
  
  parameter {
    name  = "log_statement"
    value = "all"
  }
  
  tags = var.tags
}

# RDS cluster parameter group
resource "aws_rds_cluster_parameter_group" "postgres" {
  name        = "${var.project_name}-postgres-cluster-params"
  family      = "aurora-postgresql13"
  description = "RDS cluster parameter group for URL Shortener"
  
  parameter {
    name  = "log_statement"
    value = "all"
  }
  
  tags = var.tags
}
