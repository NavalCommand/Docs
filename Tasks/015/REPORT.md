# Task 015 — Acyclic domain architecture, simulation truth and observer-specific views

**Implemented and verified locally. No implementation commits were pushed.** This report follows the revised Task 015 pulled from Docs on 2026-10-11. The Task 014-era type layout and the earlier Task 015 UnitId-only observation layout are superseded. No compatibility wrappers, aliases or parallel legacy projections remain.

## Synchronization and local revisions

All eight requested working trees were inspected before pulling and were clean. Seven repositories contained prior unpublished Task 015 commits on their local branches. Docs had advanced independently: `git pull --ff-only` stopped because local Docs and `origin/main` had diverged, preserving both sides. I merged the fetched remote history with a normal `--no-ff` merge, resolved the sole `Tasks/README.md` conflict by preserving the historical Task 014 report reference and the new Task 015 assignment, then pulled all repositories again; the remaining remotes were already up to date. No reset, rebase, stash or forced update was used. The prior local implementation commits remain in history and were superseded by this revision; no user work was discarded.

| Repository | Synchronized local baseline for this revision | Remote baseline after pull | Task 015 implementation commit |
|---|---|---|---|
| Docs | `e2baab51b16ce125a94a01b86d259684470e3e51` | `a7cf20de572ab8df0752d9a8b062ad37f7e8a99a` | The local Docs commit containing this report and updated normative documents; nothing pushed |
| Domain.Api | `de4d5bf2c06b36e7faca6a7c9fcc77301a08c113` | `2f7148541cfe0fa922427f999d229b0817b84d5b` | `21951da22328695b1435f4695e8397ed71a12ff2` |
| Gateway.Api | `8c48bca27df96d9790f75a8600c32d4d4c1e4407` | `4bed3d2f6bcfe23389bf1cbc6b545710ace3ab65` | `03c2e6be6a806662e95a904182e581450134d897` |
| Gateway.Grpc.Api | `639bcdf6348c2106d04aa7a14ce33e7e53109bb2` | `2cd297a72ee3d7ea5e9f77fee6eaace3f22cc383` | `93bf73fc198f9fd4b28121a3339a12324b4428f3` |
| Gateway.Grpc | `6d97ee0b419c080ed3fb8e58a1414ccb06b24ae9` | `221de8f41c6dec8a0783064cea9683ac03caf1bb` | `9516a09c200cf953fdb326c11c84490f913b7795` |
| SeaPower.Client | `75cab70423a54ab92e3bf3aa745fb25869eaf145` | `88c6a4875a007056df6fd3ddb073b85758b80961` | `e4f19a5f2fd66f6440380a1d3eb3b8de1bd64d6b` |
| SeaPower.Mod | `5c31c56d90f393af936bedcc4de84496572752a9` | `9604c32b29d5e78d26f6218fc8fa0eb2d30b5031` | `a6e48097bc49a9f41d7f015a554411f5716d7fc8` |
| Build.CMake | `477d8055a366731839b42a4b73e913e0968301f9` | `477d8055a366731839b42a4b73e913e0968301f9` | Unchanged |

The Domain.Api source TODOs from synchronized remote `2f7148541cfe0fa922427f999d229b0817b84d5b` were read directly. They identified the ambiguous launch/deploy/jettison/action/duration/mode/index fields, `ResourceQuantity::Kind` numeric/accounting ambiguity, `OwnUnitId`/`TrackId` identity split, strong catalog IDs, direction/units, observation-vs-query time and vague provenance, duplicated physical traits, helicopter classification, stock/facility ownership and string-typed order eligibility. All affected source headers were superseded. The historical Task 012/013 findings and Task 014 report were not rewritten. Task 012's old domain proposal was marked historical/superseded so it cannot be mistaken for the current contract.

## Domain ownership and dependency enforcement

Seven domains are populated in the existing Domain.Api repository; Environment.Api remains an enforced reserved boundary with no empty target or fabricated environmental data.

