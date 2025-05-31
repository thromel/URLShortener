variable "aws_region" {
  description = "AWS region to deploy the infrastructure"
  type        = string
  default     = "us-west-2"
}

variable "project_name" {
  description = "Name of the project, used as prefix for resources"
  type        = string
  default     = "urlshortener"
}

variable "cluster_name" {
  description = "Name of the EKS cluster"
  type        = string
  default     = "urlshortener-cluster"
}

variable "vpc_cidr" {
  description = "CIDR block for the VPC"
  type        = string
  default     = "10.0.0.0/16"
}

variable "azs" {
  description = "Availability zones to use for the VPC"
  type        = list(string)
  default     = ["us-west-2a", "us-west-2b", "us-west-2c"]
}

variable "tags" {
  description = "Tags to apply to all resources"
  type        = map(string)
  default     = {
    Project     = "URLShortener"
    Environment = "Production"
    ManagedBy   = "Terraform"
  }
}
