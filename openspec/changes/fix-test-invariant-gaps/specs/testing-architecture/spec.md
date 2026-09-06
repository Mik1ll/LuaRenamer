## ADDED Requirements

### Requirement: Assertions hold on every supported platform

No assertion SHALL depend on the maintainer's operating system. Where a platform's path semantics differ, an assertion SHALL be stated against the semantics in force on the running platform rather than against those of one platform.

#### Scenario: Platform constants that coincide

- **WHEN** an assertion is written in terms of two platform constants that are distinct on one platform and identical on another
- **THEN** it states a condition that remains satisfiable where they coincide
- **AND** it does not reduce to a condition no correct result could meet

#### Scenario: Platform-conditional rules

- **WHEN** a test asserts behavior governed by a platform-dependent rule set
- **THEN** it asserts against the rule set actually in force for the configuration under test

#### Scenario: Running on the continuous-integration platform

- **WHEN** the suite runs on the platform continuous integration uses
- **THEN** its results are unchanged from the maintainer's platform

### Requirement: Arrangements can discriminate which identifier a behavior used

Where production code selects or orders host entities by an identifier, the arrangement SHALL vary the candidate identifier spaces independently, so that an assertion naming one space fails if the production code used another. Identifier bands are an arrangement convention and SHALL NOT be asserted as a property of the system.

#### Scenario: Ordering by identifier

- **WHEN** a test asserts that a selection is made by an entity's identifier in one identifier space
- **THEN** the arrangement orders the other identifier spaces differently from that one
- **AND** the assertion fails if the production code selects by any of the others

#### Scenario: Cross-space comparison bug

- **WHEN** production code compares an identifier of one space against an identifier of another space
- **THEN** at least one test observes the resulting misbehavior
- **AND** the observation does not depend on the test having chosen bespoke identifiers

#### Scenario: Coincidental equality between spaces

- **WHEN** identifiers from two spaces would be equal in production
- **THEN** no test fails on that account, because no test asserts that the spaces are disjoint

## MODIFIED Requirements

### Requirement: Environment marshaling invariants are verified over generated models

The rules by which a model graph becomes a script-visible environment SHALL be verified over generated model graphs, not only over enumerated examples. Every model type in the schema SHALL be covered. The suite SHALL verify all of the following.

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

#### Scenario: Map integrity

- **WHEN** a model field holds a keyed collection
- **THEN** the resulting table carries exactly the keys the collection held
- **AND** a key the collection did not hold fails the check

#### Scenario: Callable fields

- **WHEN** a model field declares a callable, whether implemented in the host language or as a script source
- **THEN** the materialized value is callable from a script
- **AND** a value that is merely the callable's source text fails the check

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

#### Scenario: A schema shape that would not terminate

- **WHEN** the schema gains a required model-to-model reference that does not pass through a collection
- **THEN** graph generation reports that shape and names the field
- **AND** it does not exhaust the stack

#### Scenario: Model type coverage

- **WHEN** the generated-model verification completes
- **THEN** every model type the schema declares has been generated and checked at least once
- **AND** a model type that was never reached fails the suite

### Requirement: The script-visible sandbox surface is pinned

The exact set of names reachable from a user script SHALL be asserted, including names reachable indirectly rather than only those held directly by the environment table. Adding or removing a name SHALL fail the suite until the assertion is updated.

#### Scenario: A name is added to the environment

- **WHEN** a name not previously reachable becomes reachable from a user script
- **THEN** the suite fails

#### Scenario: Names reachable through a value's metatable

- **WHEN** a name is reachable by indexing a value of a primitive type rather than by indexing the environment
- **THEN** the pinned surface accounts for it
- **AND** a name reachable that way but absent from the published set fails the suite

#### Scenario: Process-global mutators stay unreachable

- **WHEN** the reachable surface is asserted
- **THEN** it excludes operations whose effect outlives the sandbox or extends beyond it, including locale mutation

## REMOVED Requirements

### Requirement: Identifier defaults cannot collide across identifier spaces

**Reason**: The requirement asserted disjointness of identifier spaces as though it were a property of the system, but Shoko and AniDB identifiers are independent sequences that may legitimately coincide in production. The test enforcing it (`FakesTests.IdentifierSpacesCannotCollide`) therefore checked a fact about the fixture's own constants while reading as a claim about the domain, and would have obstructed any future arrangement that deliberately made two spaces agree.

**Migration**: Replaced by "Arrangements can discriminate which identifier a behavior used", which keeps the cross-space observability scenario that gave the original its value and drops the disjointness guarantee. The disjoint bands in `Ids.cs` are retained as an arrangement convention, no longer asserted as a system invariant.
