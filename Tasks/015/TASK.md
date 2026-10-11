# Task 015 — Acyclic domain architecture and semantic refactor

**Status:** Assignment ready for Codex; implementation pending. **This task supersedes Task 014's accepted *type layout* wherever specified below.** Preserve Task 012–014 findings as historical evidence, not as an obligation to keep their class names or wire shapes.

## 0. Synchronize before doing anything

Inspect branches and working-tree status; safely **pull/fast-forward** all impacted repositories before planning changes:

- `NavalCommand/Docs` (**reread this newest task after pulling**)
- `NavalCommand/Domain.Api`
- `NavalCommand/Gateway.Api`
- `NavalCommand/Gateway.Grpc.Api`
- `NavalCommand/Gateway.Grpc`
- `NavalCommand/SeaPower.Client`
- `NavalCommand/SeaPower.Mod`
- `NavalCommand/Build.CMake` if required for builds

The owner pushed additional TODO comments to Domain.Api **after** Task 014. Read those TODOs **in source** and inspect the latest code; do not design against the Task 014 completion report alone. Do not discard, reset, overwrite or rebase local user work. Stop and report conflicts rather than forcing synchronization. Record exact base commits in the report.

Read `Docs/AGENTS.md`, `ARCHITECTURE.md`, `MODULE_STRUCTURE.md`, `CODE_STYLE.md`, `CodeStyle.xml`, `LOCAL_SOURCES.md`, and Task 014's report for context. The domain-ownership graph and decisions in **this** assignment are the new intended architecture; change outdated normative documentation as part of implementation.

## 1. Objective

Task 014 introduced too many overlapping abstractions, repeated semantic state, and legacy assumptions. Refactor to a **small set of independently meaningful concepts**, segregated into acyclic domain namespaces/targets **within the existing `Domain.Api` repository**. Then refactor the canonical protobuf and all consumers to that architecture. There are **no pre-release backward-compatibility obligations**; remove obsolete types/fields and avoid wrappers, aliases or double representations merely for compatibility.

**Deliverables:** (a) coherent domain definitions, (b) enforced directed dependency boundaries, (c) updated single canonical wire contract, (d) synchronized consumers with existing operational functionality, (e) architectural/behavioral tests, (f) documentation and a candid report.

Do not turn this into a generic ECS engine, reflection-based component bag or comprehensive physical simulation. Typed composition through focused aggregates/optional components is sufficient.

## 2. Normative domain boundaries

Use these namespace and CMake-target ownership boundaries (the spelling `Physics.Api` is the working default; document a compelling semantic alternative if needed, but do not stall the implementation over the name):

| Domain | Owns | Must not own |
|---|---|---|
| `Physics.Api` | Physical quantity/value types (e.g. distance, speed, mass, angles, durations), geographic `Position`, geometry and coordinate semantics | Naval units, sensors, commands, uncertainty |
| `Uncertainty.Api` | Generic `Knowledge<T>` and `ObservedCollection<T>` and generic uncertainty/coverage helpers | Native tracks, particular sensors, catalog definitions, order policy |
| `Catalog.Api` | Authoritative session-context `Catalog`, strong catalog IDs, `UnitType`, `Loadout`, `EquipmentType`, operations/effects, resource definitions and definitive nominal fits | Runtime instance state, observer uncertainty, orders |
| `Units.Api` | Stable public `UnitId`, `UnitInstance`, equipment capability instances, kinematics, navigation, flight and attached air-facility/air-operations inventory components | Orders, order eligibility, factions/task-force ownership |
| `Orders.Api` | Typed order requests/payloads/results, descriptions, per-unit order-availability **overlay**, execution lifecycle | Unit type or unit state ownership, faction membership |
| `Environment.Api` | Terrain, bathymetry, atmosphere/weather/ocean data and eventual environment observations | Unit/order implementations, fictitious current readings |
| `World.Api` | World snapshot aggregation, session metadata, simulation clock, factions/task forces and unit-membership **overlay** | Equipment implementation, embedded order eligibility, duplicate unit models |

**Allowed direct dependencies only** (arrow means depends on):

```text
Physics.Api      -> (none)
Uncertainty.Api  -> (none)
Catalog.Api      -> Physics.Api
Units.Api        -> Catalog.Api, Physics.Api, Uncertainty.Api
Orders.Api       -> Units.Api, Physics.Api, Uncertainty.Api
Environment.Api  -> Physics.Api, Uncertainty.Api
World.Api        -> Units.Api, Environment.Api, Physics.Api, Uncertainty.Api

Gateway.Api (outside Domain.Api) may depend on World.Api, Orders.Api,
Catalog.Api and whichever other domain targets its contracts need.
```

