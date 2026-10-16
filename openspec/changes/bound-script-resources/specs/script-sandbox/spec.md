## ADDED Requirements

### Requirement: A script's execution and memory are bounded

A user script SHALL run within a bounded amount of execution and a bounded amount of memory. Exceeding either bound SHALL stop the script and be reported as the relocation's error, naming the bound that was exceeded. The host process SHALL be unaffected, and no published name SHALL let a script continue past a bound.

#### Scenario: A script that never terminates

- **WHEN** a script loops without terminating
- **THEN** it is stopped after a bounded amount of execution
- **AND** the relocation reports an error naming the execution bound

#### Scenario: A script that catches the bound's error

- **WHEN** a script runs non-terminating code inside the published error-catching function, including repeatedly or in nested calls
- **THEN** the script is still stopped
- **AND** the relocation reports an error naming the execution bound

#### Scenario: A script that registers a finalizer

- **WHEN** a script sets a metatable that has a `__gc` field
- **THEN** the call raises an error saying finalizers aren't available to scripts
- **AND** no finalizer the script supplied ever runs, during the relocation or when its environment is torn down

#### Scenario: Unbounded recursion

- **WHEN** a script recurses without a base case
- **THEN** the relocation reports an error
- **AND** the host process is unaffected

#### Scenario: Gradual memory growth

- **WHEN** a script keeps allocating without releasing
- **THEN** it is stopped once the memory bound is reached
- **AND** the relocation reports an error naming the memory bound

#### Scenario: A single oversized allocation

- **WHEN** a single published operation requests more memory than the bound allows
- **THEN** the request fails without the memory being obtained from the host
- **AND** the relocation reports an error naming the memory bound

#### Scenario: Bounds reset between relocations

- **WHEN** a script for one relocation exceeds a bound
- **THEN** the script for the next relocation runs with its full allowance and completes normally

#### Scenario: Ordinary scripts are unaffected

- **WHEN** the shipped default script runs against a fully populated environment
- **THEN** it completes without error even under bounds one hundredth of those in force
