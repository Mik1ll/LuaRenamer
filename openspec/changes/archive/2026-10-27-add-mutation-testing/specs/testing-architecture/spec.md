## ADDED Requirements

### Requirement: The suite's ability to detect defects is measured by mutation

The production code the suite tests SHALL be subject to mutation analysis, which reports each mutation of that code that no test detects. Surviving mutations in the sandbox boundary SHALL each be either detected by a test or recorded as unobservable with a reason. The measurement SHALL be reported, and SHALL NOT gate merging a change.

#### Scenario: A mutation run

- **WHEN** mutation analysis runs against the production code
- **THEN** it reports every surviving mutation with its location and the change made
- **AND** the report is retained where a maintainer can read it after the run

#### Scenario: Code the suite does not own

- **WHEN** mutation analysis runs
- **THEN** it does not mutate build-time tooling whose output is guarded by an earlier check, or generated sources

#### Scenario: A survivor in the sandbox boundary

- **WHEN** a mutation survives in code that decides what a user script can reach
- **THEN** either a test is added that detects it, and that test names the behavior it protects rather than the mutation
- **OR** the mutation is recorded at its site as unobservable, with the reason it cannot change behavior

#### Scenario: A survivor elsewhere

- **WHEN** a mutation survives outside the sandbox boundary
- **THEN** it appears in the report
- **AND** its presence does not fail the run

#### Scenario: A change is proposed

- **WHEN** a change is opened for review
- **THEN** its mergeability does not depend on a mutation score
