resource "azuread_application" "web" {
  display_name     = local.entra_app_display_name
  description      = "Travel Itinerary web front-end"
  sign_in_audience = "AzureADMyOrg"

  web {
    homepage_url  = "https://${local.public_hostname}/"
    logout_url    = local.entra_logout_url
    redirect_uris = local.entra_redirect_uris

    implicit_grant {
      access_token_issuance_enabled = false
      id_token_issuance_enabled     = false
    }
  }

  prevent_duplicate_names = true
}

resource "azuread_service_principal" "web" {
  client_id                    = azuread_application.web.client_id
  app_role_assignment_required = true

  owners = [
    data.azuread_client_config.current.object_id
  ]
}

resource "azuread_application_password" "web" {
  application_id = azuread_application.web.application_id

  rotate_when_changed = {
    rotation = time_rotating.thirty_days.id
  }
}
