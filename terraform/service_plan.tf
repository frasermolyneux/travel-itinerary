resource "azurerm_service_plan" "default" {
  count = var.environment == "dev" ? 1 : 0

  name                = "asp-${var.workload}-${var.environment}-${var.location}-default"
  resource_group_name = data.azurerm_resource_group.rg.name
  location            = data.azurerm_resource_group.rg.location
  os_type             = "Linux"
  sku_name            = "B1"

  tags = var.tags
}
