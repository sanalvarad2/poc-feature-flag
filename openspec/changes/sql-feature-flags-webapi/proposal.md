## Why

Teams need feature flags evaluated with the official ASP.NET Core Feature Management library, while owning definitions in SQL Server (create, update, disable) without Azure App Configuration. This change establishes a .NET 10 Web API POC that both administers flags in MSSQL and evaluates them consistently across multiple app instances.

## What Changes

- Greenfield ASP.NET Core Web API on .NET 10 using `Microsoft.FeatureManagement` / `Microsoft.FeatureManagement.AspNetCore`.
- Feature definitions persisted in SQL Server and loaded via a custom `IFeatureDefinitionProvider` (not `IConfiguration` / App Configuration).
- Unauthenticated admin HTTP API (POC) to create, read, update, and delete system-wide feature flags and their filters.
- Public HTTP evaluation endpoint: caller passes a feature name and receives whether it is currently enabled.
- Support for rich flag conditions using official filters (e.g. Percentage, TimeWindow, Targeting) plus always-on/off.
- In-process cache of definitions with cross-instance refresh via a SQL store version stamp and background polling (no Redis / message bus required for MVP).

## Capabilities

### New Capabilities

- `feature-evaluation`: Load feature definitions from SQL Server, map them to `FeatureDefinition`, evaluate enablement through the official Feature Management stack (including built-in filters), expose a public HTTP probe by feature name, and refresh cached definitions across multiple instances when the store version changes.
- `feature-administration`: Unauthenticated CRUD API (POC) for system-wide feature flags and filter configurations persisted in SQL Server, bumping the store version on every successful write.

### Modified Capabilities

- (none — greenfield)

## Impact

- New solution/projects under this repo (API + persistence); no existing application code to migrate.
- Dependencies: ASP.NET Core 10, `Microsoft.FeatureManagement.AspNetCore`, SQL Server access (EF Core or equivalent). No auth packages required for this POC.
- Out of scope for this change: authentication/authorization, multi-tenant flags, Azure App Configuration, feature variants/allocation, dedicated admin UI, Redis-based invalidation.
