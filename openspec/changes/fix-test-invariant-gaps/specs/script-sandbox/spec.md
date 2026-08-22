## Purpose

Defines what a user-authored renaming script can reach when it runs: the published set of names, the guarantee that nothing outside that set is reachable by any route, and the shipped helpers that must remain callable through ordinary method syntax.

## ADDED Requirements

### Requirement: A script reaches only the published names

A user script SHALL resolve only the names the sandbox publishes. A name outside the published set SHALL NOT resolve, regardless of the route taken to it — including indexing a value of a primitive type, whose metatable would otherwise reach the unrestricted standard library.

#### Scenario: A published name

- **WHEN** a script names something the sandbox publishes
- **THEN** it resolves to the published value

#### Scenario: An unpublished standard-library name

- **WHEN** a script indexes a value of a primitive type for a standard-library name the sandbox does not publish
- **THEN** the name does not resolve

#### Scenario: A name added by a future runtime version

- **WHEN** the underlying Lua runtime or its host binding gains a standard-library name the sandbox does not publish
- **THEN** that name is not reachable from a user script

#### Scenario: Operations that outlive or escape the sandbox

- **WHEN** a script attempts to reach process control, the filesystem, the process environment, module loading, bytecode loading, or locale mutation
- **THEN** none of them resolve

### Requirement: Shipped helpers are callable as methods on the values they extend

Helpers defined by the shipped trusted chunks SHALL be callable using method-call syntax on values of the type they extend, not only through the published library table.

#### Scenario: A string helper

- **WHEN** a script calls a shipped string helper as a method on a string value
- **THEN** the helper runs and returns its result

#### Scenario: A helper added after the environment is built

- **WHEN** a trusted chunk defines a further helper into the published string table
- **THEN** that helper is likewise callable as a method on a string value

### Requirement: A script cannot affect a later relocation

State a script mutates SHALL NOT be observable by a script run for a subsequent relocation.

#### Scenario: A script mutates shared machinery

- **WHEN** a script replaces or extends a metatable, a published library table, or the environment itself
- **THEN** a script run for the next relocation observes the original state