| Domain | Unique responsibility | Exact allowed direct dependencies |
|---|---|---|
| `Physics.Api` | Strong physical quantities, geographic position and coordinate semantics | None |
| `Uncertainty.Api` | Generic `Knowledge<T>`, `ObservedCollection<T>` and coverage invariants | None |
| `Catalog.Api` | Definitive content-context unit/equipment/resource definitions, strong IDs, loadouts, effects and operations | Physics.Api |
| `Units.Api` | Authoritative `UnitId`, definite `UnitInstance`/`EquipmentInstance` facts and supported true runtime state | Catalog.Api, Physics.Api |
| `Observations.Api` | Observer/session-scoped identities, uncertain unit/equipment state, navigation/AirOps observations and evidence | Catalog.Api, Physics.Api, Uncertainty.Api |
| `Orders.Api` | Typed requests/results/descriptions and per-observation availability overlay | Observations.Api, Physics.Api, Uncertainty.Api |
| `Environment.Api` | Reserved for safe, provider-supported environment facts | Physics.Api, Uncertainty.Api if populated |
| `World.Api` | Separate simulation/observer snapshots, authoritative membership and observer-known relationship overlays; privileged in-memory correlation contract | Units.Api, Observations.Api, Environment.Api if populated, Physics.Api, Uncertainty.Api |

CMake target declarations link these direct dependencies. `Domain.Api/test/ExportDomainGraph.cmake` exports raw final direct `INTERFACE_LINK_LIBRARIES` properties. The maintained `CheckArchitecture.py` compares the evaluated target graph with public-header include edges, enforces namespace/header ownership and the allowlist, and detects cycles. It rejects quoted/relative and macro includes, external targets, aliases, dependency generator expressions, missing edges/targets and invalid use of reserved Environment. `ArchitectureTests.py` configures real target declarations and injects forbidden includes and target edges; it also tests cycles, missing links, aliases, external targets, generator expressions, relative/macro includes and the Environment boundary. CTest runs these checks with Python bytecode generation disabled.

A previous CMake-property exporter used `$<TARGET_PROPERTY:...,INTERFACE_LINK_LIBRARIES>` and CMake 4.2/CMP0189 expanded transitive links as if they were direct. The exporter now reads raw direct properties after configuration. The initial failure is corrected and the final negative suite passes. Tests include forbidden `Units→Uncertainty`, `Units→Observations`, `Observations→Units`, `Units→Orders`, `World→Orders`, and other disallowed direct edges. Every public header is also compiled alone and linked through its owning target.

## Truth, observations, orders and relationships

### Authoritative simulation truth

`Units.Api::UnitId` identifies one actual simulated entity. `UnitInstance` and `EquipmentInstance` expose only definite values; neither contains `Knowledge<T>`, an observer identity, order eligibility or faction membership. Unavailable values remain absent/optional instead of guessed default zero/false. Truth kinematics and equipment properties are independently optional. The current SeaPower adapter does not publish an authoritative simulation snapshot because its supported operational source is a controlled-side observer view, not a complete privileged world feed.

A tactical unit need not be a crewed platform. The catalog's platform classification is optional; its meaningful categories distinguish fixed-wing and rotary-wing aircraft (a helicopter is rotary-wing aircraft), surface vessel, submarine and land platform. A launched missile or torpedo can be a true `UnitInstance` with its own `UnitId`, type and supported kinematics without platform, aircraft, crew or AirOperations state. The catalog fixture defines a projectile `UnitType` without platform classification. A carried missile remains launcher equipment/resource inventory; the independently simulated launched projectile is a separate unit. No new projectile capture or missile order implementation was added.

### Observer evidence

`Observations.Api::ObserverId` identifies a perspective. Its `ObservationId` identifies an observer-side representation and is a different strong type from truth `UnitId`. Friendly and hostile observations share `ObservationId`. `UnitObservation` and `EquipmentObservation` are separate observer-specific structures, with `Knowledge<T>`, `ObservedCollection<T>`, measurement/evidence metadata and optional observed components. They are not `Knowledge<UnitInstance>` wrappers. A false, duplicated, fused or many-to-many contact has no mandatory `UnitId` or direct 1:1 truth relationship. Observer A and observer B can have different identities/knowledge for the same optional truth correlation.

The SeaPower provider keeps separate internal source-record maps for own units and plotting contacts. It uses per-observer opaque `ObservationId` handles, invalidated on game/player-taskforce/session changes; contact IDs follow plotting-record lifetime, not reusable numeric IDs or hidden `BaseObject` identity. Orders resolve an observation internally to a native object and recheck current player-taskforce membership, control, freshness and protected native state immediately before mutation. An identifier or old availability snapshot never grants authority.

