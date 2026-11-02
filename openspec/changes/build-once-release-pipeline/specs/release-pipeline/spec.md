## Purpose

Defines how a release version is derived and validated, how plugin archives are built once and published unchanged, and what has to pass before a change merges or a release is published.

## ADDED Requirements

### Requirement: A release is requested by major, minor, and patch

A release SHALL be requested with a `major.minor.patch` version and a channel. The revision, the Abstractions version, and the tag SHALL be derived and SHALL NOT be supplied by the maintainer. The Abstractions version SHALL be the one resolved in the plugin's lock file at the commit being released.

#### Scenario: First Dev release of a version

- **WHEN** a Dev release of `6.5.0` is requested, no release exists for `6.5.0`, and the plugin resolves Abstractions `6.0.0-alpha.108`
- **THEN** the release version is `6.5.0.1`
- **AND** its tag is `v6.5.0.1-abstr-6.0.0-alpha.108`

#### Scenario: Next Dev release of a version

- **WHEN** a Dev release of `6.5.0` is requested and the highest existing revision of `6.5.0` is 3
- **THEN** the release version is `6.5.0.4`

#### Scenario: A revision known only from a tag

- **WHEN** a revision of the requested version exists as a tag but not in the manifest
- **THEN** that revision counts towards the next revision

#### Scenario: Stable release

- **WHEN** a Stable release of `6.5.0` is requested and the plugin resolves Abstractions `6.0.0`
- **THEN** the release version is `6.5.0` with no revision
- **AND** its tag is `v6.5.0-abstr-6.0.0`

#### Scenario: A malformed version

- **WHEN** the requested version is not three dot-separated integers, or any part has a leading zero
- **THEN** the release is refused before anything is built

### Requirement: Each version is used by one kind of release

Versions with a revision and a version without one SHALL NOT coexist for the same `major.minor.patch`. A request that would break this SHALL be refused, because the host's ordering of a version against its own revisions must not decide whether users are offered an upgrade.

#### Scenario: Dev after a release with no revision

- **WHEN** a Dev release of `6.4.0` is requested and `6.4.0` already exists
- **THEN** the release is refused

#### Scenario: Stable after Dev

- **WHEN** a Stable release of `6.5.0` is requested and `6.5.0.1` already exists
- **THEN** the release is refused

#### Scenario: Stable repeated

- **WHEN** a Stable release of `6.5.0` is requested and `6.5.0` already exists
- **THEN** the release is refused

### Requirement: The channel does not constrain the Abstractions version

The pipeline SHALL NOT refuse a release because of the relationship between its channel and its Abstractions version.

#### Scenario: Stable on a prerelease Abstractions version

- **WHEN** a Stable release is requested and the plugin resolves a prerelease Abstractions version
- **THEN** the release is not refused on that account

### Requirement: The released commit is fixed once per run

A release SHALL build the commit named by an optional commit-ish, or the dispatched ref when none is given. The commit-ish SHALL be resolved to one commit at the start of the run, and every later stage SHALL use that commit.

#### Scenario: No commit-ish

- **WHEN** a release is dispatched without a commit-ish
- **THEN** the commit at the dispatched ref is released

#### Scenario: A commit-ish names a branch

- **WHEN** a release names a branch and that branch moves during the run
- **THEN** every stage still uses the commit resolved at the start

#### Scenario: A commit that cannot take a resolved version

- **WHEN** the built archives do not carry the resolved version, as with a commit that predates this pipeline
- **THEN** the run fails before anything is published

### Requirement: Archives are built once and published unchanged

Each runtime's archive SHALL be built once per run. The archives tested SHALL be the archives published, byte for byte, and the published manifest SHALL record their checksums.

#### Scenario: Every supported runtime

- **WHEN** a run packages the plugin
- **THEN** it produces one archive each for `win-x64`, `linux-x64`, and `linux-arm64`
- **AND** a failure to produce any of them fails the run

#### Scenario: Publishing

- **WHEN** a release is published
- **THEN** the uploaded archives are the ones the test stage used
- **AND** nothing is rebuilt

#### Scenario: Checksums disagree

- **WHEN** an archive does not match the checksum recorded for it in the manifest
- **THEN** nothing is published

#### Scenario: Packaging on a pull request

- **WHEN** a pull request runs
- **THEN** it packages the plugin the same way a release does, against a copy of the published manifest
- **AND** it does not change the published manifest

### Requirement: Generated Lua stays current in the archive

The packaged Lua definitions SHALL match the ones committed at the released commit.

#### Scenario: Stale definitions

- **WHEN** packaging regenerates Lua definitions that differ from the committed ones
- **THEN** the run fails

### Requirement: Publishing requires a passing run

A release SHALL be published only by a dispatched run that is not a dry run, and only after packaging and every test leg have passed. Publishing SHALL create the GitHub release at the released commit, then update the manifest with the release notes and the source revision.

#### Scenario: A failing leg

- **WHEN** any test leg fails in a dispatched run
- **THEN** nothing is published

#### Scenario: A dry run

- **WHEN** a dry run is dispatched
- **THEN** version resolution, packaging, and every test leg run
- **AND** no tag, release, or manifest change is made

#### Scenario: A pull request

- **WHEN** a pull request runs
- **THEN** nothing is published

#### Scenario: Retrying a partly finished publish

- **WHEN** publishing failed after the release was created and the failed job is re-run
- **THEN** the same archives replace those on the release
- **AND** the manifest update is retried

### Requirement: Release runs do not overlap

Only one dispatched run SHALL be in progress at a time, because each reads the manifest early and pushes it late.

#### Scenario: Two dispatches

- **WHEN** a release is dispatched while another is running
- **THEN** the second waits and resolves its version after the first has finished

### Requirement: Only publishing can write to the repository

Stages other than publishing SHALL run with read-only repository access.

#### Scenario: Code under test

- **WHEN** a pull request's code runs during packaging or testing
- **THEN** it has no write access to the repository and no access to the release credentials

### Requirement: A single check gates merging

The pipeline SHALL report one aggregate result that passes only when version resolution, packaging, and every test leg have passed. Master SHALL require that result.

#### Scenario: A failing leg on a pull request

- **WHEN** any leg fails on a pull request
- **THEN** the aggregate result fails
- **AND** the pull request cannot merge

#### Scenario: Publishing is skipped

- **WHEN** a pull request passes and the publish stage is skipped
- **THEN** the aggregate result passes
