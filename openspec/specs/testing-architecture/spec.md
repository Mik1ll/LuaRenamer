## Purpose

Defines the contract the automated test suite holds itself to: which layer a claim is verified at, how test arrangement is obtained, which behaviors are verified over generated inputs rather than enumerated examples, and which guards must fail before tests ever run.

## Requirements

### Requirement: Claims are verified at the layer that owns them

A test SHALL assert against the lowest-level unit that can express its claim. A test SHALL NOT execute the full relocation pipeline in order to observe behavior that a narrower unit already determines.

#### Scenario: A string-transform claim

- **WHEN** a test asserts that a path segment containing an illegal character is rewritten
- **THEN** it invokes path cleaning directly
- **AND** it constructs no host metadata graph, no Lua interpreter, and no relocation request

#### Scenario: A path-resolution claim

- **WHEN** a test asserts that a malformed value path is rejected
- **THEN** it invokes path resolution directly, without first populating an environment from a host metadata graph

#### Scenario: A mapping claim

- **WHEN** a test asserts how a host metadata value maps into the script environment
- **THEN** it asserts on the produced model or the materialized environment
- **AND** it does not infer the mapping from a resulting file name

### Requirement: Host graph arrangement comes from shared factories

Arrangement of host metadata graphs SHALL be obtained from a shared set of factories. A test SHALL NOT assemble a host graph inline, and SHALL NOT restate arrangement that a factory already provides in order to vary one value.

#### Scenario: Varying a single value

- **WHEN** a test needs the standard graph with one field differing
- **THEN** it obtains the standard graph from a factory and overrides that field
- **AND** it restates no other part of the graph

#### Scenario: Cyclic references

- **WHEN** a factory produces a series and its associated anime, which reference each other
- **THEN** the factory wires both directions
- **AND** no test is required to patch the cycle itself

#### Scenario: Referencing host metadata types

- **WHEN** the suite references the host's metadata interfaces
- **THEN** those references occur only within the arrangement factories, so that the boundary between the environment layer and the host mapping layer stays observable in the suite's structure

### Requirement: Identifier defaults cannot collide across identifier spaces

Default identifiers supplied by arrangement factories SHALL be drawn from disjoint ranges per identifier space, and SHALL NOT be zero or otherwise defaulted. Comparing an identifier from one space against an identifier from another SHALL NOT be able to match by coincidence.

#### Scenario: Cross-space comparison bug

- **WHEN** production code compares an identifier of one space against an identifier of another space
- **THEN** at least one test observes the resulting misbehavior
- **AND** the observation does not depend on the test having chosen bespoke identifiers

### Requirement: Environment marshaling invariants are verified over generated models

The rules by which a model graph becomes a script-visible environment SHALL be verified over generated model graphs, not only over enumerated examples. The suite SHALL verify all of the following.

#### Scenario: Presence and absence

- **WHEN** a model field holds a value
- **THEN** the corresponding environment key is present
- **AND** when a model field holds no value, the corresponding key is absent rather than present-and-empty

#### Scenario: Leaf types

- **WHEN** any leaf reachable in the materialized environment is inspected
- **THEN** it is a string, integer, floating-point number, boolean, table, or function
- **AND** it is never a host enumeration value or other host object

#### Scenario: Sequence integrity

- **WHEN** a model field holds a list
- **THEN** the resulting sequence is keyed from 1 with no gaps
- **AND** absent elements compact the sequence rather than leaving holes

#### Scenario: Enumeration fidelity

- **WHEN** a model field holds an enumeration value
- **THEN** the environment holds its name
- **AND** parsing that name recovers the original value

#### Scenario: Repeatability

- **WHEN** the same model graph is materialized twice
- **THEN** the two results are structurally equal

#### Scenario: Schema conformance

- **WHEN** the keys present on a materialized model are inspected
- **THEN** every key is a declared field of that model type
- **AND** no undeclared key is present

#### Scenario: Termination

- **WHEN** any generated model graph is materialized
- **THEN** materialization terminates without unbounded recursion

### Requirement: Path cleaning invariants are verified over generated input

Path cleaning SHALL be verified over generated inputs across all combinations of its behavior flags. For every input, cleaning SHALL either reject the segment with an error or return a segment satisfying all of the following.

#### Scenario: A cleaned segment

- **WHEN** cleaning returns a segment
- **THEN** it contains no character illegal for the target platform
- **AND** it does not begin with a space
- **AND** it does not end with a space or a period
- **AND** it is not a reserved device name
- **AND** it is not empty or whitespace-only

#### Scenario: Idempotence

