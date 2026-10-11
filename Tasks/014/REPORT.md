# Task 014 — Composed domain and canonical wire migration

**Completed locally on 2026-10-11; awaiting owner review. Nothing pushed.** The revised pre-release specification and AGENTS.md at Docs `7a1cdb6` govern this implementation. All six functional repositories are migrated together. No game was launched, no plugin was installed or activated, and no original game file was changed.

## Synchronization and local commits

All checkouts were on `main`. Status was inspected before `git pull --ff-only`; Docs fast-forwarded to the revised assignment and the other repositories were already current. The unfinished Domain.Api edits from this chat were retained. There were no user-work conflicts, resets, forced updates or rebases.

| Repository | Synchronized starting revision | Local implementation commit |
|---|---|---|
| Docs | `7a1cdb6d303506ce7c6f7ee1196230ea9ae3995f` | The commit containing this report |
| Domain.Api | `0e49e27ec2173e47669ac25a988806f550c74c50` | `e51fab8b253d53f389ad65d1bf6b56c63d544051` |
| Gateway.Api | `d1c462e486aa6a1cc4a29373ab9bd7e0b9e8207e` | `4bed3d2f6bcfe23389bf1cbc6b545710ace3ab65` |
| Gateway.Grpc.Api | `a238d8c64d7d7d02c43abd25ec6f53878ff1c71a` | `2cd297a72ee3d7ea5e9f77fee6eaace3f22cc383` |
| Gateway.Grpc | `22e9abd98e7d2b43e3c5af30d1b048f518665042` | `221de8f41c6dec8a0783064cea9683ac03caf1bb` |
| SeaPower.Client | `f4a685673d7c4a27c41a3a0e6e694d977ff11943` | `88c6a4875a007056df6fd3ddb073b85758b80961` |
| SeaPower.Mod | `d95e7cdeb2708589b3f975f69403b34997563319` | `9604c32b29d5e78d26f6218fc8fa0eb2d30b5031` |
| Build.CMake | `477d8055a366731839b42a4b73e913e0968301f9` | Unchanged |

Review and build the complete local graph together. The existing ignored `Build.Local.cmake` registrations resolve the canonical dependency URLs to these checkouts; no implicit sibling resolution or committed machine paths were added.

## Domain decisions

The existing header-only `World.Api` target owns the catalog and observation contracts. A separate `Catalog.Api` target would not currently isolate an additional dependency. `World.Api` still uses only `Orders.Api`; domain headers have no transport, simulator or third-party dependencies.

`EquipmentType` composes typed sensing, damage, jamming, deception, communication and storage capability variants. Separate operations identify the capability, target inputs, invocation, duration, supported modes and resources. Costs represent consumption for one-shot operations and occupancy while continuous operations run. Physical arcs and orientation are separate optional traits. Equipment instances represent operational capabilities, not mandatory physical devices.

`Catalog` contains a resolved-context revision, platform-tagged unit types (including helicopters), alternative loadouts and shared equipment definitions. All definition values are definitive. Optional traits mean unmodeled/not applicable; exhaustive empty collections mean empty. Source-date resolution and required-resolution failures belong to the future adapter/retrieval operation. No catalog knowledge states, public year model, extra configuration layer, equipment root hierarchies, inheritance framework or ECS was introduced.

`Knowledge<T>` privately stores Known, Estimated with source, or Unknown. Unknown is the default and has no readable value. `ObservedCollection<T>` defaults to Unknown empty and rejects Unknown nonempty and invalid coverage. Its members cannot be appended through a mutable vector reference. Partial empty means incomplete enumeration with no reportable members. Missing applicable components and unknown values within present components remain distinct.

The sole world entity is `UnitInstance`; `View::Instances` contains both controlled units and observer contacts:

| Responsibility | Component |
|---|---|
| Scoped identity and authority | `Subject`: `Orders.Api::OwnUnitId` or opaque `TrackId` |
| Name and catalog type identification | `IdentityState` |
| Observed coordinates, positional error ellipse and speed | `KinematicsState`, `PositionObservation` |
| Inspection state and current route | Optional `NavigationState` |
| Implemented orders and coverage | Optional `OrderEligibilityState`, reusing Orders.Api eligibility |
| Explicit airborne false/true/unknown | Optional `AircraftState` |
| Capture/observation time and provenance | `ObservationMetadata` |
| Observed capabilities and per-attribute state/resources | One `ObservedCollection<EquipmentInstance>` |

