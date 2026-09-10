## Purpose

Defines how the system loads feature definitions from SQL Server and evaluates whether features are enabled for the whole application using the Feature Management runtime, including filter-based conditions, a public HTTP enablement probe, and multi-instance cache refresh.

## ADDED Requirements

### Requirement: Definitions are loaded from the SQL feature store
The system SHALL obtain feature definitions from SQL Server as the source of truth for evaluation. Definitions MUST NOT depend on Azure App Configuration.

#### Scenario: Known feature is evaluated from store
- **WHEN** the system evaluates a feature that exists in the SQL store
- **THEN** the system uses the definition currently represented in the store (subject to cache freshness rules)

#### Scenario: Unknown feature is treated as disabled
- **WHEN** the system evaluates a feature that does not exist in the SQL store
- **THEN** the system reports the feature as disabled

### Requirement: Boolean and filter-based conditions are supported
The system SHALL support features that are always off, always on, or conditionally enabled by one or more client filters. Supported built-in filter types for the MVP MUST include Percentage, TimeWindow, and Targeting. When multiple filters are configured, the system MUST honor the feature’s requirement type (Any or All).

#### Scenario: Feature disabled in store
- **WHEN** a feature is marked disabled in the store
- **THEN** evaluation reports the feature as disabled regardless of filters

#### Scenario: Feature always on
- **WHEN** a feature is marked enabled and has no client filters
- **THEN** evaluation reports the feature as enabled

#### Scenario: Percentage filter
- **WHEN** a feature is enabled with a Percentage filter configured to Value 50
- **THEN** evaluation enables the feature for approximately half of evaluation contexts according to the Percentage filter behavior

#### Scenario: Requirement type All
- **WHEN** a feature is enabled with requirement type All and multiple filters
- **THEN** evaluation reports enabled only if every configured filter evaluates to true

### Requirement: Public HTTP enablement probe
The system SHALL expose a public HTTP endpoint that accepts a feature name and returns whether that feature is currently enabled. The probe MUST use the same evaluation path as in-process Feature Management. Unknown feature names MUST be reported as not enabled. The endpoint MUST NOT require authentication for this POC.

#### Scenario: Probe returns enabled for an on feature
- **WHEN** a client calls the enablement probe with the name of a feature that evaluates as enabled
- **THEN** the response indicates the feature is enabled

#### Scenario: Probe returns disabled for an off or unknown feature
- **WHEN** a client calls the enablement probe with the name of a disabled feature or a name that does not exist
- **THEN** the response indicates the feature is not enabled

### Requirement: Cached definitions refresh across instances
The system MAY cache feature definitions in process for performance. After a successful administrative write that changes the store, every running instance MUST observe the change within a bounded freshness window without requiring process restart. Observation MUST be driven by a monotonic store version (or equivalent) persisted in SQL Server.

#### Scenario: Writer instance sees change immediately
- **WHEN** an instance successfully updates a feature definition in the store
- **THEN** subsequent evaluations on that same instance use the updated definition without waiting for the poll interval

#### Scenario: Peer instance refreshes after version bump
- **WHEN** one instance bumps the store version after a write
- **AND** another instance later detects that the store version is newer than its last seen version
- **THEN** that peer instance discards stale cached definitions and evaluates using definitions loaded from the store