Own source-backed `ObjectBase.name` is preserved as known `UnitObservation.Name`; contact names remain unknown unless faction-visible evidence exists. Own position/speed and heading are sampled as supported. Heading and course-over-ground remain distinct; course is not inferred from heading. The cached enemy plotting position has no verified measurement epoch or sensor identity and stays without fabricated measurement age/provenance. The provider publishes snapshot capture time separately.

### Separate World views, replay and groups

`World.Api::SimulationSnapshot` carries privileged true `UnitInstance`s and authoritative `UnitId` faction/task-force memberships. `World.Api::ObserverSnapshot` carries only `UnitObservation`s, its observer/session/time and uncertain relationship rows keyed by `ObservationId`. There is no aggregate `View` that can mix truth and observations. The ordinary GetView and provider envelope expose the observer view only.

The in-memory privileged `RecordedSnapshots` type can hold separate truth and observer snapshots plus optional `ObservationAssociation` pairs, supporting absent and many-to-many relationships without placing correlation in observer data. `Gateway.Api`'s internal-only `RecordedSession` can also hold issued order decisions/outcomes. It is not reachable through a provider endpoint, GetView, debug switch or gRPC RPC, and does not claim deterministic simulation replay. Current SeaPower observations represent own task-force relationship only when verified; faction and enemy-group affiliation remain unknown, not inferred from privileged native references.

### Orders overlay and AirOps inventory

Orders identify the requesting `ObserverId` and `ObservationId` plus session/freshness context. A `Request` owns exactly one typed `std::variant<RouteCommand, LaunchAircraft>` payload; the wire uses oneof. `OrderKind` replaces arbitrary strings. `OrderAvailability`/discovery is a separate overlay, not a UnitInstance, UnitObservation or World snapshot component. The provider rejects false/hostile/stale/reused handles even when an earlier availability row appeared executable. The native mailbox retains bounded queues, request idempotency, session reset, route replace/append/idle semantics, takeoff protection and game-thread revalidation.

Aircraft inventory is grouped once by actual aircraft type/squadron with a single observed aggregate count and a separately covered list of loadout choices. A per-order `AircraftSelection` identifies one type/squadron/loadout combination; it does not duplicate or partition stock count. `LaunchAircraft` availability remains in the order overlay. Public Facility/Stock entities and snapshot facility collections are deleted. An applicable true unit may have optional `Units.Api::AirOperations`, while the observer receives separate uncertainty-bearing `AirOperationsObservation`. Spawned aircraft receive an ordinary `UnitObservation`; launch results expose only optional `SpawnedObservationId`, never a truth UnitId. The existing tested native launch support remains limited to its previously validated small-airfield/F-4E/AirToAir scope.

### Equipment, resources and navigation

One `Catalog.Api::EquipmentType` is one independently addressable operational capability, not a physical-mount identity. Effects say what it does; operations say how (`Use` once or `SetActive` operating state). Target kind lists describe accepted designations; an empty list means no designation, not a fake required `None` argument. Launch/deploy/jettison synonyms, `CapabilityIndex`, invocation-derived duration, generic Active/Passive/Guided modes, and ambiguous duplicated `PhysicalTraits` are removed. Guidance is a specific damage/projectile trait; sensing emission is separate. Nominal resource range is intrinsic catalog data, not copied installation/runtime geometry.

One Catalog resource definition specifies strong ID, integer/continuous numeric representation, SI measurement unit and consumable/depletable versus allocatable accounting. A shared resource value (`uint64_t` or `double`) serves separate nominal capacity/initial-fit, operation consume/reserve and runtime-inventory relations. Discrete ammunition stays integer. Observer inventory has Unknown/Partial/Complete coverage; known zero, complete-empty and unknown remain distinct. This is a definition, not an accounting engine.

Navigation has no redundant `Inspected` field: Known(empty) route is inspected-empty; Unknown route is uninspected/unknown. Strong units remain in Physics.Api. No catalog retrieval, equipment extraction, environment simulation or replay store was added.

## Canonical Protobuf and consumer cutover

Schema version **4** is a direct breaking replacement, with no schema-3 fallback. Ordinary ProviderObservation contains an ObserverSnapshot and independent order-availability rows. Public `Gateway.Grpc.Api` GetView returns only ObserverSnapshot and validates observer/session scope; DiscoverOrders, requests, status and results carry ObserverId/ObservationId. True UnitId, SimulationSnapshot and correlation are absent from public operational messages. Privileged recording messages are isolated from public RPC/provider/bridge views. Cross-language Conformance serializes the same canonical schema through C# → C++ domain → C#.

