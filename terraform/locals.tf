locals {
  resource_group_name   = "rg-${var.workload}-${var.environment}-${var.location}"
  app_service_plan_name = "asp-${var.workload}-${var.environment}-${var.location}"
  web_app_name          = "app-${var.workload}-${var.environment}-${var.location}-${random_id.environment_id.hex}"
  app_insights_name     = "ai-${var.workload}-${var.environment}-${var.location}"
}
