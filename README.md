# travel-itinerary
A simple ASP.NET Core app hosted on Azure for sharing travel itineraries with my travel companions.

## Authentication & Authorization
- Microsoft Entra ID (single-tenant) secures the site. Terraform now provisions the app registration, service principal, and client secret.
- User assignment is required on the Enterprise Application, so only explicitly assigned tenant users can reach authenticated pages.
- The landing page (`/`) is intentionally anonymous so itineraries can expose publicly shareable context before requesting sign-in.

### Terraform outputs
After applying the infrastructure, capture the identity values for other environments:

```bash
terraform output -raw entra_application_client_id
terraform output -raw entra_application_client_secret
terraform output entra_redirect_uris
```

Add or rotate user assignments in the Microsoft Entra portal (`Enterprise applications > <app-name>`).

### Local development secrets
Configure `dotnet user-secrets` (or environment variables) with the values from Terraform:

```bash
cd src/MX.TravelItinerary.Web
dotnet user-secrets set "AzureAd:TenantId" "<tenant-guid>"
dotnet user-secrets set "AzureAd:ClientId" "<entra_application_client_id>"
dotnet user-secrets set "AzureAd:ClientSecret" "<entra_application_client_secret>"
dotnet user-secrets set "AzureAd:Domain" "molyneux.io"
```

> Tip: `az account show --query tenantId -o tsv` returns the tenant GUID if you need to confirm it locally.

The default callback path is `/signin-oidc`. Ensure your local HTTPS profile (from `launchSettings.json`) is included in the Terraform-managed redirect URIs if you customize ports.
