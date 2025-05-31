variable "domain_name" {
  description = "Domain name for the certificate"
  type        = string
  default     = ""
}

variable "zone_id" {
  description = "Route53 hosted zone ID for validation"
  type        = string
  default     = ""
}

variable "tags" {
  description = "Tags to apply to resources"
  type        = map(string)
  default     = {}
} 