Transitive dependencies follow from those edges. **No reverse edges or cycles.** In particular:
- **Units must not import Orders**. Orders are an overlay keyed by a unit ID and query/observation context.
- **Units must not import World**. Faction and task-force memberships are a world-level overlay keyed by unit IDs.
- **World must not import Orders**. Domain snapshots and order discovery may be combined at the gateway/composition layer, not by violating domain dependencies.
- **Orders imports Units** for `UnitId` and, where truly needed, read-only unit concepts. It must not invent another own-unit ID namespace.
- **Catalog must not import Uncertainty**. All exposed catalog fields are certain by definition, though deliberately selective.

Namespace paths must mirror `public/NavalCommand/<Domain>/Api/`; provide independent header-only `INTERFACE` CMake targets for **populated** domains, with dependencies exactly declared. Treat `Environment.Api` as a **reserved ownership boundary** if no supported environment data exists yet; don't invent bogus environment facts or meaningless placeholder data just to create a header or target. Still enforce the reserved boundary in the dependency checker. Do not introduce another Git repository. Avoid dependency inversions through aliases or re-exported duplicate types.

## 3. Specific design corrections required

### 3.1 One public identity and observer-safe control

- Define a single strongly typed `Units.Api::UnitId` for every reported `UnitInstance`. It is a NavalCommand-owned **observer/session-scoped observation identity**, not a native Sea Power object identifier or a guarantee the record represents exactly one real platform (phantom/fused contacts are possible).
- **Remove public `TrackId`, `Orders.Api::OwnUnitId`, `Subject` and own-vs-track tagged wire IDs**; don't merely rename them. Internal Sea Power plotting-table/contact structures may remain where they genuinely reflect native mechanics, but they must not leak into public domain identity.
- Order requests refer to `UnitId` directly. **Authority is separate from identity**: order availability is supplied by `Orders.Api`; dispatch revalidates faction control/membership and native safety near execution. An enemy `UnitId` must never authorize an order merely by being syntactically valid.
- Preserve stable handles across observations within the same session where the adapter can genuinely identify the same record. Do not leak hidden enemy truth or automatically merge contacts using privileged identity. Explicitly invalidate handles on session change.

### 3.2 Composable capability-oriented equipment

- `Catalog.Api::EquipmentType` describes **one independently addressable operational capability**, potentially producing several *effect descriptions* and supporting several operations. It is not a compulsory representation of one physical device.
- Simplify the `EquipmentCapability` vs `EquipmentOperation` overlap. Treat damage/sensing/jamming/deception/storage/etc. as **what a capability does**, not a second public capability identity. **Remove `CapabilityIndex`**, which currently indexes a parallel vector without adding useful identity.
- Normalize the current action distinctions: **Use** (one invocation, e.g. launch, deploy or jettison) versus **SetActive** (desired on/off or meaningful operating state). Do not encode `Launch/Deploy/Jettison` as separate fundamental actions with the same resource/effect semantics. Allow distinct operations by ID only when their targeting or behavior genuinely differs.
- Remove redundant `Duration` when it merely restates action. **The lasting effect of an expendable deployment is a different question** (sonobuoy listening after deployment) and must not be silently equated with invocation duration.
- Remove `EquipmentOperation::Mode::{Active,Passive,Guided}`: sensor emission and projectile guidance are different properties. Only reintroduce **specific** typed traits where there's a real modeling use.
- Targeting describes accepted designations (track/unit ID, geographic point, area, bearing, none, etc.) in one meaningful typed contract. A no-target operation is not a bogus required `None` target parameter.
- `Units.Api::EquipmentInstance` is the **runtime operational capability**, with applicable activity/readiness/resource observations. Do not make a public instance correspond obligatorily to a physical launcher/hardpoint.
- Physical characteristics are a separate dimension with clear ownership and coordinate frames: intrinsic capability constraints versus per-loadout installation geometry versus runtime physical observation. **Do not duplicate an ambiguous `PhysicalTraits` aggregate in `EquipmentType`, `EquipmentFit` and `EquipmentInstance`**. Introduce only traits required now; defer unused physical structures.

### 3.3 One resource semantics model

Current `ResourceQuantity::Kind::{Ammunition,DeploymentCharges,JamChannels,FuelLiters}` conflates identity, numeric units and accounting rules. Replace with:
- Definitive catalog resource identity/definition (`ResourceId` with **numeric representation**, **measurement unit** and **accounting semantics** such as consumable/depletable versus reserved/allocatable). Do not force jam channels into ammunition or fuel into a charge count.
- Shared strongly typed resource quantity/value shapes usable at the **distinct relationships**: nominal capacity/initial fit, operation consumption/reservation and current instance inventory. Relationships must remain named and meaningful; don't create three duplicate resource type systems.
- Integers for discrete counts and continuous values for appropriate quantities; never silently use a floating point number for all discrete ammunition counts.
- Runtime resource collections have **explicit unknown/partial/complete coverage** or a rigorously equivalent known-type contract; an empty vector must not silently assert a confirmed lack of resources. Distinguish `Known(0)` from `Unknown` and `not applicable`.

