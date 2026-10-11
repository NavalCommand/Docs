# Task 015 — Acyclic domain architecture and semantic refactor

**Implemented and verified locally. Nothing pushed.** This directly replaces Task 014's type layout and canonical wire contract. No compatibility aliases, legacy projections or managed domain replica were introduced. No game was launched, loader installed/activated, deployment changed or vendor binary modified.

## Synchronization and revisions

All eight requested repositories were on `main` with clean working trees before `git pull --ff-only`. Docs fast-forwarded to the new assignment; the other repositories were already current. No stash, reset, rebase or forced update was needed. Source TODOs in Domain.Api were read alongside the new task and normative documentation. Historical Task 012–014 findings and Task 014's report were left unchanged.

| Repository | Synchronized base | Local implementation commit |
|---|---|---|
| Docs | `9ad1d186ecc81abe6d58ff83b3dbc472a34595cb` | Commit containing this report and the normative documentation/index changes |
| Domain.Api | `2f7148541cfe0fa922427f999d229b0817b84d5b` | `de4d5bf2c06b36e7faca6a7c9fcc77301a08c113` |
| Gateway.Api | `4bed3d2f6bcfe23389bf1cbc6b545710ace3ab65` | `8c48bca27df96d9790f75a8600c32d4d4c1e4407` |
| Gateway.Grpc.Api | `2cd297a72ee3d7ea5e9f77fee6eaace3f22cc383` | `639bcdf6348c2106d04aa7a14ce33e7e53109bb2` |
| Gateway.Grpc | `221de8f41c6dec8a0783064cea9683ac03caf1bb` | `6d97ee0b419c080ed3fb8e58a1414ccb06b24ae9` |
| SeaPower.Client | `88c6a4875a007056df6fd3ddb073b85758b80961` | `75cab70423a54ab92e3bf3aa745fb25869eaf145` |
| SeaPower.Mod | `9604c32b29d5e78d26f6218fc8fa0eb2d30b5031` | `5c31c56d90f393af936bedcc4de84496572752a9` |
| Build.CMake | `477d8055a366731839b42a4b73e913e0968301f9` | Unchanged |

Review/build the complete local graph together. Existing ignored top-level `Build.Local.cmake` registrations resolve the canonical URLs to these checkouts; no implicit sibling resolver or committed machine paths were added. The Docs commit can be identified with `git log -1 --format=%H -- Tasks/015/REPORT.md` in Docs.

## Ownership and enforceable dependency graph

All domain definitions live in the existing Domain.Api repository under `public/NavalCommand/<Domain>/Api/`. Six populated domains have independent header-only INTERFACE targets. Environment is a seventh **reserved boundary**, not an invented empty API or repository.

| Domain | Unique responsibility | Allowed direct dependencies |
|---|---|---|
| Physics.Api | Focused physical values and geodetic position/coordinates | None |
| Uncertainty.Api | Generic knowledge and observed collection/coverage invariants | None |
| Catalog.Api | Definitive session-context catalog, strong IDs, resolved loadouts, operational capability effects/operations and resource definitions | Physics.Api |
| Units.Api | Observer-facing identity and composed runtime observations, equipment, navigation/flight and attached air inventory | Catalog.Api, Physics.Api, Uncertainty.Api |
| Orders.Api | Exactly-one typed payload, lifecycle/results and separate per-unit discovery/availability | Units.Api, Physics.Api, Uncertainty.Api |
| Environment.Api | Reserved ownership of supported terrain/weather/ocean observations | Physics.Api, Uncertainty.Api |
| World.Api | Session/capture/clock aggregation and unit-membership relationships | Units.Api, Environment.Api when populated, Physics.Api, Uncertainty.Api |

World currently links Units, Physics and Uncertainty only. Units never imports Orders or World; World never imports Orders; Catalog never imports Uncertainty. Gateway.Api has the required domain dependencies PUBLIC and owns composition/serialization outside the DAG.

Enforcement is maintained code, not just documentation:

