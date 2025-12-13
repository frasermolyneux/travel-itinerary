locals {
  resource_group_name    = "rg-${var.workload}-${var.environment}-${var.location}"
  app_service_plan_name  = "asp-${var.workload}-${var.environment}-${var.location}"
  web_app_name           = "app-${var.workload}-${var.environment}-${var.location}-${random_id.environment_id.hex}"
  app_insights_name      = "ai-${var.workload}-${var.environment}-${var.location}"
  public_hostname        = "${var.dns.subdomain}.${var.dns.domain}"
  entra_app_display_name = "${var.workload}-${var.environment}-web"
  entra_redirect_uris = distinct([
    "https://${local.public_hostname}/signin-oidc",
    "https://${local.web_app_name}.azurewebsites.net/signin-oidc",
    "https://localhost:5001/signin-oidc"
  ])
  entra_logout_url = "https://${local.public_hostname}/signout-callback-oidc"
}