Do not implement an accounting engine or fuel network; this is a domain definition.

### 3.4 Unit composition and air operations

- Keep `Units.Api::UnitInstance` as one observer-facing shape with typed components: identification, kinematics, equipment, optional navigation/flight/air operations, and other truly independent facts. No ECS runtime or meaningless one-field wrapper layers.
- `Units.Api::UnitInstance` must **not** embed order eligibility or task-force membership. `Orders.Api` owns a per-unit `OrderAvailability`/discovery overlay; `World.Api` owns faction/task-force membership relationships. Avoid manufacturing faction assignments not supported by the provider.
- Replace the duplicate `NavigationState.Inspected` + `Knowledge<vector<RoutePoint>>` relationship. `Known([])` is an inspected, known-empty route; `Unknown` means uninspected/unknown. If a distinct inspection flag has genuinely independent operational semantics, explicitly prove that with tests/documentation before retaining it.
- Add meaningful observed heading/course if available, but do not invent the simulator reading. Heading and course-over-ground must not be conflated; support strongly typed SI/angle values. Geometry and `Position` belong in `Physics.Api`.
- Collapse public `World.Api::Facility`, generic `Stock`, and snapshot-level `facilities` into an optional **`Units.Api::AirOperations` component** on applicable carrier/airfield/helicopter-capable `UnitInstance`. Clarify names (e.g. `AircraftInventory`, `AircraftInventoryEntry`), eligibility distinct from inventory, observation age, counts and lifecycle status. Use source-backed existing semantics only. The same unit ID identifies the owning air-operations facility. Never add a second public entity for an airfield that is already a unit.
- `PlatformDescription` currently contains empty alternative structs and treats helicopter as disjoint from aircraft. Prefer a small authoritative platform classification with meaningful subtype/typed performance traits only where needed. A helicopter **is** aircraft; do not add complexity to encode fake exhaustive class hierarchies.

### 3.5 Evidence, uncertainty, and order contracts

- Keep `Uncertainty.Api::Knowledge<T>` and `ObservedCollection<T>` (with default-unknown and invalid Unknown+nonempty rejection) as genuinely reusable, transport-agnostic primitives; avoid multiple alternative wrappers for the same knowledge state.
- **Snapshot capture time** belongs to `World.Api` snapshot metadata. **Last measurement time** belongs with a unit/field observation only if known. Don't assert that snapshot time was when a sensor measured an enemy track. Likewise, avoid duplicating a generic provenance string in both estimate and top-level metadata; distinguish source identity, source class and provenance trace when genuinely available. A hard-coded adapter cache path is not a sensor identity.
- Strong identifiers: `Catalog.Api::UnitTypeId`, `EquipmentTypeId`, `LoadoutId`, `ResourceId`; `Units.Api::UnitId`; other IDs only with a demonstrated need. Make public fields follow `CodeStyle.xml` conventions. Use focused physical units in `Physics.Api` without inventing a generic measurement/units framework.
- **Orders.Api** request must hold **exactly one** typed order payload (`std::variant` or equivalent), replacing two independent optional `routeCommand` and `facilityLaunch` fields. `Eligibility.kind` cannot remain an undocumented arbitrary string; associate availability with a well-defined `OrderKind` or typed order descriptor. Avoid duplicate eligibility in air-inventory launch options; preserve dynamic reasons, unknown/blocked/unsupported distinctions and protection against stale dispatch.
- **World.Api** aggregates units, session state, simulation time, faction/task-force membership if known, and environment data as applicable. **Do not** have it own unit internals, orders or `Catalog.Api` definitions by copying their fields.

## 4. Canonical wire and consumer migration

Change `Gateway.Api` canonical protobuf schemas **directly** to express the revised domain. No backward compatibility, old Subject/Track/OwnUnit tags, old unit/facility split, legacy projected fields or parallel model. Follow existing repository/schema ownership, regenerate both C++ and C# bindings from the canonical source and migrate all callers.

Ensure that:
- Wire `UnitId` is identical for observations, order references and any air-operations owner reference.
- Separate per-unit order-discovery/availability results are **not** fields of `UnitInstance` or `WorldSnapshot`. Do not force `World.Api` to depend on `Orders.Api`.
- Faction/task-force observations (where currently supported) remain in the World-level relationship overlay. Do not invent an enemy faction from inaccessible simulator truth.
- Unknown/estimated/known values, measured-versus-snapshot timestamps, `Known(false)`, `Known(0)`, complete-empty and unknown collections survive through protobuf without false certainty.
- Existing route/waypoint and aircraft-launch functionality remains operative via new requests/response shapes; permission checks, side filtering, native automation protection and game-thread revalidation remain intact.
- `Gateway.Grpc.Api` service, `Gateway.Grpc` server/mock, `Gateway.Api` native codec, `SeaPower.Client` managed wire publisher, and `SeaPower.Mod` native/managed bridge and tests use the **new** contract. Use direct changed types, not transitional replicas.
- Existing simulator-native contact/track capture types may remain *inside the backend* when needed to represent how native sensors work. They must not be public NavalCommand IDs, a second operational schema, or a mechanism to reveal authoritative enemy state.