Own-unit identity is owned by Orders.Api and reused by the world subject variant. `Orders.Api::Request::Subject` accepts that strong type; `TrackId` cannot be assigned to it. This keeps orders independent of world headers and makes the authority boundary explicit.

## Canonical wire and consumer migration

The old domain Unit/Track headers and old Protobuf Unit/Track messages, `own_units`/`tracks` fields and their mappings are removed. WorldSnapshot has one `instances` collection. Typed component messages mirror the observation model directly. Knowledge uses oneofs, including explicit presence for estimated scalar values; unset state means Unknown. Route messages preserve known-empty routes, and coverage preserves known-empty equipment versus unknown inventory. Coordinates require explicit latitude/longitude presence. Estimated values retain their sources and positional uncertainty. Invalid subjects, contradictory coverage and estimates without values are rejected.

`observation.proto` owns common coverage and subject tags. Order requests and discovery queries carry the same subject tag. Track-tagged commands are rejected even if the text equals an own-unit ID. Order discovery returns the composed eligibility state including coverage, using the same codec as world snapshots. Missing/unknown eligibility never becomes executable.

The existing RPC operations still serve their meaningful roles: connect, observe, discover, submit and inspect status. Their imported request/response contracts are intentionally changed; their operation names need no additional redesign. Canonical schema version is now **2**, with no schema-1 fallback. Native host ABI version remains 1 because its byte-buffer/lifecycle ABI is unchanged. Idle-only waypoint semantics remain a supported safety behavior and are now named `IdleWaypoint` / `IDLE_WAYPOINT`, without a compatibility-labelled mode.

Gateway.Api generates native and managed bindings from the same sources and provides lossless component codecs. Native code generation and service code generation track imported canonical proto dependencies. The obsolete compatibility golden-file check is replaced with protoc compilation and architectural checks for unified subjects, knowledge and coverage.

Gateway.Grpc serves the new model, and its mock publishes an owned unit and a contact in the same collection. Gateway.Grpc.Api's CLI selects only own subjects with known positions and reads routes/aircraft state through components. SeaPower.Client publishes generated composed messages directly, preserving route inspection, eligibility, motion, names, timestamps and grounded aircraft. Contacts use only the existing selected plotting-table detected-position cache; their position is marked Estimated and equipment remains Unknown. No class-standard fit is promoted to observed equipment.

SeaPower.Mod's bridge decodes the new model, accepts only own-unit command subjects and rejects invalid composed snapshots without allowing decode exceptions across its C ABI. Thread ownership, immutable queues, bounds, duplicate-request handling, stale-session checks and game-thread revalidation remain intact. Its runtime and harness projects compile against the regenerated managed contract. The Sea Power-specific facade sample records remain capture/test seams for distinct native sources; they are not a second public domain or wire schema.

## Verification

All six repositories were independently configured with `--fresh` and rebuilt with `--clean-first` in Release. After the final shared eligibility-codec refactor and imported-schema dependency adjustment, affected repositories were rebuilt and their tests passed again.

Toolchain: CLion CMake/CTest 4.2.2, Ninja, MSVC 19.51, .NET SDK 10.0.401, source-built protoc 33.4. Native compile commands use `/std:c++latest` and `/MT`; host and target dependencies use `x64-windows-static`. This follows the project's C++26 configuration without claiming full compiler C++26 conformance.

From a VS x64 developer PowerShell, the exact configure/build/test command pattern was:

```powershell
$env:VCPKG_ROOT = 'C:/vcpkg'
$cmake = 'C:/Program Files/JetBrains/CLion 2026.1/bin/cmake/win/x64/bin/cmake.exe'
$ctest = 'C:/Program Files/JetBrains/CLion 2026.1/bin/cmake/win/x64/bin/ctest.exe'
# Run in each functional repository; Domain.Api omits the two vcpkg options.
& $cmake --preset windows-msvc-release --fresh `
  -DFETCHCONTENT_SOURCE_DIR_NCBUILD=C:/Software/GitHub/NavalCommand/Build.CMake `
  -DCMAKE_MAKE_PROGRAM=C:/Software/STM32CubeCLT/Ninja/bin/ninja.exe `
  -DVCPKG_INSTALLED_DIR=C:/Users/DmitriyPC/AppData/Local/Temp/naval-static-runtime-prefix `
  -DVCPKG_MANIFEST_INSTALL=OFF
& $cmake --build o/windows-msvc-release --clean-first --parallel 8
& $ctest --test-dir o/windows-msvc-release --output-on-failure --no-tests=error
# Additional maintained managed targets, in their owning repository:
& $cmake --build o/windows-msvc-release --parallel 8 --target SeaPower.Client.Managed
& $cmake --build o/windows-msvc-release --parallel 8 --target managed-runtime managed-harness
```

