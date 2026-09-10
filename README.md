# FeatureFlags

POC ASP.NET Core (.NET 10) Web API that stores feature flags in SQL Server and evaluates them with the official [`Microsoft.FeatureManagement`](https://learn.microsoft.com/en-us/azure/azure-app-configuration/feature-management-dotnet-reference) library via a custom `IFeatureDefinitionProvider`, including **variants** and full **allocation**.

## Warning (POC)

There is **no authentication or authorization**. Admin CRUD and probes are open. Do not expose this API on untrusted networks.

## Prerequisites

- .NET 10 SDK
- SQL Server LocalDB (default connection) or another SQL Server instance

## Configure

Connection string and poll interval live in `src/FeatureFlags.Api/appsettings.json` (override in `appsettings.Development.json`).

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
| `GET` | `/api/features/{name}/variant` | Public probe: assigned variant + configuration value |
| `GET` | `/api/featurex` | Demo controller gated by `[FeatureGate("FeatureX")]` (404 if disabled/missing) |

### Targeting headers (variant allocation)

| Header | Maps to |
|--------|---------|
| `X-User-Id` | `TargetingContext.UserId` |
| `X-Groups` | Comma-separated group names |

Allocation order follows the library: **user → group → percentile → default_when_enabled** (or `default_when_disabled` when the feature is off). Percentile `from` is inclusive and `to` is exclusive.

### Create body with variants

```json
{
  "enabled": true,
  "requirementType": "Any",
  "filters": [],
  "variants": [
    {
      "name": "Small",
      "configurationValue": { "Size": 300 },
      "statusOverride": "None"
    },
    {
      "name": "Big",
      "configurationValue": { "Size": 500 },
      "statusOverride": "None"
    }
  ],
  "allocation": {
    "defaultWhenEnabled": "Small",
    "defaultWhenDisabled": "Small",
    "seed": "layout-seed",
    "user": [{ "variant": "Big", "users": ["alice"] }],
    "group": [{ "variant": "Big", "groups": ["Ring1"] }],
    "percentile": [{ "variant": "Big", "from": 0, "to": 10 }]
  }
}
```

`statusOverride`: `None` | `Enabled` | `Disabled` (library `StatusOverride`).

### Probe examples

```json
{ "name": "CheckoutV2", "enabled": true }
```

```json
{ "name": "Layout", "variant": "Small", "value": { "Size": 300 } }
```

Unknown feature on `/enabled` → `{ "enabled": false }`. Unknown on `/variant` → `404`.

## Multi-instance refresh

Each successful admin write increments `FeatureStoreMeta.StoreVersion`. Every instance caches definitions in memory and runs `FeatureStoreVersionWatcher`, which polls that version (default every 2 seconds) and clears the cache when it changes.

## Test

```bash
dotnet test FeatureFlags.slnx
```

## OpenSpec

- Active: `openspec/changes/feature-flag-variants/`
- Archived: `openspec/changes/archive/2026-09-10-sql-feature-flags-webapi/`
