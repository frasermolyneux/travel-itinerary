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
terraform output -raw storage_account_name
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
dotnet user-secrets set "Storage:TableEndpoint" "https://<storage-account>.table.core.windows.net"
dotnet user-secrets set "GoogleMaps:ApiKey" "<google-maps-places-api-key>"
```

> Tip: `az account show --query tenantId -o tsv` returns the tenant GUID if you need to confirm it locally.

## Table Storage

Terraform now provisions a dedicated Storage Account plus Azure Table containers for the data model. The web app's system-assigned managed identity receives the `Storage Table Data Contributor` role, so the application code authenticates with managed identity (or `DefaultAzureCredential` locally) instead of connection strings.

- Tables: `Trips`, `ItineraryEntries`, `Bookings`, `ShareLinks`.
- Outputs: `storage_account_name` and `storage_table_names` expose deployment-time metadata.
- App configuration: the web app expects `Storage:TableEndpoint` (and optionally overrides for table names) via configuration. In Azure these values are injected through App Service settings; locally configure them with `dotnet user-secrets` or environment variables.

> Share links must store `OwnerUserId` (or `CreatedBy`) so the repository layer can enforce that consumers only see itineraries they own or have an explicit share code.

The default callback path is `/signin-oidc`. Ensure your local HTTPS profile (from `launchSettings.json`) is included in the Terraform-managed redirect URIs if you customize ports.

## Data Model

Azure Table Storage backs the travel itinerary experience. Each entity keeps a `PartitionKey` and `RowKey` tuned for the most common queries (per-user trip listings, and per-trip timelines) while allowing flexible columns for planned vs. confirmed items and share links.

Multi-day spans are expressed directly on itinerary entries (`IsMultiDay` + `EndDate`), so there is no longer a separate `TripSegments` table to maintain.

### Trips

| Column                  | Type / Example              | Notes                                               |
| ----------------------- | --------------------------- | --------------------------------------------------- |
| `PartitionKey`          | `UserId`                    | Groups trips per owner (user or household).         |
| `RowKey`                | `TripId` (GUID)             | Primary identifier.                                 |
| `Name`                  | `"India & Sri Lanka 2026"`  | Display title.                                      |
| `Slug`                  | `"india-sri-lanka-2026"`    | Friendly URL segment for the `/trips/<slug>` route. |
| `StartDate` / `EndDate` | `2026-01-15` / `2026-02-03` | Trip span.                                          |
| `HomeTimeZone`          | `"Europe/London"`           | Default time zone for itinerary rendering.          |
| `DefaultCurrency`       | `"GBP"`                     | Applied when an entry omits currency.               |

### ItineraryEntries

| Column                         | Type / Example                                                               | Notes                                                    |
| ------------------------------ | ---------------------------------------------------------------------------- | -------------------------------------------------------- |
| `PartitionKey`                 | `TripId`                                                                     | Scope per trip.                                          |
| `RowKey`                       | `EntryId` (GUID)                                                             | Identifier referenced by bookings and UI mutations.      |
| `Date`                         | `2026-01-18`                                                                 | Local start date for the entry.                          |
| `EndDate`                      | `2026-01-21`                                                                 | Optional inclusive end for multi-day spans.              |
| `IsMultiDay`                   | `true/false`                                                                 | Paints a timeline lane across `Date` → `EndDate`.        |
| `ItemType`                     | `travel`, `lodging`, `activity`, `dining`, `transportation`, `note`, `other` | Drives icons/colors on the timeline.                     |
| `Title`                        | `"Hotel Taj Resorts"`                                                        | Primary text.                                            |
| `Details`                      | Multiline text                                                               | Narrative for the slot (activities, instructions, etc.). |
| `GooglePlaceId`                | `ChIJ4dG0G4IJlQ0RjZ1qfG9aCBA`                                                | Optional link to Google Maps Places for map + address.   |
| `LocationName` / `LocationUrl` | Name + map link                                                              | Used for cards and Google Maps deep links.               |
| `Latitude` / `Longitude`       | `28.592` / `77.250`                                                          | Map markers.                                             |
| `Tags`                         | `"hotel,luxury"`                                                             | Lightweight filtering.                                   |
| `MetadataJson`                 | `{ "flight": { ... } }`                                                      | Category-specific metadata (flight or stay snapshots).   |

`MetadataJson` currently captures either `FlightMetadata` (airline, route, timing) or `StayMetadata` (property, room, check-in/out details). The JSON is optional and only emitted when the entry type supports that schema.

### Bookings

| Column                | Type / Example                                       | Notes                                                  |
| --------------------- | ---------------------------------------------------- | ------------------------------------------------------ |
| `PartitionKey`        | `TripId`                                             | Scoped per trip.                                       |
| `RowKey`              | `BookingId`                                          | GUID.                                                  |
| `EntryId`             | Reference to itinerary row                           | Maintains linkage back to the day card.                |
| `ItemType`            | `flight`, `hotel`, `tour`, `dining`, `note`, `other` | Snapshot of the linked itinerary category for UI cues. |
| `Vendor`              | `"British Airways"`                                  | Provider name.                                         |
| `Reference`           | `"YQ695R"`                                           | PNR/reservation number.                                |
| `Cost` / `Currency`   | `631.81` / `GBP`                                     | Actual spend.                                          |
| `IsRefundable`        | `true/false`                                         | Highlight cancellable bookings.                        |
| `IsPaid`              | `true/false`                                         | Whether the organiser already settled the bill.        |
| `CancellationPolicy`  | Text                                                 | Free-form summary.                                     |
| `ConfirmationDetails` | `{ "notes": "Call desk on arrival" }`                | Sensitive structured data (tickets, passengers, etc.). |
| `ConfirmationUrl`     | `https://manage.booking.com/reservation/123`         | Direct link back to the booking portal.                |

### ShareLinks

| Column                    | Type / Example                 | Notes                                                       |
| ------------------------- | ------------------------------ | ----------------------------------------------------------- |
| `PartitionKey`            | `TripId`                       | Multiple links per trip.                                    |
| `RowKey`                  | `ShareCode` (e.g. `HUHB3HU2H`) | Used in `/shares/<trip-slug>/<code>`.                       |
| `CreatedOn` / `CreatedBy` | timestamps + user id           | Audit info.                                                 |
| `ExpiresOn`               | nullable                       | Optional auto-expiry.                                       |
| `MaskBookings`            | `true/false`                   | Whether confirmations/costs should be hidden for this link. |
| `IncludeCost`             | `true/false`                   | Toggle budget visibility.                                   |
| `Notes`                   | `"Link for parents"`           | Organizer-only context.                                     |

## Sharing itineraries

- From the Trips dashboard use the **Share** action to open `/Trips/ShareLinks`. Every trip can own multiple share codes with their own expiry date, booking visibility, and notes.
- Share links surface a public, anonymous route at `/shares/{tripSlug}/{shareCode}`. The page reuses the same responsive timeline component but trims all editing affordances.
- `MaskBookings = true` removes booking cards entirely; `IncludeCost = false` keeps the linked bookings but strips currency, totals, and outbound confirmation links before rendering.
- The clipboard helper next to each share URL uses the browser Clipboard API, so the site must be served over HTTPS (or `localhost`) for one-click copy.
