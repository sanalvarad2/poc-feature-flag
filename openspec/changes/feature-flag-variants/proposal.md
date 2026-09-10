## Why

Feature flags today only answer on/off (plus filters). Product and experiment needs require returning arbitrary configuration through the official Microsoft Feature Management variant model, including full allocation (user, group, percentile, defaults, seed). This change extends the SQL-backed store and API so callers can resolve a variant and bind its configuration like the documented `IVariantFeatureManager` flow.

## What Changes

- Persist feature **variants** (`name` + `configuration_value` JSON) and full **allocation** in SQL Server alongside existing flags/filters.
- Map variants and allocation onto `FeatureDefinition` in `SqlFeatureDefinitionProvider` so evaluation uses the official Feature Management stack.
- Enable targeting for allocation (`WithTargeting` + request-derived targeting context for the POC).
- Extend the admin HTTP API to create/read/update variants and allocation with feature definitions.
- Add a public HTTP probe that returns the assigned variant name and configuration value for a feature (unknown/disabled behavior aligned with Feature Management defaults).
- Keep existing enablement probe and filter behavior; writes continue to bump store version and invalidate caches.

## Capabilities

### New Capabilities

- (none)

### Modified Capabilities

- `feature-evaluation`: Add variant resolution via official Feature Management (including full allocation and arbitrary configuration values) and a public variant probe.
- `feature-administration`: Add administration of variants and full allocation on system-wide features; version bump on those writes.

## Impact

- Code: EF entities/migrations, mapper/provider, admin DTOs/endpoints, targeting accessor, optional `FeatureX`-style demos, tests, README.
- Dependencies: existing `Microsoft.FeatureManagement.AspNetCore` (variants already in library); register targeting for allocation.
- Out of scope: authentication, Azure App Configuration, multi-tenant flags, dedicated admin UI, telemetry schema.