- **WHEN** a returned segment is cleaned a second time under the same flags
- **THEN** the result is unchanged

#### Scenario: A replacement character that is itself illegal

- **WHEN** a caller supplies a replacement mapping whose replacement is itself an illegal character
- **THEN** cleaning reports an error rather than emitting the illegal character

### Requirement: Value path resolution is total and rejects non-value paths

Resolution of a value path into the script environment SHALL be verified over generated paths. Every input SHALL either resolve, return no value, or raise a path-validation error; no input SHALL raise an unrelated error.

#### Scenario: A well-formed path

- **WHEN** a path is generated from the accepted path grammar and the named value exists
- **THEN** resolution returns that value

#### Scenario: A well-formed path that names nothing

- **WHEN** a well-formed path names an absent key, an index past the end of a sequence, or traverses through a non-table leaf
- **THEN** resolution returns no value rather than raising

#### Scenario: A malformed path

- **WHEN** a path is empty, has an empty segment, has unbalanced or non-numeric brackets, has trailing content after an index, or denotes a call rather than a value
- **THEN** resolution raises a path-validation error
- **AND** it does not raise an index or reference error

### Requirement: Episode number collapsing round-trips

The collapsing of episode numbers into a padded, type-prefixed, range-compressed string SHALL be verified over generated episode sets by round-trip, in addition to enumerated regression examples.

#### Scenario: Round-trip

- **WHEN** a generated set of episodes is collapsed into its string form
- **THEN** parsing that string recovers exactly the input set of type-and-number pairs, with no additions, omissions, or duplicates

#### Scenario: Known collapsings

- **WHEN** a previously recorded episode set is collapsed
- **THEN** the result matches the recorded string, including padding width, type prefixes, and range boundaries

### Requirement: The script-visible sandbox surface is pinned

The exact set of names reachable from a user script SHALL be asserted. Adding or removing a name SHALL fail the suite until the assertion is updated.

#### Scenario: A name is added to the environment

- **WHEN** a name not previously reachable becomes reachable from a user script
- **THEN** the suite fails

#### Scenario: Process-global mutators stay unreachable

- **WHEN** the reachable surface is asserted
- **THEN** it excludes operations whose effect outlives the sandbox or extends beyond it, including locale mutation

### Requirement: Assertions do not depend on host locale or timezone

No assertion SHALL depend on the host's locale, its date-formatting conventions, or its timezone offset. Date and time behavior SHALL be asserted on discrete field values, or through explicit format specifiers that fix every rendered component.

#### Scenario: A date mapping

- **WHEN** a test asserts how a host date maps into the script environment
- **THEN** it asserts on the individual date fields

#### Scenario: A date formatting call

- **WHEN** a test asserts date formatting from within a script
- **THEN** it supplies explicit format specifiers rather than a locale-defined composite format

#### Scenario: Running under a different host configuration

- **WHEN** the suite runs under a locale or timezone other than the maintainer's
- **THEN** its results are unchanged

### Requirement: Vendored Lua libraries are not re-specified

Behavior belonging to a vendored, unmodified Lua library SHALL NOT be re-specified by the suite. Coverage of such a library SHALL be limited to confirming that it loads into the restricted environment and that its entry points resolve there.

#### Scenario: Vendored library coverage

- **WHEN** the suite exercises a vendored Lua library
- **THEN** it asserts that the library loaded and that a representative entry point resolves and returns
- **AND** it does not enumerate that library's behavior across its operations

### Requirement: Guards that can fail earlier do not live in the suite

A constraint that can be enforced before tests execute SHALL be enforced at that earlier point rather than as a test. Dependency version constraints SHALL be expressed so that a violating resolution fails at dependency restore.

#### Scenario: An incompatible dependency major version

- **WHEN** a dependency is bumped to a major version the plugin cannot run against
- **THEN** dependency restore fails
- **AND** the failure names the offending dependency and the permitted range
- **AND** no test asserts the resolved version

### Requirement: End-to-end coverage spans the result-shaping paths

Each path that shapes a relocation result SHALL have end-to-end coverage, including those whose arrangement cost previously left them uncovered.

#### Scenario: Destination selection

- **WHEN** a script selects a destination by name, by path, by folder reference, or leaves it unset
- **THEN** each form is covered, along with rejection of a folder that is not a destination folder

#### Scenario: Reuse of an existing location

- **WHEN** a script opts into reusing an existing location for the series
- **THEN** coverage includes the selection among differing candidate locations and the fallback when none qualifies

#### Scenario: The shipped default script

- **WHEN** the shipped default script runs against a fully populated environment
- **THEN** it completes without error and produces a file name and subfolder
