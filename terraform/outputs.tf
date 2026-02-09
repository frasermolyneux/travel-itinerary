output "resource_group_name" {
  value = data.azurerm_resource_group.rg.name
}

output "web_app_name" {
  value = azurerm_linux_web_app.app.name
}

output "web_app_resource_group_name" {
  value = azurerm_linux_web_app.app.resource_group_name
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

output "storage_account_name" {
  value = azurerm_storage_account.data.name
}

output "storage_table_names" {
  value = local.storage_table_names
}
