# CLAUDE.md — TravelMap

## Commands

```bash
dotnet build
dotnet run          # http://localhost:5020
dotnet test
dotnet publish -c Release
```

**Local secrets setup** (Google OAuth from https://console.cloud.google.com/apis/credentials):

```bash
dotnet user-secrets set "Google:ClientId" "YOUR_CLIENT_ID"
dotnet user-secrets set "Google:ClientSecret" "YOUR_CLIENT_SECRET"
dotnet user-secrets set "Storage:BlobEndpoint" "https://<account>.blob.core.windows.net/"
```

Without `Storage:BlobEndpoint`, data falls back to local files in `App_Data/` — fine for dev.

## Workflow

1. Make changes → `git commit` → `git push` (`origin/main`)
2. Run `/Users/mariuszpiatkowski/ClaudeCode/deploy-travelmap.sh` to ship to Azure.

**DO NOT deploy without being asked.**

## Architecture

ASP.NET Core 8 MVC. Full-page Leaflet.js world map for tracking visited countries. Google OAuth for auth; per-user JSON persisted to Azure Blob Storage (keyed by hashed email).

**Azure deployment**: https://mytravelmap.azurewebsites.net

### Key files

- **`Controllers/HomeController.cs`** — serves `Home/Index.cshtml` (the map page)
- **`Controllers/AccountController.cs`** — Google OAuth login (`GET /Account/Login`) and logout (`POST /Account/Logout`)
- **`Controllers/VisitsController.cs`** — REST API at `api/visits`: `GET` (load), `POST` (upsert), `DELETE /{countryCode}`. All `[Authorize]`. User identity = `ClaimTypes.Email` from Google.
- **`Services/TravelDataService.cs`** / **`Services/ITravelDataService.cs`** — loads/saves `TravelData` JSON. In prod: Azure Blob Storage (`DefaultAzureCredential`). In dev (no `BlobEndpoint`): local file in `App_Data/`.
- **`Services/BlobContainerInitializer.cs`** — `IHostedService`, creates the blob container on startup.
- **`Models/TravelData.cs`** — `TravelData` (UserEmail, Visits, LastModified) + `CountryVisit` (CountryCode ISO alpha-3, CountryName, VisitType, FirstVisited, LastVisited, Notes) + `VisitType` enum (Mainland/Islands/Both)
- **`Settings/TravelDataSettings.cs`** — `BlobEndpoint` (note: `Settings/` folder, not `Configuration/`)
- **`Constants.cs`** — `BlobContainerName = "travel-data"`
- **`ServiceCollectionExtensions.cs`** — DI: `TravelDataSettings`, `ITravelDataService`, `BlobContainerInitializer`
- **`wwwroot/js/travelmap.js`** — all map logic, GeoJSON layer, click handlers, API calls
- **`wwwroot/data/countries.geojson`** — Natural Earth 110m polygons (~275 KB, pre-optimised — don't replace)
- **`wwwroot/css/site.css`** — map styling, dark/light theme

### Blob storage

Blob container: `travel-data`. One blob per user, named `<first16chars-of-sha256(lowercase-email)>.json`. This keeps emails out of blob names while remaining deterministic.

### Auth model

Google OAuth via `Microsoft.AspNetCore.Authentication.Google`. After login, `ClaimTypes.Email` is the user identifier used for all storage operations. No Strava involved.

## External integrations

- **Google OAuth** — `Microsoft.AspNetCore.Authentication.Google` NuGet
- **Azure** — Blob Storage, Application Insights, Key Vault, Managed Identity
- **Leaflet.js** (CDN) — interactive world map
- **CartoDB tiles** — dark/light base map tiles

## Debugging

- For web issues: check network tab / actual HTTP responses before assuming application logic bugs
- For backend errors: check App Insights or stream live logs (`az webapp log tail`) before assuming code bugs

## UI / Styling

- Always test dark mode AND light mode after any CSS/UI changes. Check text visibility, table styling, and chart colors in both themes.

## Tests

`TravelMap.Tests/` — NUnit + Moq.
- `TravelDataServiceTests.cs` — blob load/save/upsert/delete logic
- `VisitsControllerTests.cs` — API controller behaviour
