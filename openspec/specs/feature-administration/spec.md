# feature-administration Specification

## Purpose

Defines the administration surface for creating and maintaining system-wide feature flags and their filter configurations in SQL Server, including version bumps that drive multi-instance refresh. This POC does not require authentication.

## Requirements

### Requirement: Clients can list and read features
The system SHALL provide HTTP operations to list all feature flags and to retrieve a single feature by name, including its enabled state, requirement type, and configured filters.

#### Scenario: List features
- **WHEN** a client requests the feature list
- **THEN** the system returns all features currently stored, each with enough detail to identify name, enabled state, and filters

#### Scenario: Get feature by name
- **WHEN** a client requests a feature by its unique name and the feature exists
- **THEN** the system returns that feature’s full definition including filters

#### Scenario: Get missing feature
- **WHEN** a client requests a feature name that does not exist
- **THEN** the system responds with a not-found error

### Requirement: Clients can create and update features
The system SHALL allow clients to create a new feature with a unique name and to update an existing feature’s enabled state, requirement type, and filter set. Filter parameters MUST be accepted as structured data suitable for Percentage, TimeWindow, Targeting, and equivalent client filters.

#### Scenario: Create feature
- **WHEN** a client creates a feature with a unique name and valid definition
- **THEN** the feature is persisted and subsequent reads return the new definition

#### Scenario: Reject duplicate name
- **WHEN** a client attempts to create a feature whose name already exists
- **THEN** the system rejects the request with a conflict error and does not create a duplicate

#### Scenario: Update feature and filters
- **WHEN** a client updates an existing feature’s enabled state, requirement type, or filters with a valid payload
- **THEN** the persisted definition matches the update and subsequent reads return the new values

### Requirement: Clients can delete features
The system SHALL allow clients to delete a feature by name so it no longer appears in lists and evaluates as unknown/disabled.

#### Scenario: Delete existing feature
- **WHEN** a client deletes an existing feature by name
- **THEN** the feature is removed from the store and later get/list operations no longer include it

### Requirement: Writes bump the store version
Every successful create, update, or delete of feature data MUST increment a monotonic store version persisted in SQL Server so evaluation hosts can detect changes.

#### Scenario: Version increases on write
- **WHEN** a client successfully creates, updates, or deletes a feature
- **THEN** the store version is greater than it was before that write

#### Scenario: Failed write does not bump version
- **WHEN** an admin write fails validation or persistence
- **THEN** the store version remains unchanged

### Requirement: Admin API is unauthenticated for the POC
Feature administration HTTP endpoints MUST be callable without authentication or authorization for this proof of concept.

#### Scenario: Unauthenticated admin call accepted
- **WHEN** a client calls an admin feature endpoint without credentials
- **THEN** the system processes the request according to normal validation and persistence rules (it MUST NOT reject the call for lack of auth)

### Requirement: Features are system-wide
Administered feature flags MUST apply to the entire system. The admin API MUST NOT accept or persist per-tenant feature scopes.

#### Scenario: No tenant scope on create
- **WHEN** a client creates or updates a feature
- **THEN** the persisted definition is global and is not keyed by tenant
