# CLAUDE.md — TravelMap

## Commands

```bash
dotnet build
dotnet run          # http://localhost:5010
dotnet publish -c Release
```

**Local secrets setup** (Google OAuth from https://console.cloud.google.com/apis/credentials):

```bash
dotnet user-secrets set "Google:ClientId" "YOUR_CLIENT_ID"
dotnet user-secrets set "Google:ClientSecret" "YOUR_CLIENT_SECRET"
```

## Workflow

1. Make changes → `git commit` → `git push` (`origin/main`)
2. Run deploy script (`deploy-travelmap.sh`) to ship to Azure.

**DO NOT deploy without being asked.**

## Architecture

ASP.NET Core 8 MVC app with full-page Leaflet.js world map for tracking visited countries. Google OAuth for authentication, Azure Blob Storage for per-user JSON data.

**Azure deployment**: https://mytravelmap.azurewebsites.net

### Key layers

- **Controllers/** — `HomeController` serves the map page, `AccountController` handles Google login/logout, `VisitsController` is the REST API for country visits CRUD.
- **Services/** — `TravelDataService` handles blob storage persistence (Azure Blob in prod, local file in dev). Follows the GoalsService pattern from ActivitiesJournal.
- **Models/** — `TravelData` with `CountryVisit` and `VisitType` enum (Mainland/Islands/Both).
- **Views/** — Single page app: `Home/Index.cshtml` is a full-page Leaflet map.
- **wwwroot/js/travelmap.js** — All map logic, click handlers, API calls.
- **wwwroot/data/countries.geojson** — Natural Earth 110m, optimized (~275KB).

### External integrations

- **Google OAuth** — `Microsoft.AspNetCore.Authentication.Google` NuGet
- **Azure** — Storage Blobs, Application Insights, Key Vault, Managed Identity
- **Leaflet.js** — Interactive world map with GeoJSON country polygons
- **CartoDB tiles** — Dark/light base map tiles
