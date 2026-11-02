## ADDED Requirements

### Requirement: Continuous integration verifies the packaged archive

Each continuous-integration leg SHALL run the suite against the archive packaged for its platform, in the build configuration that ships, not against a separate build. Each leg SHALL demonstrate that the packaged build is what the test process loaded, and SHALL also load the archive the way the host does.

#### Scenario: Every leg

- **WHEN** a change is proposed
- **THEN** each leg runs the suite with the packaged plugin assemblies and native Lua library of its runtime in place

#### Scenario: A host-configuration leg

- **WHEN** a leg differs from a platform leg only in culture or timezone
- **THEN** it uses the archive of that platform

#### Scenario: The packaged build did not take effect

- **WHEN** a leg is expected to run against a packaged build but the test process loaded one stamped with a different runtime or version
- **THEN** that leg fails
- **AND** it does not pass on the strength of having tested a local build

#### Scenario: Loading the way the host does

- **WHEN** a leg loads its archive in an isolated plugin load context and runs a script through the relocation provider
- **THEN** the plugin's assemblies and native Lua library come from the archive
- **AND** only the framework and the assemblies the host supplies come from outside it
- **AND** the script's result is returned

#### Scenario: Something missing from the archive

- **WHEN** the archive lacks an assembly or native library the plugin needs
- **THEN** the leg fails
- **AND** a copy in the test process's own output does not stand in for it

#### Scenario: Outside continuous integration

- **WHEN** the suite runs with no packaged build expected
- **THEN** these tests report as skipped rather than passed