- `Domain.Api/test/CheckArchitecture.py` checks canonical namespace/header ownership, direct public-header includes, the exact allowlist, cycles, missing targets and equality between actual include edges and direct target links. Quoted/relative and macro-based includes are rejected; aliases, external/unknown target links and dependency generator expressions cannot hide an edge.
- `test/ExportDomainGraph.cmake` exports the final actual `INTERFACE_LINK_LIBRARIES` properties after configuration, rather than regex-parsing CMake source. Raw direct properties deliberately preserve expressions so the checker can reject them.
- Initial verification exposed CMake 4.2/CMP0189 behavior: `$<TARGET_PROPERTY:...,INTERFACE_LINK_LIBRARIES>` expanded transitive links and falsely classified Orders → Units → Catalog as a direct Orders → Catalog edge. Replaced that exporter with deferred direct `get_target_property` reads. Both real-graph and negative tests then passed.
- `test/ArchitectureTests.py` runs 16 fixtures using real modified CMake target declarations and copied headers. Forbidden Units → Orders, Units → World and World → Orders includes/links, target/header cycles, missing links, aliases, external links, generator expressions, relative/macro includes and reserved Environment violations are rejected.
- All 48 exported headers compile as their translation unit's only domain include and link through their owning target. Six additional semantic consumer executables link the individual populated targets.
- CTest invokes Python with `-B`; generated Python bytecode is not left in maintained source directories.

A separate executable smoke scenario ran the checker on the valid graph, then on an isolated injected `Units/Api/ForbiddenEdge.h`. The valid graph exited 0. The injected include exited 1 and reported:

```text
ForbiddenEdge.h:2: forbidden include edge Units.Api -> Orders.Api (NavalCommand/Orders/Api/Request.h)
Public header cycle: Orders -> Units -> Orders
```

The isolated copy was removed after verification. A shared public include root remains convenient for compilation, but it cannot make a forbidden dependency pass this checker.

## Semantic decisions and deletions

### One observation identity; authority is separate

`Units.Api::UnitId` is the sole public strong observation identity, with an opaque string value scoped to observer/session. Own units and contacts use the same type through observation, discovery, commands and spawned-aircraft results. It does not promise one actual platform or grant control.

SeaPower.Client retains separate internal own-source/contact-source maps so equal backend keys never merge identities. The native contact source uses plotting-record reference lifetime rather than reusable numeric contact IDs or hidden native platform identity. Handles remain stable across coordinate gaps while that record is retained, and retired/reused records obtain new identities. GameMain/player-taskforce source changes invalidate the public session even if readiness never becomes false. Orders are revalidated against current side/membership and native safety at dispatch and immediately adjacent to native mutation.

World's membership overlay references the same UnitId. The provider publishes a session-scoped taskforce handle for verified own membership; faction remains unknown when not supplied. It does not guess enemy affiliation from privileged objects or the observer string. The mock uses an independent current-control set and rejects revoked control even when an old overlay is executable.

Deleted public `TrackId`, `OwnUnitId`, `Subject` and own-versus-track wire tags. Native contact capture seams remain internal backend mechanics, not public identity or a second operational schema.

### Orders are an overlay

`Orders.Api::Request::Payload` is `std::variant<RouteCommand, FacilityLaunch>`: exactly one typed command. Wire requests use oneof payload; missing payloads are rejected. `OrderKind` replaces arbitrary eligibility strings. Each `OrderAvailability` identifies a UnitId and observation version, with explicit coverage and executable/blocked/unsupported/unknown distinctions and reasons.

Neither UnitInstance nor WorldSnapshot contains eligibility. `Gateway.Api::ProviderObservation` combines a World snapshot and separate availability rows for provider/bridge publication. External GetView returns only WorldSnapshot; DiscoverOrders serves the independent overlay. The composition codec rejects schema mismatch, foreign-session rows, duplicate/orphan identities and version mismatches.

Launch availability is selector-specific in Orders, not duplicated inside inventory. `Units.Api::AircraftSelection` describes a genuine inventory aircraft/squadron/loadout selection reused by `Orders.Api::FacilityLaunch`; Orders does not include an unrelated Units header merely to smuggle in Catalog definitions. No second order framework was introduced. Mailbox acceptance is not execution: authoritative game-thread validation remains required.

### Equipment and resources

