# Task 015 — Acyclic domain architecture, simulation truth and observer-specific views

**Implemented, built, tested, committed and pushed after the owner's explicit “Please push” instruction.** No force push or compatibility layer. The completed state follows the latest Task 015 revision fetched from Docs (`1c53dfd9c466c8c28f9a9910a6ebb611006e515c`).

## Synchronization and preservation

All eight affected repositories were inspected before synchronization. Docs had diverged from its fetched `origin/main`; I preserved both histories with a normal merge (`835096d587b99c03152bd2e1e075d777ce2af8fe`) rather than reset, rebase or overwrite. The other affected remotes were fetched and already contained the local implementation bases. Earlier local Task 015 commits remain ancestors of the final heads; the Exact cutover removes their obsolete types at the current tree. Build.CMake was unchanged.

The latest Domain.Api TODOs were read directly. They called out duplicate equipment operation semantics and capability indices, numeric/resource accounting ambiguity, `OwnUnitId` versus `TrackId`, weak catalog IDs, units/directions, ambiguous observation provenance and timing, duplicated physical traits, platform classification, Facility/Stock ownership, and eligibility embedded in units. The superseded public types and semantics are removed below. Task 012/013 evidence and the Task 014 report remain historical; the older Task 012 domain proposal is marked superseded, not rewritten.

While preparing publication, uncommitted user work appeared in Domain.Api for `Factions.Api`, `UnitInstance::Faction`, and a Factions target. It conflicts with Task 015's World-owned relationship overlay and exact populated-domain graph. I preserved it in stash `5a43cb9baf3743727cf522c41b080d135edcae90`; it is not applied, committed or pushed. No user edits were discarded.

| Repository | Synchronized pre-Exact local head | Fetched remote baseline | Exact cutover commit |
|---|---|---|---|
| Docs | `835096d587b99c03152bd2e1e075d777ce2af8fe` | `1c53dfd9c466c8c28f9a9910a6ebb611006e515c` | The normative docs, report and index are in the Docs commit that contains this report; its hash is reported in the delivery message. |
| Domain.Api | `21951da22328695b1435f4695e8397ed71a12ff2` | `2f7148541cfe0fa922427f999d229b0817b84d5b` | `d2bc2d7fcfd48c9ad43d12e364be1d16ac5b5f0e` |
| Gateway.Api | `03c2e6be6a806662e95a904182e581450134d897` | `4bed3d2f6bcfe23389bf1cbc6b545710ace3ab65` | `6a05257461264fd635a895796545e0a6052e5380` |
| Gateway.Grpc.Api | `93bf73fc198f9fd4b28121a3339a12324b4428f3` | `2cd297a72ee3d7ea5e9f77fee6eaace3f22cc383` | `08d3507a2bec1218c5bcebbdfa1ffb0c52d9622a` |
| Gateway.Grpc | `9516a09c200cf953fdb326c11c84490f913b7795` | `221de8f41c6dec8a0783064cea9683ac03caf1bb` | `6899d4956ac0c0d6926d1afb98122f035362d05c` |
| SeaPower.Client | `e4f19a5f2fd66f6440380a1d3eb3b8de1bd64d6b` | `88c6a4875a007056df6fd3ddb073b85758b80961` | `a031c3fb57185ecbb24e0e7bb120087ad6d701ae` |
| SeaPower.Mod | `a6e48097bc49a9f41d7f015a554411f5716d7fc8` | `9604c32b29d5e78d26f6218fc8fa0eb2d30b5031` | `b55251d375d702524254b1df613c1da5d2c2e9c0` |
| Build.CMake | `477d8055a366731839b42a4b73e913e0968301f9` | `477d8055a366731839b42a4b73e913e0968301f9` | Unchanged |

The local implementation commits preceding these cutovers are retained as history; the push was a normal fast-forward and did not rewrite that history.

## Domain ownership and enforced dependency graph

Seven domains have populated header-only INTERFACE targets inside the existing Domain.Api repository. Environment.Api remains a reserved boundary because no source-verified environment contract is available; it has no placeholder target or fabricated data.