`Gateway.Api` codec/provider, `Gateway.Grpc.Api` service/CLI, `Gateway.Grpc` server/mock, SeaPower.Client provider/native source capture, and SeaPower.Mod native bridge/managed transport fixtures were migrated. The native host byte-buffer ABI remains version 1 because its ABI shape did not change. Generated C++/C# bindings are build artifacts under each repository's ignored `o/`; only canonical Gateway.Api `.proto` source is maintained. No parallel managed domain model or alternate wire schema exists.

## Verification performed

Toolchain: Windows x64 VS 2026 MSVC 14.51, CMake/CTest 4.2.2, Ninja, .NET SDK 10.0.401, Python 3.13.3, PowerShell 7.6.5, pinned protoc 33.4. Generated Ninja rules use `/std:c++latest` and `-MT`; configured Vcpkg host/target triplets are `x64-windows-static`. This is not a claim of full C++26 compiler conformance.

All six affected functional repositories were reconfigured and built in their existing `windows-msvc-release` directories against the local checkout graph; builds were incremental, not `--fresh`/`--clean-first`. Exact common command pattern:

```powershell
$env:VCPKG_ROOT = 'C:/vcpkg'
$cmake = 'C:/Program Files/JetBrains/CLion 2026.1/bin/cmake/win/x64/bin/cmake.exe'
$ctest = 'C:/Program Files/JetBrains/CLion 2026.1/bin/cmake/win/x64/bin/ctest.exe'
& $cmake --preset windows-msvc-release `
  -DFETCHCONTENT_SOURCE_DIR_NCBUILD=C:/Software/GitHub/NavalCommand/Build.CMake `
  -DCMAKE_MAKE_PROGRAM=C:/Software/STM32CubeCLT/Ninja/bin/ninja.exe `
  -DVCPKG_INSTALLED_DIR=C:/Users/DmitriyPC/AppData/Local/Temp/naval-static-runtime-prefix `
  -DVCPKG_MANIFEST_INSTALL=OFF
& $cmake --build o/windows-msvc-release --parallel 8
& $ctest --test-dir o/windows-msvc-release --output-on-failure --no-tests=error
```

For Domain.Api omit the two Vcpkg options. Additionally executed:

```powershell
# Gateway.Api canonical schema compile is registered in CTest; resolved protoc is under the static-runtime prefix.
& $cmake --build o/windows-msvc-release --parallel 8 --target SeaPower.Client.Managed
& $cmake --build o/windows-msvc-release --parallel 8 --target managed-runtime managed-harness
& ./scripts/Test-Mock.ps1 `
  -HostExecutable C:/Software/GitHub/NavalCommand/Gateway.Grpc/o/windows-msvc-release/app/mock-host.exe `
  -Cli C:/Software/GitHub/NavalCommand/Gateway.Grpc.Api/o/windows-msvc-release/app/naval-cli.exe `
  -OutputDirectory C:/Software/GitHub/NavalCommand/Gateway.Grpc/o/architecture-r2-cli-pass
