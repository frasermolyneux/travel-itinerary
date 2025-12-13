output "resource_group_name" {
  value = data.azurerm_resource_group.rg.name
}

output "web_app_name" {
  value = azurerm_windows_web_app.app.name
}

output "entra_application_client_id" {
  value = azuread_application.web.application_id
}

output "entra_service_principal_object_id" {
  value = azuread_service_principal.web.object_id
}

output "entra_application_client_secret" {
  value     = azuread_application_password.web.value
  sensitive = true
}

output "entra_redirect_uris" {
  value = local.entra_redirect_uris
}
