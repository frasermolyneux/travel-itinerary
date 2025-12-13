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
  entra_logout_url       = "https://${local.public_hostname}/signout-callback-oidc"
  storage_account_prefix = substr(replace(var.workload, "-", ""), 0, 8)
  storage_account_name   = lower("st${local.storage_account_prefix}${var.environment}${random_id.storage.hex}")
  storage_table_names = {
    trips             = "Trips"
    trip_segments     = "TripSegments"
    itinerary_entries = "ItineraryEntries"
    bookings          = "Bookings"
    share_links       = "ShareLinks"
  }
}
