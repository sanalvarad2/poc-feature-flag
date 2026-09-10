## Context

Greenfield POC (no existing API). Constraints: official `Microsoft.FeatureManagement` for evaluation; SQL Server as sole definition store; admin CRUD on the same host without auth; system-wide flags; public HTTP probe for enablement by name; multi-instance cache refresh without Redis or Azure App Configuration; filters in MVP, variants deferred. See `proposal.md` for motivation and capabilities.

## Goals / Non-Goals

**Goals:**

- Wire Feature Management so evaluation uses a SQL-backed `IFeatureDefinitionProvider`.
- Persist flags + filters in MSSQL with a monotonic store version for cross-instance refresh.
- Expose unauthenticated admin HTTP API and a public enablement probe; both use the same SQL-backed definitions.
- Keep the first cut deployable as a single ASP.NET Core 10 Web API.

**Non-Goals:**

- Authentication or authorization (POC only; not production-safe as-is).
- Feature variants / allocation UI or schema.
- Multi-tenant flag isolation.
- Azure App Configuration sync or dual-read.
- Dedicated admin SPA; Redis/Service Bus invalidation.
- Full audit history (who/when beyond basic timestamps is optional polish).

## Decisions

### D1: Custom `IFeatureDefinitionProvider` over custom `IConfiguration` provider

**Choice:** Implement `IFeatureDefinitionProvider` that reads SQL and maps rows to `FeatureDefinition` (`EnabledFor`, `RequirementType`, `Status`).

**Why:** Direct mapping, clearer admin write path, documented extension point. Avoids forcing the Microsoft JSON schema through `IConfiguration` reload semantics.

**Alternatives considered:** SQL → `IConfiguration` provider + default `ConfigurationFeatureDefinitionProvider` (more indirection; refresh awkward); store only JSON blobs identical to `feature_management` schema (flexible but weaker typed admin validation).

### D2: Relational rows + JSON filter parameters

**Choice:**

- `FeatureFlags`: Id, Name (unique), Enabled, RequirementType, UpdatedAt
- `FeatureFilters`: Id, FeatureId, Name, ParametersJson, sort/order
- `FeatureStoreMeta`: single-row StoreVersion (bigint)

**Why:** Names/enabled/requirement stay queryable; filter parameters match how Feature Management already binds filter config from unstructured sections.

**Alternatives considered:** Fully normalized parameter tables (heavy); one JSON document per feature only (harder listing/constraints).

### D3: Mapping rules to `FeatureDefinition`

| Store state | Runtime mapping |
|-------------|-----------------|
| `Enabled = false` | `Status = Disabled` (or empty `EnabledFor`) |
| `Enabled = true`, no filters | `EnabledFor = [ AlwaysOn ]` |
| `Enabled = true`, with filters | `EnabledFor` = filter list; `RequirementType` from row |
| Missing name | Provider returns null / absent → Feature Manager treats as disabled |

Register built-in filters (Percentage, TimeWindow, Targeting) via `AddFeatureManagement()` / AspNetCore package conventions.

### D4: Cache + SQL version polling for multi-instance

**Choice:** In-memory cache of definitions in the provider (or adjacent cache service). On successful admin write: persist + increment `StoreVersion` in the same transaction + invalidate local cache. `BackgroundService` polls `StoreVersion` every few seconds; on bump, clear cache.

**Why:** No extra infrastructure; eventual consistency within a bounded window; writer is immediately consistent.

**Alternatives considered:** Short TTL only (simpler but slower convergence and more SQL load); Redis pub/sub (better push, extra dependency); SqlDependency (fragile / limited on modern hosting).

### D5: Solution shape and HTTP surface

**Choice:** One Web API project using EF Core + SQL Server. Admin Minimal APIs or controllers under `/api/features` (CRUD, no auth). Public evaluation probe, e.g. `GET /api/features/{name}/enabled` returning whether the named feature is enabled (unknown → disabled), implemented via `IFeatureManager.IsEnabledAsync`.

**Auth:** None for this POC. Document that admin and probe are open and must not be exposed on untrusted networks without a later auth change.

### D6: Variants deferred

Variants/`Allocation` remain out of schema and API until a later change. Specs cover filters only.

## Risks / Trade-offs

- **[Risk] Open admin + evaluation endpoints on a POC** → Mitigation: document as local/dev only; add auth in a follow-up before any shared environment.
- **[Risk] Poll delay → brief stale reads on peer instances** → Mitigation: small poll interval (2–5s) + optional short TTL safety net; document freshness SLO.
- **[Risk] Invalid ParametersJson breaks filter evaluation** → Mitigation: validate known filter shapes on admin write; reject unknown required fields.
- **[Risk] Registering custom provider incorrectly still leaves Configuration provider active** → Mitigation: register SQL provider before `AddFeatureManagement` per library guidance; verify with integration tests.
- **[Trade-off] Single DB is both control plane and read path** → Acceptable for MVP; cache reduces load; scale-out later can add read replicas or push invalidation.

## Migration Plan

1. Create database schema (migrations) including seed `FeatureStoreMeta.StoreVersion = 0`.
2. Deploy API; create initial flags via admin API.
3. Roll out multiple instances behind LB; confirm version poll clears cache after admin update.
4. Rollback: redeploy previous API build; schema is additive—older builds that ignore new tables are N/A (greenfield). If needed, stop traffic and restore DB backup.