| Repository | Results |
|---|---|
| Domain.Api | 2/2 CTests; every public header separately compiled; six equipment fixtures, shared/resolved fits, alternative loadouts, typed subjects, knowledge and collection invariants passed |
| Gateway.Api | 3/3 CTests; 7 codec tests plus independent transitive consumer and C# → C++ domain → C# conformance passed |
| Gateway.Grpc.Api | CLI/service build and 1/1 import test passed |
| Gateway.Grpc | 1/1 CTest containing 5 provider/server tests passed; routes, ledger/session bounds, tag rejection, absent eligibility, partial coverage and malformed provider snapshots covered |
| SeaPower.Client | Native dependency build and managed game-reference build passed; 1/1 fake-provider regression suite passed, including route mutation, native safety rechecks, launch lifecycle, mixed observations and track-tag rejection |
| SeaPower.Mod | Native host, managed runtime and managed harness builds passed; 2/2 CTests covering host ABI/queue/session behavior, malformed snapshots and managed runtime/harness regressions passed |

The codecs/conformance tests cover all populated instance components: known and estimated identities, coordinates/error ellipse, speed, routes, inspection, eligibility, known airborne false, time/provenance, equipment activation/readiness/damage/resources and optional physical traits. Zero/false values and Complete/Partial/Unknown collections survive without false certainty. A known class with unknown fitted equipment remains unknown. Invalid Unknown nonempty collections and incomplete estimated values fail explicitly.

Additional checks:

```powershell
& Gateway.Api/test/Check-Schema.ps1 `
  -Protoc C:/Users/DmitriyPC/AppData/Local/Temp/naval-static-runtime-prefix/x64-windows-static/tools/protobuf/protoc.exe
& SeaPower.Mod/scripts/Test-Mock.ps1 `
  -HostExecutable C:/Software/GitHub/NavalCommand/Gateway.Grpc/o/windows-msvc-release/app/mock-host.exe `
  -Cli C:/Software/GitHub/NavalCommand/Gateway.Grpc.Api/o/windows-msvc-release/app/naval-cli.exe `
  -OutputDirectory C:/Software/GitHub/NavalCommand/Gateway.Grpc/o/composition-cli-final
# Against a separately started owned mock-host at 127.0.0.1:50053:
& Gateway.Grpc.Api/o/windows-msvc-release/app/naval-cli.exe --endpoint 127.0.0.1:50053 --movement-smoke
```

Results: canonical-schema check passed, ordinary CLI reported `SMOKE_PASS`, route sequence CLI reported `MOVEMENT_ROUTE_PASS`. Both exercised the unified owned/contact snapshot; each mock process was stopped after its check. `git diff --check` passed. Searches found no remaining obsolete public types, fields or mode names in maintained consumers; the schema check deliberately mentions forbidden old field names as negative assertions.

Full local build/test output remains under each repository's ignored `o/composition-*.log`; CLI logs are under Gateway.Grpc's ignored `o/composition-*cli*/`. Managed game-reference compilation reports SDK/Unity assembly-version conflict warnings (3 for the standalone client, 6 for runtime/transitive references, 2 for harness), with zero errors. Native builds report upstream gRPC deprecation and existing JSON-print return-value warnings. These are not claimed resolved. Debug was not validated in this task; the previously recorded Debug teardown issue remains outside this migration.

## Scope and artifact lifecycle

Catalog values are domain-only fixtures/contracts in this task: catalog retrieval and definition transport are deferred with that future feature. Every currently transported instance component is represented losslessly by the new canonical schema. No lossy projection, second schema, deprecated aliases, old-message adapters or migration fallback remains.

V1 deliberately omits detailed platform performance, guidance algorithms, calibrated engagement metrics, EW bands, magazine/launcher partitions, fuel/electric networks and physical-device correlation. No equipment extraction or engagement functionality was added. Observer sample times preserve existing capture-time behavior; actual cached-contact measurement age is not newly asserted.

Production additions are the domain contracts, canonical schema/codecs and migrated consumers. Maintained development additions are the header compilation regression and revised canonical schema check. Synthetic fixtures and fake providers remain automated tests. Disposable migration orchestration scripts were used only in ignored `o/` and removed after completion; generated bindings and build logs remain ignored. Task 013's historical game findings were left intact; the active proposal, architecture and task index now describe the approved model. No installation, packaging deployment or live-game behavior is claimed verified.