One Catalog EquipmentType is one independently addressable operational capability, not necessarily one physical launcher/device. It has effects and operations without another capability identity/index. Use is a single invocation; SetActive requests operating state. The lasting effect of an expendable deployment is not equated with invocation duration. Accepted target kinds describe meaningful designations; an empty list means no designation, not a compulsory None argument.

Deleted `EquipmentCapability` as parallel identity, `CapabilityIndex`, action-derived Duration, generic Active/Passive/Guided operation modes and all ambiguous duplicated PhysicalTraits. Concrete projectile guidance is separate from sensing traits. Optional nominal sensing range is an intrinsic catalog constraint, not copied installation geometry/runtime truth. Unused physical structures are deferred.

One resource model serves nominal capacity/initial inventory, operation consumption/reservation and current observations:

- Strong ResourceId plus definitive numeric representation, measurement unit and consumable/allocatable accounting semantics.
- `ResourceValue = std::variant<uint64_t, double>` preserves discrete counts exactly; continuous quantities use the definition's SI unit.
- Relationships retain their own names rather than three duplicated quantity systems.
- Runtime resource collections have Unknown/Partial/Complete coverage. Known zero is distinct from unknown; complete-empty reports no applicable resources.

Deleted the old ammunition/deployment-charge/jam-channel/fuel-litre Kind enum. There is no accounting engine, generic units framework or inferred fitted equipment. Equipment capture remains unsupported and is published as unknown, never complete-empty/unarmed.

### Composition, navigation and observation time

Units own identification, kinematics, equipment and optional navigation, flight and air operations. Faction/taskforce relationships and order discovery are separate overlays.

Navigation has only route knowledge: Known(empty) is inspected-empty; Unknown is uninspected/unavailable. Deleted the duplicate `NavigationState.Inspected`. Heading and course-over-ground are separate typed radians. Own heading is source-backed: existing Task 013 decompilation of SeaPower.ObjectBase shows Heading at line 1749, assignment from getHeading at 2167 and getHeading returning transform.eulerAngles.y at 7442–7448. The adapter converts degrees to radians when the transform exists. Course remains unknown; no enemy direction is invented. Game-reference compilation passed, but these changed mappings were not live-game exercised.

World CapturedAt is operational snapshot capture time. Unit MeasuredAt is last actual measurement only when known. Cached enemy measurement age stays unknown; capture time is not asserted as sensor measurement time. The hard-coded plotting-cache provenance/sensor identity duplication is removed. Estimate source text remains an evidence trace, not an invented sensor identity. Air inventory has its own actual observation time when available.

### Air operations and classification

Deleted public World Facility/Stock and snapshot-level facilities. Applicable carriers/airfields/helicopter-capable units carry optional Units AirOperations with observed AircraftInventoryEntry records, known/unknown counts, pending/onboard/reserved/cooldown semantics and measurement time. The unit's UnitId identifies the owner; no second airfield entity exists.

Inventory reports selectors and observations, not permission. Native reservation, preparation, ordinary grounded spawn and airborne observation remain distinct. Spawned aircraft enter ordinary Units/Orders immediately, while native takeoff protection continues. No unsupported individual hosted-airframe fuel/damage/readiness model was invented.

Catalog classification is small and definitive. A helicopter is Aircraft with RotaryWing classification, not a disjoint empty platform alternative. UnitTypeId, EquipmentTypeId, LoadoutId and ResourceId are strong types. Catalog fields have no observer uncertainty or provider-specific service-year model. Domain fixtures cover two resolved loadouts sharing definitions.

## Canonical wire and consumer cutover

Schema version is **3**, a direct breaking replacement. Canonical files remain only in Gateway.Api; C++ and C# regenerate from those same inputs, including the new provider composition envelope. Package paths are retained without a schema-2 fallback. The native byte-buffer/lifecycle ABI remains version 1 because that ABI did not change.

Gateway.Api codecs/provider contracts, Gateway.Grpc service/server/mock, Gateway.Grpc.Api CLI smoke consumers, SeaPower.Client provider/native capture and SeaPower.Mod native/managed transport tests all use the revised contract. Runtime/harness production code intentionally stays unchanged where it handles opaque bytes or unchanged result fields; both managed projects were built against the regenerated graph. There are no new catalog/environment/equipment RPCs or deployment changes.

