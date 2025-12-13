variable "workload" {
  default = "travel-itinerary"
}

variable "environment" {
  default = "dev"
}

variable "location" {
  default = "uksouth"
}

variable "subscription_id" {}

variable "log_analytics_subscription_id" {}
variable "log_analytics_resource_group_name" {}
variable "log_analytics_workspace_name" {}

variable "app_service_plan" {
  type = object({
    sku = string
  })
}

variable "dns" {
  type = object({
    subscription_id     = string
    resource_group_name = string
    domain              = string
    subdomain           = string
  })
}

variable "tenant_domain" {
  description = "Primary Microsoft Entra domain (for example molyneux.io)."
  type        = string
  default     = "molyneux.io"
}

variable "tags" {
  default = {}
}
