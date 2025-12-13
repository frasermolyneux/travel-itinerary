resource "azurerm_service_plan" "sp" {
  name                = local.app_service_plan_name
  location            = data.azurerm_resource_group.rg.location
  resource_group_name = data.azurerm_resource_group.rg.name

  os_type  = "Windows"
  sku_name = var.app_service_plan.sku
}
