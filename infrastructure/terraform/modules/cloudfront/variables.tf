variable "name_prefix" {
  description = "Name prefix for resources"
  type        = string
}

variable "origin_domain_name" {
  description = "Domain name for the origin"
  type        = string
}

variable "tags" {
  description = "Tags to apply to resources"
  type        = map(string)
  default     = {}
} 