| Domain | Unique responsibility | Actual direct CMake/header dependencies |
|---|---|---|
| `Physics.Api` | Strong physical quantities, position and geometry | None |
| `Uncertainty.Api` | Minimal two-state `Exact<T>` | None |
| `Catalog.Api` | Definitive unit/equipment/resource definitions, IDs, effects and operations | Physics |
| `Units.Api` | Authoritative `UnitId`, `UnitInstance` and `EquipmentInstance` | Catalog, Physics |
| `Observations.Api` | Observer IDs, perceived entities, partial kinematics, measurement time and contributors | Catalog, Physics, Uncertainty |
| `Orders.Api` | Requests, results and observer-scoped availability overlay | Observations, Physics |
| `Environment.Api` | Reserved for verified environment facts | Not populated; allowed future edges are Physics, Uncertainty |
| `World.Api` | Separate snapshots and truth/observer faction and task-force relationship overlays | Units, Observations, Physics, Uncertainty |

Uncertainty.Api is allowlisted for Orders.Api if a future exported header actually needs it, but current Orders headers do not use it, so no unnecessary target link exists. Environment.Api is similarly allowlisted and tested while unpopulated.

`Domain.Api/test/CheckArchitecture.py` compares every public-header include edge with the raw direct `INTERFACE_LINK_LIBRARIES` exported by `ExportDomainGraph.cmake`; it checks namespace/header ownership, populated-target parity, the allowlist and cycles. `ArchitectureTests.py` configures the real target declarations and verifies forbidden includes/links, cycles, missing edges, aliases, external/generator-expression links, relative and macro includes, and the reserved Environment boundary. The check is in CTest; it does not rely on a shared include root or documentation alone.

## Semantic decisions and deleted concepts

### Truth, observations and projectiles

`Units.Api::UnitInstance` and `EquipmentInstance` express definite authoritative facts only. They contain no `Exact<T>`, observer identity, faction/task-force membership or order eligibility. Catalog type and equipment inventory are optional when unrepresented; a present equipment vector is the definite current inventory. An independently simulated projectile can have a `UnitId` and kinematics without a catalog type, platform, crew, aircraft, hull or AirOperations state.

`Observations.Api::UnitObservation` and `EquipmentObservation` are distinct per-observer structures keyed by `ObservationId`, never true `UnitId`. False, duplicated, fused and ambiguous contacts need no 1:1 truth entity. Friendly and hostile observations use the same ID type. SeaPower own and plotting contacts keep separate source maps; operational IDs do not expose native or privileged identity.

`Uncertainty.Api::Exact<T>` is optional-backed with only Known(value) and Unknown; Unknown is the default. Estimated, confidence, source-string and generic provenance states are gone. `ObservedPosition` is a single mutually exclusive variant of `Exact<Physics::Position>` or `PartiallyLocalizedPosition`. The latter has independently optional latitude/longitude and preserves a source-backed error ellipse's major/minor axes and orientation. Own source positions use the exact alternative; plotted contact centers use the partial alternative. Exact speed, heading, course, name, type and measurement time are only asserted when wholly known.

`ObservationContributor` contains an observer-visible `ObservationId` for a contributing observer unit and typed sensor IDs. It never references `Units.Api::UnitId`. Lists are only reported contributors/sensors, not exhaustive claims. SeaPower's aggregate plot adapter cannot establish the mapping from plotted contacts and contributing sensors to individual observer-unit handles, so contact contributors and measurement times remain absent instead of using fabricated source strings, native target IDs or cache paths.

### Orders, World and air inventory

Orders are not UnitInstance fields. `OrderAvailability` is a separate observer/session/version-scoped overlay keyed by `ObservationId`, with typed order kinds and per-row Unknown/Blocked/Unsupported/Executable states. Rows are a plain reported list; a missing row is never executable. Requests contain one typed route or launch payload, and the provider rechecks current authority, native protections and operation preconditions.

