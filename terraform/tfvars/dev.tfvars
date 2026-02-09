workload    = "travel-itinerary"
environment = "dev"
location    = "swedencentral"

subscription_id = "d68448b0-9947-46d7-8771-baa331a3063a"

log_analytics_subscription_id     = "d68448b0-9947-46d7-8771-baa331a3063a"
log_analytics_resource_group_name = "rg-platform-logging-prd-uksouth-01"
log_analytics_workspace_name      = "log-platform-prd-uksouth-01"

platform_hosting_state = {
  resource_group_name  = "rg-tf-platform-hosting-dev-uksouth-01"
  storage_account_name = "saa3efe8753ccf"
  container_name       = "tfstate"
  key                  = "terraform.tfstate"
  subscription_id      = "7760848c-794d-4a19-8cb2-52f71a21ac2b"
  tenant_id            = "e56a6947-bb9a-4a6e-846a-1f118d1c3a14"
  use_oidc             = true
}

dns = {
  subscription_id     = "db34f572-8b71-40d6-8f99-f29a27612144"
  resource_group_name = "rg-platform-dns-prd-uksouth-01"
  domain              = "molyneux.me"
  subdomain           = "travelplans.dev"
}

tenant_domain = "molyneux.io"

tags = {
  Environment = "dev"
  Workload    = "travel-itinerary"
  DeployedBy  = "GitHub-Terraform"
  Git         = "https://github.com/frasermolyneux/travel-itinerary"
}