```

| Repository/scenario | Observed final result |
|---|---|
| Domain.Api | **69/69 CTests passed**: exact architecture graph, 21 negative graph/include fixtures, seven domain semantic consumers, and 60 standalone public-header consumers linked through owner targets |
| Gateway.Api | **4/4 CTests passed**: native semantic codec tests, independent transitive domain consumer, managed C#→C++→C# conformance, and actual canonical proto compilation/privacy assertions |
| Gateway.Grpc.Api | **1/1 CTest passed**; service codegen and CLI built |
| Gateway.Grpc | **1/1 CTest passed**, including seven server/provider cases for missing/unknown overlays, observer authority, false/fused contacts, stale session, route lifecycle and malformed payloads |
| SeaPower.Client | **1/1 managed fake-provider CTest passed**; standalone game-reference `SeaPower.Client.Managed` build succeeded |
| SeaPower.Mod | **2/2 CTests passed**: native observer-only host ABI tests and 31 managed runtime/harness transport checks; `managed-runtime` and `managed-harness` targets built |
| Mock-host and CLI workflows | Maintained PowerShell scenario passed ordinary read/discovery/order smoke (**`SMOKE_PASS`**) and movement replace/append lifecycle (**`MOVEMENT_ROUTE_PASS`**) against a fresh mock host per scenario |

Total final functional CTest count: **78/78 passed**. The mock CLI exercised connect, observer-scoped GetView/discovery, requests, results/status, route changes and stale-session/foreign-observation/invalid-argument/stale-version/unknown-route-mode rejection. Managed provider tests exercise own/contact ID separation, taskforce handles independent from SessionKey, false/fused identities, stale/reused IDs, direct current-control revalidation, known-empty/unknown routes, source-backed heading, unknown contact measurement time, taskforce overlay and the bounded aircraft-launch lifecycle through observation-scoped spawned results. The Gateway.Api and Domain.Api tests include typed relationships, independent observer projections, truth-vs-observation identity separation, projectile UnitInstance without platform classification, fused many-to-many privileged associations, name-known/name-unknown, known false/zero/empty, resource coverage, and grouped aircraft stock/loadout semantics.

Initial verification exposed and corrected test/integration defects before final results: Domain.Api had missing direct test-header includes and omitted `Observations.Api` in the CMake graph export list; the canonical codec lacked one strong LoadoutId/string encoder overload; C# conformance used generated enum/oneof names incorrectly and needed an explicit final return; the mock route CLI lacked a request ID and the codec rejected unknown RouteMode before provider-level `unsupported_route_mode`; a native host fixture omitted its snapshot session ID. The gateway false-contact discovery test was aligned with the intended `FAILED_PRECONDITION/order_availability_unknown` response when no overlay exists. Final results below were run after these fixes.

Build warnings remain: managed game-reference version conflicts (3 in the standalone SeaPower.Client build, 6 in the SeaPower.Mod managed runtime build, 2 in the harness build); upstream gRPC `IdentityKeyCertPair` deprecation and existing protobuf JSON-print `[[nodiscard]]` warnings in native CLI smoke code. Builds returned zero errors. No formatter/linter was run. Logs remain under ignored repository `o/architecture-r2-*.log`, `o/windows-msvc-release/Testing/Temporary/LastTest.log` and `Gateway.Grpc/o/architecture-r2-cli-pass/`.

No live game, loader installation, deployment/package staging, Debug configuration, Linux toolchain, clean remote-only dependency build or vendor binary changes were performed. Task 015 explicitly excludes live-game runs. Do not interpret offline adapter tests as live simulator evidence.

## Remaining boundaries and tradeoffs

- **Environment.Api remains reserved** because no safe verified environment data contract/provider exists; inventing fake readings or a placeholder target would add no information.
- Domain contracts distinguish privileged truth and observers, and the recording envelope can carry both with correlations/decisions/outcomes, but there is no full truth acquisition source or replay store in the current SeaPower adapter. Ordinary RPCs expose observer snapshots only.
- Plotting contacts do not provide verified sensor identity or a measurement epoch. Contact age/provenance stays unknown instead of being derived from snapshot capture or a cache path. Hidden authoritative native objects are not consulted to fill observation fields.
- World observer faction/taskforce knowledge remains limited to source-supported own-taskforce grouping; unknown affiliations remain unknown. No enemy faction is manufactured.
- Projectiles are representable as true and observed units, including catalog projectile types without platform classification. Native projectile-specific capture/commands were not added; tracking does not imply orderability.
- Native aircraft launch remains at the previously validated constrained operation. The refactor preserves its lifecycle and authorization checks; it does not establish general carrier launch support, recovery orders or persistent hosted-airframe IDs.
- Decisions/outcomes in `RecordedSession` are bounded data contracts only. No deterministic engine replay, ML pipeline, public privileged RPC, catalog retrieval or equipment extraction was added.

These are explicit provider/feature limits, not compatibility shims or unfinished migration paths. No architectural or safety contradiction required weakening the public separation of simulation truth from observer data.

## Artifact lifecycle

Production additions are the focused domain/wire contracts and migrated adapters. Maintained development additions are the direct dependency checker, negative DAG fixtures, standalone header consumers, semantic domain tests, canonical schema checker and updated mock scenario. There are no generated tracked bindings, task-specific runtime switches, deployment probes or retained migration scaffolds. Prior Task 014 implementation evidence and Task 012/013 game findings remain historical and unchanged; the former Task 012 accepted type proposal is marked superseded by this contract. Nothing was pushed.
