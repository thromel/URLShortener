provider "aws" {
  region = var.aws_region
}

# Create EKS Cluster
module "eks" {
  source          = "terraform-aws-modules/eks/aws"
  version         = "~> 19.0"
  cluster_name    = var.cluster_name
  cluster_version = "1.27"
  
  vpc_id          = module.vpc.vpc_id
  subnet_ids      = module.vpc.private_subnets
  
  cluster_endpoint_public_access = true
  
  eks_managed_node_groups = {
    main = {
      min_size       = 2
      max_size       = 10
      desired_size   = 3
      instance_types = ["t3.medium"]
    }
  }
  
  # Allow worker nodes to assume role to interact with other AWS services
  node_security_group_additional_rules = {
    ingress_self_all = {
      description = "Node to node all ports/protocols"
      protocol    = "-1"
      from_port   = 0
      to_port     = 0
      type        = "ingress"
      self        = true
    }
    egress_all = {
      description = "Node all egress"
      protocol    = "-1"
      from_port   = 0
      to_port     = 0
      type        = "egress"
      cidr_blocks = ["0.0.0.0/0"]
    }
  }
  
  tags = var.tags
}

# Create Aurora PostgreSQL cluster
module "aurora_postgresql" {
  source  = "terraform-aws-modules/rds-aurora/aws"
  version = "~> 7.0"
  
  name              = "${var.project_name}-postgres"
  engine            = "aurora-postgresql"
  engine_version    = "13.7"
  instance_type     = "db.t3.medium"
  instances = {
    1 = {}
    2 = {}
  }
  
  vpc_id            = module.vpc.vpc_id
  subnets           = module.vpc.database_subnets
  
  allowed_security_groups = [module.eks.node_security_group_id]
  
  storage_encrypted = true
  apply_immediately = true
  
  db_parameter_group_name         = aws_db_parameter_group.postgres.id
  db_cluster_parameter_group_name = aws_rds_cluster_parameter_group.postgres.id
  
  database_name     = "urlshortener"
  master_username   = "postgres"
  master_password   = random_password.database_password.result
  
  tags = var.tags
}

# Create ElastiCache Redis cluster
resource "aws_elasticache_replication_group" "redis" {
  replication_group_id          = "${var.project_name}-redis"
  replication_group_description = "Redis cluster for URL Shortener"
  
  node_type            = "cache.t3.small"
  port                 = 6379
  parameter_group_name = "default.redis6.x"
  
  num_cache_clusters   = 2
  
  subnet_group_name    = aws_elasticache_subnet_group.redis.name
  security_group_ids   = [aws_security_group.redis.id]
  
  automatic_failover_enabled = true
  
  tags = var.tags
}

# Random password for database
resource "random_password" "database_password" {
  length  = 16
  special = false
}

# Create Kubernetes secret for database and Redis credentials
resource "kubernetes_secret" "urlshortener_secrets" {
  depends_on = [module.eks]
  
  metadata {
    name = "urlshortener-secrets"
  }
  
  data = {
    "postgres-connection-string" = "Host=${module.aurora_postgresql.cluster_endpoint};Database=urlshortener;Username=${module.aurora_postgresql.cluster_master_username};Password=${module.aurora_postgresql.cluster_master_password}"
    "redis-connection-string"   = "${aws_elasticache_replication_group.redis.primary_endpoint_address}:${aws_elasticache_replication_group.redis.port}"
  }
  
  type = "Opaque"
}