World contains separate `SimulationSnapshot` and `ObserverSnapshot` values. Authoritative faction/task-force memberships use `UnitId`; observer-known affiliations use `ObservationId`. `RecordedSnapshots` can hold privileged truth, perspectives and optional many-to-many associations. The internal `recording.proto` can also express decisions/outcomes; it is not imported by public provider/RPC schemas and provides no deterministic replay claim.

Air operations are per applicable unit, not public Facility/Stock entities. Air inventory is grouped by aircraft type and squadron with one optional count and separately reported loadout choices. Empty item/choice lists do not claim exhaustive absence. Launch availability is only in Orders.Api; spawned results carry `SpawnedObservationId`.

The previous `Knowledge.h`, `ObservedCollection.h`, `ObservationMetadata.h`, coverage enums/messages, generic estimate/provenance fields, `TrackId`, `OwnUnitId`, `Subject` identity model, duplicate public Facility/Stock collections, and order-eligibility components were removed. The canonical protobuf is a breaking schema version 4 replacement; no compatibility wrapper, alias or parallel legacy projection remains.

## Canonical wire and consumer migration

`world.proto` and public service/provider contracts contain observer snapshots and observations only. Privileged `UnitId`, simulation snapshots and truth-observation associations live in `recording.proto`. Optional protobuf presence distinguishes Known(0)/Known(false)/Known-empty route from absent Unknown values. The position oneof distinguishes exact position, partial localization and unknown. Repeated observation and eligibility rows mean only currently reported items.

Gateway.Api C++ codecs and C# conformance fixtures; Gateway.Grpc.Api service, CLI and movement/launch smoke paths; Gateway.Grpc server/mock; SeaPower.Client publisher, source capture and managed tests; and SeaPower.Mod native bridge, ABI and managed transport tests were migrated. Schema version remains 4; generated bindings remain ignored build output.

## Verification

Windows x64 Visual Studio 2026 Insiders/MSVC 19.51.36260.0, CMake/CTest 4.2.2, Ninja, .NET SDK 10.0.401 and protoc 33.4. Builds used the `windows-msvc-release` preset, VS x64 Developer Shell, local Build.CMake checkout and the existing `x64-windows-static` prefix. Protobuf and Zlib package paths were supplied explicitly after an earlier configure attempt outside the Developer Shell invalidated the cached package hints; the successful builds used `Protobuf_DIR` and, for gRPC consumers, the installed static `zs.lib`. These were incremental builds in existing ignored output directories, not clean builds.

| Repository | Build | CTest |
|---|---|---|
| Domain.Api | Passed | 71/71 |
| Gateway.Api | Passed | 4/4 (`codec`, domain transitive includes, managed conformance, canonical schema) |
| Gateway.Grpc.Api | Passed | 1/1 |
| Gateway.Grpc | Passed | 1/1 |
| SeaPower.Client | Passed | 1/1 managed provider suite, including route, launch and lifecycle regressions |
| SeaPower.Mod | Passed | 2/2 (`host-abi`, `managed-runtime-harness`) |
| **Total** | All six affected build trees succeeded | **80/80 CTests passed** |

Actual mock-host/CLI exercises passed: `--smoke` emitted `SMOKE_PASS` after discovery, valid route execution, stale/unauthorized/invalid rejection and status polling; `--movement-smoke` emitted `MOVEMENT_ROUTE_PASS` after replace/append/route-observation checks; `--blocked` returned `REJECTED` with `protected_native_action` and left the observed route unchanged. No live Sea Power game or vendor binaries were run.

## Remaining source limitations

No unresolved domain-ownership decision remains. Environment.Api stays reserved until verified environmental data exists. The current SeaPower aggregate contact adapter cannot establish the mapping from plotted contacts and contributing sensors to individual observer-unit handles or a contact measurement epoch, so the new typed contributor and measurement fields stay empty/Unknown. Task 015 did not add projectile capture/orders, a full authoritative simulation feed, equipment extraction, catalog RPC, recorder/replay storage or deterministic replay. Offline launch lifecycle and native-protection regressions pass; live-game launch behavior was not exercised.

Task index updated. Implementation and Docs commits were pushed normally only after the explicit owner request; no force push. The preserved Factions.Api work remains in the local stash listed above.
