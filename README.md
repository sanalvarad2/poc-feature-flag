# FeatureFlags

POC ASP.NET Core (.NET 10) Web API that stores feature flags in SQL Server and evaluates them with the official [`Microsoft.FeatureManagement`](https://learn.microsoft.com/en-us/azure/azure-app-configuration/feature-management-dotnet-reference) library via a custom `IFeatureDefinitionProvider`.

## Warning (POC)

There is **no authentication or authorization**. Admin CRUD and the enablement probe are open. Do not expose this API on untrusted networks.

## Prerequisites

- .NET 10 SDK
- SQL Server LocalDB (default connection) or another SQL Server instance

## Configure

Connection string and poll interval live in `src/FeatureFlags.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "FeatureStore": "Server=(localdb)\\mssqllocaldb;Database=FeatureFlags;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "FeatureStore": {
    "VersionPollIntervalSeconds": 2
  }
}
```

On startup the API **creates the SQL Server database if it does not exist**, then applies EF migrations. For tests, SQLite is used via `FeatureStore:DatabaseProvider=Sqlite` (`EnsureCreated`).

## Run

```bash
dotnet run --project src/FeatureFlags.Api
```

Default URL: `http://localhost:5043`

Swagger UI: [http://localhost:5043/swagger](http://localhost:5043/swagger)

## HTTP API

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/features` | List features |
| `GET` | `/api/features/{name}` | Get feature definition |
| `POST` | `/api/features/{name}` | Create feature |
| `PUT` | `/api/features/{name}` | Update feature |
| `DELETE` | `/api/features/{name}` | Delete feature |
| `GET` | `/api/features/{name}/enabled` | Public probe: is the feature enabled? |
| `GET` | `/api/featurex` | Demo controller gated by `[FeatureGate("FeatureX")]` (404 if disabled/missing) |

### Create body example

```json
{
  "enabled": true,
  "requirementType": "Any",
  "filters": [
    {
      "name": "Microsoft.Percentage",
      "parameters": { "Value": 50 }
    }
  ]
}
```

### Probe response example

```json
{ "name": "CheckoutV2", "enabled": true }
```

Unknown feature names return `{ "enabled": false }`.

See `src/FeatureFlags.Api/FeatureFlags.Api.http` for sample requests.

## Multi-instance refresh

Each successful admin write increments `FeatureStoreMeta.StoreVersion`. Every instance caches definitions in memory and runs `FeatureStoreVersionWatcher`, which polls that version (default every 2 seconds) and clears the cache when it changes. The writing instance also invalidates its local cache immediately.

## Test

```bash
dotnet test FeatureFlags.slnx
```

## OpenSpec

Planning artifacts for this work: `openspec/changes/sql-feature-flags-webapi/`.
