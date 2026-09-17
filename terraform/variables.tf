variable "workload" {
  default = "travel-itinerary"
}

variable "environment" {
  default = "dev"

  validation {
    condition     = contains(["dev", "prd"], var.environment)
    error_message = "environment must be either dev or prd."
  }
}

variable "location" {
  default = "uksouth"
}

variable "subscription_id" {}

variable "platform_monitoring_state" {
  description = "Backend config for platform-monitoring remote state"
  type = object({
    resource_group_name  = string
    storage_account_name = string
    container_name       = string
    key                  = string
    subscription_id      = string
    tenant_id            = string
  })
}

variable "platform_hosting_state" {
  description = "Backend config for the production platform-hosting remote state (shared app service plan)."
  type = object({
    resource_group_name  = string
    storage_account_name = string
    container_name       = string
    key                  = string
    subscription_id      = string
    tenant_id            = string
    use_oidc             = bool
  })
  default  = null
  nullable = true

  validation {
    condition     = var.environment != "prd" || var.platform_hosting_state != null
    error_message = "platform_hosting_state must be set for production."
  }
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

variable "gcp_project_id" {
  description = "GCP project ID for Google Maps API key management"
  type        = string
}

variable "google_maps_allowed_referrers" {
  description = "Allowed HTTP referrer domains for the Google Maps API key"
  type        = list(string)
}
