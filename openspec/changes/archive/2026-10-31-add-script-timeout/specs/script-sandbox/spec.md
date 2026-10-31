## ADDED Requirements

### Requirement: A script's running time is bounded

A user script SHALL be stopped once it has run for a bounded time, and the relocation SHALL report a timeout error. No published name SHALL let a script continue past the bound.

#### Scenario: A script that never terminates

- **WHEN** a script loops without terminating
- **THEN** it is stopped after the time bound
- **AND** the relocation reports an error saying the script timed out

#### Scenario: A script that catches the timeout error

- **WHEN** a script runs non-terminating code inside the published error-catching function, repeatedly or through an error object's string conversion
- **THEN** the script is still stopped
- **AND** the relocation reports an error saying the script timed out