Canonical schema verification compiles the actual protos rather than testing incidental source spelling. Native codecs and cross-language conformance verify semantic values, including integer resource counts beyond double's exact range, continuous zero, false, zero lifecycle counts, known-empty routes and complete-empty versus unknown/partial collections. Domain conversion does not promise preservation of arbitrary unknown protobuf fields; direct generated-message behavior is separate from domain semantics.

## Verification executed

All six functional repositories were independently reconfigured and rebuilt in their existing Release preset directories against the synchronized local graph. This was an incremental migration build, **not a claim of fresh/clean-first builds**. Bindings were regenerated by the normal CMake/MSBuild steps. Build.CMake was unchanged; its infrastructure suite was not rerun.

Toolchain: CMake/CTest 4.2.2, VS 2026 MSVC 14.51 toolchain, Ninja, .NET SDK 10.0.401, PowerShell 7.6.5, Python 3.13.3, pinned protoc 33.4. Generated Ninja rules use `/std:c++latest` and `-MT` (static MSVC CRT); cached host and target triplets are `x64-windows-static`. No full C++26 compiler-conformance claim is made.

Commands executed from a VS x64 developer environment with `VCPKG_ROOT=C:/vcpkg` and PowerShell/.NET on PATH:

```powershell
$cmake = 'C:/Program Files/JetBrains/CLion 2026.1/bin/cmake/win/x64/bin/cmake.exe'
$ctest = 'C:/Program Files/JetBrains/CLion 2026.1/bin/cmake/win/x64/bin/ctest.exe'
# In each functional repository; omit the two vcpkg options for Domain.Api:
& $cmake --preset windows-msvc-release `
  -DFETCHCONTENT_SOURCE_DIR_NCBUILD=C:/Software/GitHub/NavalCommand/Build.CMake `
  -DCMAKE_MAKE_PROGRAM=C:/Software/STM32CubeCLT/Ninja/bin/ninja.exe `
  -DVCPKG_INSTALLED_DIR=C:/Users/DmitriyPC/AppData/Local/Temp/naval-static-runtime-prefix `
  -DVCPKG_MANIFEST_INSTALL=OFF
& $cmake --build o/windows-msvc-release --parallel 8
& $ctest --test-dir o/windows-msvc-release --output-on-failure --no-tests=error
# In SeaPower.Client:
& $cmake --build o/windows-msvc-release --parallel 8 --target SeaPower.Client.Managed
# In SeaPower.Mod:
& $cmake --build o/windows-msvc-release --parallel 8 --target managed-runtime managed-harness
& ./scripts/Test-Mock.ps1 `
  -HostExecutable C:/Software/GitHub/NavalCommand/Gateway.Grpc/o/windows-msvc-release/app/mock-host.exe `
  -Cli C:/Software/GitHub/NavalCommand/Gateway.Grpc.Api/o/windows-msvc-release/app/naval-cli.exe `
  -OutputDirectory C:/Software/GitHub/NavalCommand/Gateway.Grpc/o/architecture-cli
