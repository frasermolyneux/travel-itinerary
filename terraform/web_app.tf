resource "azurerm_linux_web_app" "app" {
  name = local.web_app_name
  tags = var.tags

  resource_group_name = data.azurerm_resource_group.rg.name
  location            = data.azurerm_resource_group.rg.location

  service_plan_id = azurerm_service_plan.sp.id

  https_only = true

  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_stack {
      dotnet_version = "9.0"
    }

    always_on           = true
    ftps_state          = "Disabled"
    minimum_tls_version = "1.2"
  }

  app_settings = {
    "APPLICATIONINSIGHTS_CONNECTION_STRING"      = azurerm_application_insights.ai.connection_string
    "ApplicationInsightsAgent_EXTENSION_VERSION" = "~3"
    "ASPNETCORE_ENVIRONMENT"                     = var.environment == "prd" ? "Production" : "Development"
    "WEBSITE_RUN_FROM_PACKAGE"                   = "1"
    "AzureAd__Instance"                          = "https://login.microsoftonline.com/"
    "AzureAd__Domain"                            = var.tenant_domain
    "AzureAd__TenantId"                          = data.azuread_client_config.current.tenant_id
    "AzureAd__ClientId"                          = azuread_application.web.application_id
    "AzureAd__ClientSecret"                      = azuread_application_password.web.value
    "AzureAd__CallbackPath"                      = "/signin-oidc"
    "Storage__AccountName"                       = azurerm_storage_account.data.name
    "Storage__TableEndpoint"                     = azurerm_storage_account.data.primary_table_endpoint
    "Storage__Tables__Trips"                     = azurerm_storage_table.tables["trips"].name
    "Storage__Tables__ItineraryEntries"          = azurerm_storage_table.tables["itinerary_entries"].name
    "Storage__Tables__Bookings"                  = azurerm_storage_table.tables["bookings"].name
    "Storage__Tables__ShareLinks"                = azurerm_storage_table.tables["share_links"].name
  }
}

resource "azurerm_app_service_custom_hostname_binding" "primary" {
  hostname            = local.public_hostname
  app_service_name    = azurerm_linux_web_app.app.name
  resource_group_name = data.azurerm_resource_group.rg.name

  depends_on = [
    azurerm_dns_txt_record.app_service_verification,
    azurerm_dns_cname_record.web_app
  ]
}

resource "time_sleep" "wait_for_hostname_binding" {
  create_duration = "60s"

  depends_on = [
    azurerm_app_service_custom_hostname_binding.primary
  ]
}

resource "azurerm_app_service_managed_certificate" "primary" {
  custom_hostname_binding_id = azurerm_app_service_custom_hostname_binding.primary.id

  depends_on = [
    time_sleep.wait_for_hostname_binding,
    azurerm_dns_cname_record.web_app
  ]
}

resource "azurerm_app_service_certificate_binding" "primary" {
  hostname_binding_id = azurerm_app_service_custom_hostname_binding.primary.id
  certificate_id      = azurerm_app_service_managed_certificate.primary.id
  ssl_state           = "SniEnabled"
}
