## 1. Solution scaffold

- [ ] 1.1 Create ASP.NET Core Web API project targeting .NET 10 and verify `dotnet build` succeeds
- [ ] 1.2 Add `Microsoft.FeatureManagement.AspNetCore` and EF Core SQL Server packages and verify restore/build succeeds
- [ ] 1.3 Add configuration for SQL connection string (no auth settings) and verify the app starts against a configured connection

## 2. Persistence

- [ ] 2.1 Implement EF entities for `FeatureFlags`, `FeatureFilters`, and `FeatureStoreMeta` and verify a migration creates the tables
- [ ] 2.2 Seed `FeatureStoreMeta` with `StoreVersion = 0` on migrate/startup and verify the row exists
- [ ] 2.3 Implement repository/unit-of-work helpers that update flags and increment `StoreVersion` in the same transaction; verify a write bumps the version and a failed validation does not

## 3. Feature evaluation provider

- [ ] 3.1 Implement `SqlFeatureDefinitionProvider` mapping store rows to `FeatureDefinition` (disabled / AlwaysOn / filters + requirement type) and verify unit tests cover the mapping cases
- [ ] 3.2 Register the provider before `AddFeatureManagement`, enable Percentage/TimeWindow/Targeting filters, and verify `IFeatureManager.IsEnabledAsync` reflects seeded SQL data
- [ ] 3.3 Add in-memory caching in the provider (or cache service) with local invalidation on write and verify repeated evaluations do not hit SQL every time while still seeing local writes immediately

## 4. Multi-instance refresh

- [ ] 4.1 Implement `FeatureStoreVersionWatcher` background service that polls `StoreVersion` and clears the definition cache on bump; verify a simulated version change clears cache
- [ ] 4.2 Integration-style check: write via admin path on one logical flow, advance/observe version, confirm peer cache path reloads; verify evaluations match updated definition within the configured poll window

## 5. Admin API

- [ ] 5.1 Implement unauthenticated list/get endpoints for features (including filters) and verify 404 for missing names with no auth challenge
- [ ] 5.2 Implement create/update with validation (unique name, known filter parameter shapes) and verify conflict on duplicate name plus successful round-trip of Percentage/TimeWindow/Targeting payloads
- [ ] 5.3 Implement delete and verify the feature disappears from list/get and evaluates as disabled/unknown afterward
- [ ] 5.4 Ensure every successful create/update/delete increments store version and invalidates local cache; verify with assertions on version and post-write evaluation

## 6. Public enablement probe and hardening

- [ ] 6.1 Implement public `GET` enablement probe by feature name (e.g. `/api/features/{name}/enabled`) using `IFeatureManager` and verify enabled, disabled, and unknown names return the correct enabled flag without requiring credentials
- [ ] 6.2 Add integration tests covering admin CRUD + probe + version bump behavior from the specs and verify the test project passes
- [ ] 6.3 Document runbook (connection string, probe URL, poll interval, POC no-auth warning) in project README and verify a clean clone can follow it to run locally