```

| Repository/scenario | Observed result |
|---|---|
| Domain.Api | **56/56 CTests passed**: real graph, negative fixtures, six domain semantic consumers and 48 standalone header consumers |
| Gateway.Api | **4/4 CTests passed**: seven native codec cases, transitive domain consumer, C# → C++ domain → C# conformance and canonical proto compilation |
| Gateway.Grpc.Api | **1/1 CTest passed**, service bindings/CLI built |
| Gateway.Grpc | **1/1 CTest passed**, containing seven server/provider cases including revoked authority, missing/unknown availability and route sequences |
| SeaPower.Client | **1/1 fake-provider CTest passed**; game-reference managed provider build passed |
| SeaPower.Mod | **2/2 CTests passed**: three native host cases and 31 managed runtime/harness checks; managed runtime and harness builds passed |
| Maintained mock-host/CLI script | Ordinary smoke **SMOKE_PASS** and route smoke **MOVEMENT_ROUTE_PASS**, each using a fresh owned mock host |
| Direct architecture checker smoke | Valid graph exit 0; injected forbidden Units → Orders header exit 1 with actionable file/line and cycle diagnostic |

Total functional CTest result: **65/65 passed**. Logs remain in each repository's ignored `o/architecture-*.log` and `o/windows-msvc-release/Testing/Temporary/LastTest.log`; detailed CLI output is in Gateway.Grpc's ignored `o/architecture-cli/`.

Behavioral evidence is not limited to codec round trips:

- Fake provider checks stable own/contact handles, equal-source isolation, coordinate gaps, enemy/retired/reused/stale handles, ownership races adjacent to mutation, continuous-ready session changes, taskforce overlay and protected native sequences.
- Routes exercise inspected-empty versus unknown, idle non-destructive behavior, moving replace/append sequences, duplicate idempotency, future/aged versions and queued-append displacement not falsely reported as execution.
- Air-operation fixtures exercise known zero/unknown/partial/complete-empty inventory, separate selectors/eligibility, stale views, resource/operability/current-side rechecks, reservation idempotency, ordinary grounded spawn, protected takeoff, airborne transition, uncertain native outcomes, bounded tracking and subscription cleanup.
- Domain fixtures exercise guided missile, radar, active ECM, chaff, sonobuoy and external tank, multi-effect/operation capability semantics, consumption versus reservation, two shared/resolved loadouts, integer/continuous values, memberships and default-unknown rejection invariants.
- The real mock CLI exercises the new wire across connect/observe/discover/submit/status and visible route changes, including negative stale-session/side/argument/version/mode requests.

Initial architecture-export and canonical-checker failures were fixed before the passing results above. The former is documented with its CMP0189 cause. The latter was a PowerShell multiline-expression parser failure in source-spelling assertions; those incidental implementation checks were removed rather than repinned. Actual protoc compilation and semantic codec/conformance tests remain.

Managed game-reference builds retain assembly-version conflict warnings: three for the standalone provider, six for runtime/transitive references and two for harness. Native builds retain upstream gRPC deprecation and JSON-print discarded-return warnings. These are not claimed resolved. No formatter/linter run is claimed.

## Tradeoffs, limitations and artifact lifecycle

- **Environment reserved:** supported, side-effect-free acquisition is absent, so no fake data or meaningless target was added. The reserved allowlist remains enforced.
- **Strict dependency expressions:** domain targets are simple INTERFACE contracts. Aliases and dependency generator expressions are rejected rather than introducing a general CMake-expression evaluator. Future conditional domain dependencies require an explicit checker/design change.
- **Resource values versus accounting:** the integer/continuous variant avoids numeric loss; definitions supply representation/unit/accounting. It is not a catalog-resolution or runtime accounting engine. Producers must use resource definitions consistently.
- **Observer identity:** native record/reference lifetime supports stable handles, not proof of platform truth or automatic identity continuity across provider replacement/fusion. Contacts are never merged using hidden identity.
- **Membership evidence:** own taskforce membership is supported; unknown faction and enemy relationships remain unknown. There is no guessed affiliation.
- **Air operations:** existing aggregated inventory and lifecycle are preserved. Native launch support remains the previously validated constrained own-airfield/aircraft/loadout operation; this refactor does not expand launch capability or prove general carrier dispatch.
- **Not reverified:** live game behavior, Debug configuration/known historical gRPC teardown issue, Linux toolchains, interactive IDE workflows, clean remote dependency resolution and packaging/deployment. Live runs and installation were explicitly out of scope. Offline native/managed/provider evidence must not be read as live-game verification.

No unresolved contradiction required violating the requested DAG or retaining a compatibility layer. Remaining tradeoffs above are explicit limits of provider evidence and supported features, not unfinished migration paths.

Production additions are focused domain contracts, canonical messages/codecs and migrated adapters. Maintained development additions are the graph checker/exporter, independent header consumers and updated mock smoke entry point. Semantic/negative fixtures remain isolated tests. The throwaway forbidden-edge copy and generated source-tree Python bytecode were removed; CTest now suppresses that bytecode. Generated bindings, binaries and logs stay ignored under `o/`. No temporary implementation scaffolds, deployment probes or compatibility aliases were retained.