**Feature scope:** this is a refactoring, not catalog retrieval implementation, equipment extraction, radar/engagement calculations, faction AI or new environment simulation. Do not add RPCs for unimplemented features. No live Sea Power runs, loader installation, vendor binary changes or deployment changes without separate authorization. Domain and mock/managed offline tests are in scope.

## 5. Dependency enforcement (essential deliverable)

- Exported headers must be placed under the owning namespace directory and compile **independently** when included directly.
- Add a **maintained automated architecture check** run via CTest/CI that verifies both **direct public header include dependencies** and **CMake target dependencies** against the exact allowlist in §2, detects cycles and forbidden `Units → Orders` / `Units → World` / `World → Orders` edges, and reports an actionable file/target on failure.
- `INTERFACE` target links must represent actual dependencies. Merely setting a shared root include path on every target does **not** enforce architectural segregation. The architecture check must reject an otherwise compiling forbidden cross-domain include.
- Tests link individual `Physics.Api`, `Uncertainty.Api`, `Catalog.Api`, `Units.Api`, `Orders.Api`, `Environment.Api` (if populated), and `World.Api` targets, proving independent consumption and correct transitive linking.
- Update `Docs/ARCHITECTURE.md`, `MODULE_STRUCTURE.md`, and `AGENTS.md` where they still mandate `World.Api → Orders.Api`, split Subject IDs, embedded order eligibility, or separate facility collections. Include a concise explicit dependency table/diagram and source ownership rules. Do not rewrite historical investigation evidence or the Task 014 report.

## 6. Verification scenarios and acceptance criteria

Create focused tests, **not only synthetic codec round trips**:

1. **Identity and authority:** same public `UnitId` path for own units and contacts, same value type through observation and orders; enemy order rejected after current-side validation, including reused/stale IDs; no `TrackId`/own-unit public enum variants.
2. **Observation semantics:** `Known([])` versus `Unknown` routes; complete-empty versus unknown/partial equipment and runtime resources; `Known(0)` versus unknown count; zero/false survive both languages; measurement age is never fabricated from snapshot capture time.
3. **Equipment semantics:** guided missile, radar, active ECM, chaff, sonobuoy, external tank; one equipment capability can have multiple effects/operations without parallel capability indexes; clear Use versus SetActive and resource depletion versus channel allocation.
4. **Catalog:** two resolved loadouts and shared types, authoritative definitive catalog fields, strong IDs, no catalogue uncertainty or provider-specific service-year schema.
5. **Air operations:** carrier/airfield unit with attached inventory, known/unknown stock, no duplicate public facility, typed launch order and separately discoverable availability; existing launch lifecycle and safety semantics unchanged.
6. **World relationships:** faction/task force overlay, when represented, references `UnitId` without creating a dependency from Units to World; no guessed enemy affiliation.
7. **Architecture:** deliberate bad `Units → Orders` include/target edge must be rejected by architectural checker; no cycles; standalone headers/targets compile.
8. **System:** affected native and managed builds, conformance tests, mock-host/CLI route and order discovery behavior, and offline SeaPower.Client provider/fake-game tests pass. Record any failing/unsupported checks honestly; do not claim live-game verification.

Completion means:
- [ ] All populated domain types live under the correct new namespaces with acyclic, enforced dependencies.
- [ ] One owner per concept; no duplicate public identity, resource types, equipment capability identity or eligibility state.
- [ ] Orders and World faction memberships are overlays, not UnitInstance components.
- [ ] No public TrackId, OwnUnitId, Subject, Facility/Stock world split or vestigial old World.Api type aliases.
- [ ] Single revised canonical wire schema with all affected consumers migrated; no compatibility wrappers.
- [ ] Above semantic and architecture tests run and reported.
- [ ] `Docs/Tasks/015/REPORT.md` records actual changes, design choices/alternatives, removed types, dependency graph, repo commit IDs, build commands/results, known limitations.
- [ ] Docs task index updated to reflect outcome; meaningful changes committed **locally per repository**. **Do not push** without owner approval.

**Do the work in this assignment now**; no separate redesign-approval loop is required. If a named requirement creates a genuine contradiction with proven simulator safety behavior, choose the smallest safe, clean design, document the evidence and deviation, and report it explicitly instead of adding a silent workaround.
