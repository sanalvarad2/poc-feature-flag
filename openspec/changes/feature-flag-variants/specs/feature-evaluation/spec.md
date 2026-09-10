## ADDED Requirements

### Requirement: Variants and configuration values are resolved through Feature Management
The system SHALL support feature definitions that include named variants, each with an arbitrary configuration value (string, number, boolean, or JSON object). When a client requests the assigned variant for a feature, the system MUST resolve it using the official Feature Management variant allocation rules and return the assigned variant’s name and configuration value.

#### Scenario: Default variant when enabled
- **WHEN** a feature is enabled, has variants, and allocation specifies `default_when_enabled`
- **AND** no user, group, or percentile allocation matches the current targeting context
- **THEN** the system assigns the `default_when_enabled` variant and exposes its configuration value

#### Scenario: Default variant when disabled
- **WHEN** a feature is disabled (or evaluates as disabled) and allocation specifies `default_when_disabled`
- **THEN** the system assigns the `default_when_disabled` variant when a variant is requested

#### Scenario: User allocation wins
- **WHEN** allocation maps the current user id to a specific variant
- **THEN** the system assigns that variant regardless of group or percentile rules

#### Scenario: Group allocation
- **WHEN** no user allocation matches and the current user belongs to a group listed for a variant
- **THEN** the system assigns that variant

#### Scenario: Percentile allocation
- **WHEN** no user or group allocation matches and the user’s percentile falls in a configured range for a variant
- **THEN** the system assigns that variant (using the configured seed when present)

#### Scenario: Arbitrary configuration object
- **WHEN** the assigned variant’s configuration value is a JSON object
- **THEN** the returned configuration represents that object so callers can consume it as structured configuration

#### Scenario: Status override disables feature
- **WHEN** the assigned variant has status override Disabled
- **THEN** feature enablement evaluation reports the feature as disabled even if the flag’s base enabled state is on

### Requirement: Targeting context is available for allocation
The system SHALL derive a targeting context for variant allocation from the incoming HTTP request for the POC (at minimum a user identifier and zero or more group names). Missing user/groups MUST still allow default and percentile allocation to function according to Feature Management rules.

#### Scenario: User and groups from request
- **WHEN** a request includes the configured user and group targeting inputs
- **THEN** variant allocation uses that user id and those groups when evaluating user and group allocations

### Requirement: Public HTTP variant probe
The system SHALL expose a public HTTP endpoint that accepts a feature name and returns the assigned variant name and configuration value for the current targeting context. The endpoint MUST NOT require authentication for this POC.

#### Scenario: Probe returns assigned variant configuration
- **WHEN** a client calls the variant probe for a feature that has an assignable variant
- **THEN** the response includes the feature name, assigned variant name, and configuration value

#### Scenario: Probe for unknown feature
- **WHEN** a client calls the variant probe for a feature name that does not exist
- **THEN** the system responds without inventing a configuration value (not-found or empty assignment consistent with Feature Management behavior for unknown features)
