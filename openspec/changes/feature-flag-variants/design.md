## Context

The API already stores flags/filters in SQL (`FeatureFlags` schema), evaluates via a custom `IFeatureDefinitionProvider`, exposes admin CRUD and `/enabled`, and refreshes caches via store version polling. Variants were deferred in the archived change. See `proposal.md` for motivation. Official Microsoft Feature Management supports `FeatureDefinition.Variants` / `Allocation` and `IVariantFeatureManager.GetVariantAsync`.

## Goals / Non-Goals

**Goals:**

- Persist and map official variants + full allocation (defaults, user, group, percentile, seed).
- Resolve variants through Feature Management (not a hand-rolled allocator).
- POC targeting context from HTTP headers; public `/variant` probe; admin round-trip of variants/allocation.
- Preserve existing filter/enablement behavior and version-bump invalidation.

**Non-Goals:**

- Auth; App Configuration; multi-tenant; admin UI; telemetry productization.

## Decisions

### D0: Library fidelity

**Choice:** Map as closely as practical to Microsoft Feature Management CLR/schema types (`VariantDefinition`, `Allocation`, `UserAllocation`, `GroupAllocation`, `PercentileAllocation`, `StatusOverride`). Percentile bounds follow the library (`From` inclusive, `To` exclusive). Include `status_override` on variants. Do not reimplement allocation order—delegate to `IVariantFeatureManager`.

### D1: Relational variants + structured allocation tables

**Choice:**

- `FeatureVariants`: FeatureFlagId, Name (unique per feature), ConfigurationJson, StatusOverride (`None`/`Enabled`/`Disabled`)
- Allocation on feature or child tables:
  - Columns on `FeatureFlags`: `DefaultWhenEnabled`, `DefaultWhenDisabled`, `AllocationSeed`
  - `FeatureAllocationUsers`: FeatureFlagId, VariantName, UserId
  - `FeatureAllocationGroups`: FeatureFlagId, VariantName, GroupName
  - `FeatureAllocationPercentiles`: FeatureFlagId, VariantName, From, To

**Why:** Matches admin validation (referential variant names) and maps cleanly to `VariantDefinition` / `Allocation` CLR types. Avoids a single opaque AllocationJson blob that is harder to validate.

**Alternatives considered:** Single `AllocationJson` column (faster to ship, weaker validation); store entire Microsoft feature flag JSON document per row (flexible, duplicates filter model).

### D2: Map configuration_value via IConfiguration

**Choice:** Persist the raw JSON token for `configuration_value`. When mapping, wrap as `{ "configuration_value": <token> }` and set `VariantDefinition.ConfigurationValue` to that section so scalars and objects match library binding.

**Why:** Matches Feature Management’s `IConfigurationSection` expectation for variant configuration.

### D3: Targeting for allocation

**Choice:** Call `.AddFeatureManagement()...WithTargeting()`. Replace empty accessor with one that reads:

- `X-User-Id` → `TargetingContext.UserId`
- `X-Groups` → comma-separated → `Groups`

**Why:** Required for user/group/percentile allocation per docs. Header-based context is enough for POC without auth.

### D4: HTTP surface

**Choice:**

- Extend feature DTOs with `variants` (including `statusOverride`) + `allocation`
- `GET /api/features/{name}/variant` → `{ name, variant, value }` via `IVariantFeatureManager.GetVariantAsync`
- Keep `/enabled` unchanged (status override affects enablement when variants are assigned)

**Why:** Mirrors enablement probe; uses official assignment path.

### D5: Validation

**Choice:** On write, require every allocation reference (`default_*`, user/group/percentile variant names) to exist in the submitted variants list; percentile ranges with `from < to` in `0–100` (library: from inclusive, to exclusive); `statusOverride` in `{ None, Enabled, Disabled }`; configuration value any valid JSON token.

### D6: Cache

**Choice:** Continue caching full `FeatureDefinition` (now including Variants/Allocation) under the same cache key; admin writes invalidate + bump `StoreVersion` as today. Always map Variants/Allocation even when the flag is disabled so `default_when_disabled` works.

## Risks / Trade-offs

- **[Risk] Incorrect mapping of Allocation CLR types** → Mitigation: integration tests for user/group/percentile/default paths against documented order.
- **[Risk] Scalar vs object configuration_value binding quirks** → Mitigation: cover string and object values in tests; document probe JSON shape.
- **[Risk] Empty targeting context makes percentile sticky/surprising** → Mitigation: document headers; tests with and without user id.
- **[Trade-off] More tables vs JSON blob** → Prefer tables for validation clarity in POC admin API.

## Migration Plan

1. Add EF migration for variant/allocation tables and new columns (schema `FeatureFlags`).
2. Deploy API; existing flags remain valid with empty variants/allocation.
3. Rollback: reverse migration after API rollback; or leave nullable new tables unused.
