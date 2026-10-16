## ADDED Requirements

### Requirement: A hung test run fails promptly and says where

A test run that stops making progress SHALL fail within a bounded time rather than running until an external limit, and SHALL leave diagnostics identifying the tests in flight when it was stopped. A test whose failure mode is non-termination SHALL be written so that a regression fails that test rather than hanging the run.

#### Scenario: A test that never finishes

- **WHEN** a test does not complete within the run's hang bound
- **THEN** the run fails
- **AND** a process dump and the names of the tests in flight are retained as artifacts of that run

#### Scenario: A run that exceeds its overall bound

- **WHEN** a continuous-integration run exceeds its overall time bound, for any reason
- **THEN** the run is cancelled and reported as failed

#### Scenario: A regression in a non-termination guard

- **WHEN** a guard that stops a non-terminating script regresses
- **THEN** the test covering that guard fails within its own time bound
- **AND** the other tests in the run still execute and report
