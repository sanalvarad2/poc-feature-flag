## 1. Persistence

- [x] 1.1 Add EF entities/columns for variants (including status override) and allocation (defaults, seed, users, groups, percentiles) under schema `FeatureFlags` and verify a new migration builds
- [x] 1.2 Wire relationships and unique constraints (variant name per feature; allocation rows reference feature) and verify `dotnet ef migrations add` / build succeeds

## 2. Mapping and targeting

- [x] 2.1 Extend `FeatureDefinitionMapper` to populate `Variants` (configuration + status override) and `Allocation` from store rows and verify unit tests for defaults, user, group, percentile, and status override shapes
- [x] 2.2 Load variants/allocation in `SqlFeatureDefinitionProvider` queries (including when flag disabled) and verify cached definitions include them after invalidate
- [x] 2.3 Replace empty targeting accessor with header-based `X-User-Id` / `X-Groups`, register `WithTargeting()`, and verify allocation uses that context in a test

## 3. Admin API

- [x] 3.1 Extend request/response DTOs for variants (incl. statusOverride) and allocation and verify OpenAPI/Swagger shows the new fields
- [x] 3.2 Persist variants/allocation on create/update (same transaction + store version bump) and verify get/list round-trip
- [x] 3.3 Reject allocation that references missing variant names (and invalid percentile ranges / status override) and verify 400 with no version bump

## 4. Variant probe and evaluation

- [x] 4.1 Add `GET /api/features/{name}/variant` using `IVariantFeatureManager.GetVariantAsync` and verify response includes variant name and configuration value
- [x] 4.2 Integration tests covering default_when_enabled/disabled, user, group, percentile assignment order, and status override and verify they pass
- [x] 4.3 Confirm existing `/enabled` and filter behavior still pass and verify the prior test suite remains green

## 5. Docs

- [x] 5.1 Document variant probe, allocation headers, status override, and example payload in README and verify a reader can exercise Swagger end-to-end from the doc
