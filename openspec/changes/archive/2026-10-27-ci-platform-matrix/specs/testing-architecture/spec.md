## ADDED Requirements

### Requirement: Platform and host-configuration independence is exercised, not assumed

Continuous integration SHALL run the suite on every supported platform, and under at least one host configuration that differs from the defaults in the ways the suite claims not to depend on. Each configured leg SHALL demonstrate that its configuration took effect in the test process.

#### Scenario: Every supported platform

- **WHEN** a change is proposed
- **THEN** the suite runs on Windows x64, Linux x64, and Linux arm64
- **AND** a failure on any one of them fails the change

#### Scenario: A non-default locale

- **WHEN** a change is proposed
- **THEN** the suite also runs under a culture whose case mapping and decimal separator differ from the invariant culture's

#### Scenario: A non-default timezone

- **WHEN** a change is proposed
- **THEN** the suite also runs under a timezone whose UTC offset is not a whole number of hours and that observes daylight saving time

#### Scenario: A configuration that did not take effect

- **WHEN** a leg is configured for a culture or timezone but the test process observes a different one
- **THEN** that leg fails
- **AND** it does not pass on the strength of having run under the defaults

#### Scenario: One leg failing

- **WHEN** the suite fails on one platform or configuration
- **THEN** every other leg still runs to completion and reports its own result
