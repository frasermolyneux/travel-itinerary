resource "azurerm_storage_account" "data" {
  name                            = local.storage_account_name
  resource_group_name             = data.azurerm_resource_group.rg.name
  location                        = data.azurerm_resource_group.rg.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  tags                            = var.tags
}

resource "azurerm_storage_table" "tables" {
  for_each             = local.storage_table_names
  name                 = each.value
  storage_account_name = azurerm_storage_account.data.name
}

resource "azurerm_role_assignment" "web_table_data_contributor" {
  scope                = azurerm_storage_account.data.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = azurerm_linux_web_app.app.identity[0].principal_id

  depends_on = [
    azurerm_linux_web_app.app
  ]
}
