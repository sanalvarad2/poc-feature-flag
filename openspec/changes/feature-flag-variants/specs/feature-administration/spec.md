## MODIFIED Requirements

### Requirement: Clients can list and read features
The system SHALL provide HTTP operations to list all feature flags and to retrieve a single feature by name, including its enabled state, requirement type, configured filters, variants (name and configuration value), and allocation settings.

#### Scenario: List features
- **WHEN** a client requests the feature list
- **THEN** the system returns all features currently stored, each with enough detail to identify name, enabled state, filters, variants, and allocation

#### Scenario: Get feature by name
- **WHEN** a client requests a feature by its unique name and the feature exists
- **THEN** the system returns that feature’s full definition including filters, variants, and allocation

#### Scenario: Get missing feature
- **WHEN** a client requests a feature name that does not exist
- **THEN** the system responds with a not-found error

### Requirement: Clients can create and update features
The system SHALL allow clients to create a new feature with a unique name and to update an existing feature’s enabled state, requirement type, filter set, variants, and full allocation. Filter parameters MUST be accepted as structured data suitable for Percentage, TimeWindow, Targeting, and equivalent client filters. Variant configuration values MUST accept arbitrary JSON (string, number, boolean, or object). Allocation MUST accept defaults, user maps, group maps, percentile ranges, and an optional seed.

#### Scenario: Create feature
- **WHEN** a client creates a feature with a unique name and valid definition
- **THEN** the feature is persisted and subsequent reads return the new definition

#### Scenario: Reject duplicate name
- **WHEN** a client attempts to create a feature whose name already exists
- **THEN** the system rejects the request with a conflict error and does not create a duplicate

#### Scenario: Update feature and filters
- **WHEN** a client updates an existing feature’s enabled state, requirement type, or filters with a valid payload
- **THEN** the persisted definition matches the update and subsequent reads return the new values

#### Scenario: Create feature with variants and allocation
- **WHEN** a client creates a feature including variants and allocation (defaults, user, group, percentile, seed as applicable)
- **THEN** subsequent reads return those variants and allocation settings unchanged in meaning

#### Scenario: Update variants and allocation
- **WHEN** a client updates an existing feature’s variants or allocation with a valid payload
- **THEN** the persisted definition matches the update and subsequent reads return the new values

#### Scenario: Reject invalid allocation references
- **WHEN** a client submits allocation that references a variant name not present in the feature’s variants
- **THEN** the system rejects the request with a validation error and does not persist the change
