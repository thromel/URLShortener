# ElastiCache Subnet Group
resource "aws_elasticache_subnet_group" "main" {
  name       = "${var.name_prefix}-redis-subnet-group"
  subnet_ids = var.subnet_ids

  tags = var.tags
}

# Security Group for Redis
resource "aws_security_group" "redis" {
  name_prefix = "${var.name_prefix}-redis-"
  vpc_id      = var.vpc_id

  ingress {
    description     = "Redis"
    from_port       = 6379
    to_port         = 6379
    protocol        = "tcp"
    security_groups = var.allowed_security_group_ids
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-redis-sg"
  })
}

# ElastiCache Replication Group
resource "aws_elasticache_replication_group" "main" {
  replication_group_id          = "${var.name_prefix}-redis"
  description                   = "Redis cluster for ${var.name_prefix}"
  
  node_type                     = var.node_type
  port                         = 6379
  parameter_group_name         = var.parameter_group_name
  
  num_cache_clusters           = var.num_cache_nodes
  
  subnet_group_name            = aws_elasticache_subnet_group.main.name
  security_group_ids           = [aws_security_group.redis.id]
  
  automatic_failover_enabled   = var.num_cache_nodes > 1 ? true : false
  multi_az_enabled            = var.num_cache_nodes > 1 ? true : false
  
  at_rest_encryption_enabled  = true
  transit_encryption_enabled  = false
  
  apply_immediately           = true
  
  tags = var.tags
} 