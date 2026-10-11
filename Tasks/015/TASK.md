# Task 015 — Acyclic domain architecture, simulation truth and observer-specific views

**Status:** Implementation assigned; specification revised after owner discussion. **Pull the newest Docs before proceeding.** This task supersedes the *type and dependency layout* described in Tasks 012–014. Preserve their investigation findings as historical evidence. **Breaking changes are required; compatibility with pre-release interfaces is not a goal.**

## 0. Synchronize and read current source

Inspect branches and working-tree status, then safely `git pull --ff-only` (or equivalent safe fast-forward) across all affected checkouts:

- `NavalCommand/Docs` — **pull and reread this file before implementation**, as its scope changed after initial publication.
- `NavalCommand/Domain.Api` — includes the owner's most recent TODO comments, which must be read in source.
- `NavalCommand/Gateway.Api`, `Gateway.Grpc.Api`, `Gateway.Grpc`.
- `NavalCommand/SeaPower.Client`, `SeaPower.Mod`.
- `NavalCommand/Build.CMake` if needed for builds.

Do not reset, discard, overwrite, or rebase unpublished local/user work. Report synchronization conflicts rather than forcing updates. Record precise baseline revisions. Read `Docs/AGENTS.md`, `ARCHITECTURE.md`, `MODULE_STRUCTURE.md`, `CODE_STYLE.md`, `CodeStyle.xml`, `LOCAL_SOURCES.md`, Task 014's report, Task 012/013 game evidence, and every owner TODO in the latest `Domain.Api`.

## 1. Goal and nonnegotiable concepts

Task 014 mixed independent concepts and left remnants of the legacy world model. Refactor its domain model into clean, strongly typed, noncyclic namespaces **inside the existing Domain.Api Git repository**, update the single canonical Protobuf schema, and migrate all affected C++ and C# consumers.

**Crucial change from the initial Task 015: simulation truth and player observation are different domain models.**

- `Units.Api::UnitInstance` and `EquipmentInstance` describe **authoritative simulated entities/capabilities**. Their **expressed fields are definite** and contain **no `Exact<T>`, observational uncertainty wrappers or observer contact identities**. Definitive does not mean exhaustive: deliberately omit unsupported/unmodeled fields.
- `Observations.Api::UnitObservation` and `EquipmentObservation` describe **one observer's perceived state**. Data may be wholly known, wholly unknown, partially localized, incomplete, stale or inaccurate. An observation might describe a false, ambiguous, merged or duplicated contact. **Use `Uncertainty.Api::Exact<T>` only for wholly known/unknown facts; model genuinely partial uncertainty in specialized observation types** such as `ObservedKinematics`.
- `UnitId` (truth) and `ObservationId` (per-observer contact/representation) are **different strong ID types**, not the old own-versus-enemy ID split. Friendly and hostile **observations use the same `ObservationId` type**.
- `Orders.Api` accepts **observation identifiers scoped to the requesting observer/session**, then the authoritative backend checks control permissions and resolves the observed handle safely before issuing real commands. No order can be authorized just because an ID exists or looks like a friendly identifier.
- `World.Api` provides **separate simulation-level and observer-specific aggregate views**; privileged truth must never be accidentally embedded in an ordinary faction snapshot. Multi-faction control uses separate perspectives of the same simulation; privileged replay/ML recording may retain both truth and all recorded perspectives.
- **A `Unit` is a simulated tactical entity, NOT necessarily a crewed platform or vehicle.** Sea Power treats some projectiles, notably missiles, as unit-like simulated objects: they can be tracked and selected and some can receive commands. Such projectiles **must be representable as true `UnitInstance`s and observer-side `UnitObservation`s**. Do not assume all projectiles are separately simulated units or orderable; applicability is determined by the simulation and current authority.

No generic ECS runtime, reflection dictionary, speculative full physical engine, or privilege-bypassing convenience API.

## 2. Domain namespace/CMake target boundaries (acyclic)

All targets reside in `NavalCommand/Domain.Api`; exported public headers mirror their namespace directories:

| Target / namespace | Sole responsibility | Exclusions |
|---|---|---|
| `Physics.Api` | Physical units/quantities, `Position`, angles, velocity/speed, geometry, time values | Units, orders, observation provenance |
| `Uncertainty.Api` | Only the minimal generic `Exact<T>`: exactly `Known(T)` or `Unknown`; no provenance, estimate or collection machinery | Sensors, contact evidence, `ObservedCollection`, partial-uncertainty frameworks |
| `Catalog.Api` | Authoritative scenario-context unit/equipment/resource **definitions**, IDs, loadouts and compatible effects/operations | Runtime state, uncertainty, orders |
| `Units.Api` | **Authoritative** `UnitId`, `UnitInstance`, `EquipmentInstance`, actual kinematics, runtime equipment, optional flight/air-operations state | Observer uncertainty, contact records, Orders, World |
| `Observations.Api` | **Observer-facing** `ObservationId`, `UnitObservation`, `EquipmentObservation`, specialized partially uncertain values (e.g. `ObservedKinematics`), observer-unit/sensor contributors and available measurement ages | True `UnitId`, authoritative state ownership, orders, World |
| `Orders.Api` | Order payloads, requests, results, descriptions and per-observation **order-availability overlay** | Unit or observation type ownership, factions, embedded order eligibility |
| `Environment.Api` | Terrain, bathymetry, weather, ocean/atmosphere information with clearly separated definitive source data vs observer observations when actually supported | Unit/order state, invented simulator data |
| `World.Api` | Session/world snapshot wrappers, simulation clock, factions/task forces, authoritative membership and distinct observer-visible relationship overlays | Unit/equipment internals, embedded order-eligibility types |

**Allowed direct dependency edges only:**

```text
Physics.Api     -> (none)
Uncertainty.Api -> (none)
Catalog.Api     -> Physics.Api
Units.Api       -> Catalog.Api, Physics.Api
Observations.Api-> Catalog.Api, Physics.Api, Uncertainty.Api
Orders.Api      -> Observations.Api, Physics.Api, Uncertainty.Api
Environment.Api -> Physics.Api, Uncertainty.Api
World.Api       -> Units.Api, Observations.Api, Environment.Api, Physics.Api, Uncertainty.Api
```

**No cycles and no additional domain-to-domain includes or CMake links.** Transitive dependencies exist only through allowed edges. `Catalog.Api` and `Units.Api` must not depend on `Uncertainty.Api`; `Observations.Api` must not depend on `Units.Api`; `World.Api` must not depend on `Orders.Api`. `Orders.Api` must not depend on `Units.Api` or `World.Api` (orders operate on observation handles). It is fine for `Gateway.Api` to import several of these APIs to compose boundary contracts.

`Environment.Api` may remain an explicitly reserved boundary with no production target/files if no verified environmental contract exists. Do **not** create meaningless empty components solely to satisfy a namespace list. Enforce even reserved boundaries against future forbidden includes.

Do not create another Git repository. Build targets are distinct header-only `INTERFACE` targets; target links should reflect **actual** allowed dependencies, not an arbitrary umbrella include path.

### 2.1 World views, authority and replay ownership

Use two distinct, clearly named values such as:

```cpp
// Illustrative responsibility, not fixed implementation signatures.
World::Api::SimulationSnapshot   // privileged, definite Units.Api::UnitInstance data
World::Api::ObserverSnapshot     // one faction/perspective's Observations.Api::UnitObservation data
```

Do **not** define one `View` with optional privileged and observer fields. Do not let ordinary `GetView` or faction clients receive a true `UnitId`, a truth-to-contact mapping, hidden equipment/classification, or any other privileged state.

A snapshot must be scoped to its session and simulation time. An observer snapshot additionally identifies its perspective/observer. If `Observations.Api` needs an observer-scoping ID, own a small `ObserverId` there; `World.Api` may associate it with authoritative `FactionId` without creating `Observations → World`.

- **Multi-faction:** multiple independent observer snapshots of the same authoritative simulation state, each with independent knowledge and contact lifecycle; no faction receives the other's hidden truth.
- **ML/replay:** privileged recorder can capture authoritative snapshots, per-observer snapshots, issued commands and outcomes, plus optional **privileged** correlation of `ObservationId` to `UnitId` (which may be zero-to-many/many-to-one or absent due to false/fused contacts). Never put that correlation in observer-facing types. Distinguish recorded-state playback from deterministic engine replay; do not claim deterministic reconstruction solely from snapshots.
- Implement necessary **type contracts and safe separation now**, not a full replay store, ML pipeline, historical database, privileged external RPC or complete simulation capture. Sea Power may not expose every ground-truth field; no pretending missing data is definitive. An authoritative snapshot source remains privileged and may be deferred until verified.

## 3. Required semantic cleanup

### 3.1 Identifiers, entity scope and projectiles

- Replace old public `Orders.Api::OwnUnitId`, `World.Api::TrackId` and `Subject` with distinct `Units.Api::UnitId` (real entity) and `Observations.Api::ObservationId` (observer's potentially ambiguous record). Neither is automatically the game's native ID; adapters own mappings and validity. IDs stay stable during their documented session/lifetime; invalidate at session changes.
- One `UnitObservation` is not guaranteed to correspond to exactly one true `UnitInstance`. Do not invent a direct `ObservedUnitId → UnitId` field in operational observation types.
- A **missile or other independently represented projectile** may become a `UnitInstance` after launch and be an observed target (or controlled object). It may have kinematics, class, guidance status and whatever independent operations the simulator legitimately exposes; no crew, `AirOperations`, hull or aircraft requirement.
- **Distinguish equipped inventory from deployed entities:** a missile carried on a ship or aircraft belongs to a launch/deployment equipment/resource fit. Once independently simulated, the launched missile is an entity with its own `UnitId`; it must **not** be the same object as the launcher's `EquipmentInstance`. Optional authoritative launch-origin/deployed-type relations can be introduced only if needed and source-backed. The catalog may describe projectile unit types without conflating them with launcher equipment types. Do not mandate that every consumable turns into an independently simulated entity.
- Platform classification must not be a hard-coded manned vehicle hierarchy. Avoid variants containing empty `Aircraft`, `Helicopter`, `Submarine` types with artificial disjointness: a helicopter is an aircraft; a missile can fly without being an aircraft platform. Use a small meaningful classification or applicable traits justified by current simulator data.
- Orders for a tracked missile are **supported only when an actual native operation and observer authority allow it**; tracking/selectability does not imply arbitrary commandability. Do not invent missile-specific commands just to demonstrate architecture.

### 3.2 Capabilities, operations, resources and physical constraints

- `Catalog.Api::EquipmentType` represents one independently addressable operational capability, possibly with several **effects and operations**. `Units.Api::EquipmentInstance` represents its definitive current operational state, `Observations.Api::EquipmentObservation` the observer's uncertain evidence about a capability. None is obligatorily a physical weapon mount.
- Remove `CapabilityIndex` and the duplicate identity of `EquipmentCapability` versus `EquipmentOperation`: effects describe *what*, operations *how*. Don't invent a parallel vector of independently indexed capabilities.
- Prefer `Use` (release/launch/deploy/jettison behavior) and `SetActive` (desired on/off or operating state) as fundamental invocation forms. Do not duplicate synonymous launch/deploy/jettison enums. Drop redundant invocation duration; one-shot sonobuoy deployment can result in a sustained sensing effect.
- `EquipmentOperation::Mode::{Active,Passive,Guided}` conflates sensor emission and weapon guidance; remove it. Guidance, emission and any other real mode properties belong in distinct, applicable effect/operation descriptions when needed.
- A targeting contract distinguishes no designation, observation/target ID, position, region, bearing, etc. Do not encode fake `None` as mandatory input; avoid invalid combinations or impossible unverified targeting modes.
- Resource semantics distinguish resource identity from numerical representation, measurement units and accounting behavior (depletable vs reservable). Share authoritative `Catalog.Api::ResourceId/ResourceType` and focused value types; make discrete counts integer, quantities continuous where appropriate.
- The **same resource definition** may appear as loadout nominal full stock, operation cost/reservation or runtime stock/occupancy, but those have **distinct semantic roles**. True instance resource quantities are definitive for expressed properties. An observed amount may use `Exact<Quantity>` if the amount is wholly known or unknown; partial uncertainty requires a **specialized** domain representation. `Exact::Known(0)` ≠ `Exact::Unknown` ≠ inapplicable. Do not use generic collection coverage.
- Physical geometry has explicit context/frame: intrinsic capability limits vs installation-dependent constraints vs actual runtime physical state vs observer measurement. Remove vague triplicated `PhysicalTraits` if it has no unique semantics. Reuse underlying angle/geometry value types.

### 3.3 Minimal Exact<T>, specialized partial uncertainty, and evidence

**Latest owner correction overrides all earlier Task 014 / Task 015 Knowledge and ObservedCollection designs.**

1. **Rename and simplify `Uncertainty.Api::Knowledge<T>` → `Uncertainty.Api::Exact<T>`.** Exactly two semantic states: `Known(T)` contains an entire exact value, and `Unknown` contains no value. Unknown is the safe default. No `Estimated` state, `Source` string, confidence, quality flag or built-in explanation. Use a thin value type backed by `std::optional<T>` (or equivalent) rather than an unnecessary class framework. A value known only approximately must **not** be asserted as `Exact<Position>`.
2. **Partially uncertain information belongs to specialized observation types.** For example, `Observations.Api::ObservedKinematics` may represent whichever position, bearing, speed and uncertainty geometry the observer actually receives. Choose domain-appropriate optional/exact fields and measurement geometry only where verified by the adapter; an uncertain contact center or error ellipse must not be mislabeled an exact position. Do not introduce generic `Estimate<T>`, `MaybeKnown<T>`, an all-purpose confidence wrapper, or comparable generalized uncertainty machinery.
3. **Delete `ObservedCollection<T>`** and the Protobuf `CollectionCoverage`/Unknown-Partial-Complete abstraction. There is currently no owner-verified Sea Power use case warranting it. A plain list of observed equipment **means only items currently reported to that observer**; `[]` means none reported, **not** certainly absent/unarmed or exhaustive. Likewise choose meaningful specialized collection semantics for air inventory or other domains only when demonstrably needed; don't reconstruct a generic coverage wrapper under a different name. Definitive authoritative inventories remain distinct from reports.
4. **Observation contributors belong to `Observations.Api`.** Represent the actual observer-side contributing *allied observing units* and, for each, the *sensors providing the observation* as a typed relation associated with a `UnitObservation` (names can vary: `Observer`, `ObservationContributors`, etc.). Such references must be **observer-visible handles**, not hidden `Units.Api::UnitId`s. No dependency from `Observations.Api` to privileged `Units.Api`. Do not fabricate individual sensor provenance when Sea Power only exposes an aggregate plotted contact; allow incomplete/unavailable contributor attribution in that case.
5. **Evidence must not be confused with precision.** The set of contributing observer units/sensors explains what contributed to an observation when this is actually known, but does not alone establish which reported property is precise, up to date or correct. Preserve field-specific geometry or measurement age inside specialized observations when justified; don't turn generic `Exact<T>` into an evidence bundle.
6. **Canonical schema and all consumers must use the same simplified concepts.** Remove `StringKnowledge` / `DoubleKnowledge` / `*Knowledge` messages with Estimated variants and coverage wire enums. Encode `Exact<T>` as unambiguous present/absent fields or a concise two-state schema; zero/false count as present-known values. Use specialized Protobuf messages for partial observations and observer/sensor attribution as supported, not a parallel wire ontology.

### 3.4 Unit and observation composition

- `Units.Api::UnitInstance` comprises a definite ID, type (when represented), actual kinematics, installed capabilities, optional actual flight and air-facility state and other explicitly supported properties. **No `Knowledge<T>` and no `OrderEligibilityState`**.
- `Observations.Api::UnitObservation` comprises observer-scoped `ObservationId`, `Exact<T>` facts where applicable, specialized partial position/motion and equipment reports, optionally observed navigation/flight/air-operations data, plus actual observer-unit/sensor contributor references. It is *not* mechanically `Exact<UnitInstance>`: different structures and lifecycles are allowed.
- Use only `Uncertainty.Api::Exact<T>` for generic **all-or-nothing** knowledge; eliminate `Knowledge<T>`, `ObservedCollection<T>` and their coverage/estimate code/tests entirely. Use plain reported-item lists and specialized partial-observation types instead. Do not infer that an empty report proves an empty ground-truth inventory.
- `Known([])` route is definitely empty, `Unknown` route is not inspected/unknown. Remove duplicated `NavigationState.Inspected` unless the remaining independent semantics are verified and explicitly documented.
- Distinguish heading from course-over-ground; add real observed values when available but do not invent them. Physical units (`Speed`, `Distance`, `Angle` and durations as required) live in `Physics.Api`.
- Catalog platform descriptions and true unit properties may be definite but **only for the subset correctly obtainable from the source**. Never use guessed default zeros or placeholder certain state for unavailable attributes.
- **Air operations:** remove public generic `Facility`/`Stock` duplicate world entities/collections. For applicable actual units, use an air-operations capability/state; observers see an appropriately filtered **air-operations observation**. Ground airfields, carriers and helicopter-capable ships can provide this capability. Aircraft inventory and order availability are not the same thing; launch availability belongs in `Orders.Api`. No generic missile or projectile is required to have any airfield/carrier/crew component.

### 3.5 Orders, overlays, world relationships and evidence

- **Orders are an overlay** over an observer's known entities, keyed by `ObservationId`, not `UnitInstance` components. `Orders.Api` owns per-observer order availability, command request/result and the command-discovery contract. No dependency on `Units.Api` truth model; execution maps observer IDs to authorized native objects **inside the privileged provider**.
- Order request holds exactly **one** typed payload (`std::variant`/protobuf oneof), not two optional route/launch members. `Eligibility.kind` becomes a documented typed order descriptor/ID, not an opaque arbitrary string. Recheck current control, native automation protection, current authorization, freshness and execution preconditions on the allowed game thread.
- World owns **authoritative** faction/task-force membership relationships among actual `UnitId`s, and separately any **observer-known** faction/group relationships keyed by `ObservationId`. An unobserved hostile affiliation must not be invented from truth or inferred from ID format. Avoid `World ↔ Units` cycles.
- Snapshot capture time is not enemy contact measurement time; represent measurement age where available in specialized observations. Use typed observing-unit/sensor contributors within `Observations.Api` if source-backed. Do not duplicate generic source strings or identify a plotting-table cache path as a physical sensor; source attribution may be incomplete or fused.
- Strong typed identifiers for `Catalog.Api::{UnitTypeId,EquipmentTypeId,LoadoutId,ResourceId}`, `Units.Api::UnitId`, `Observations.Api::ObservationId`, plus other IDs only where justified. Public source fields follow `CodeStyle.xml`.

## 4. Canonical Protobuf, privilege boundaries and all consumers

Rewrite `Gateway.Api` canonical .proto definitions and native codec to match these **two distinct models** and eight ownership domains. No old `Subject`, `TrackId`, `OwnUnitId`, `Facility/Stock` split or obsolete message variants preserved solely for compatibility.

**Public operational wire/RPCs** carry `ObserverSnapshot`, `UnitObservation` and scoped `ObservationId`, never privileged `UnitInstance` state. A privileged truth snapshot may have internal wire representation if needed for bounded recorder/test work, but **must not become accessible through an existing public GetView**, debug switch, order request or ordinary provider endpoint. Keep transport authentication/authorization boundaries meaningful.

Migrate `Gateway.Grpc.Api` service and CLI, `Gateway.Grpc` server/mock, `Gateway.Api` codecs, `SeaPower.Client` C# publisher and authority checks, `SeaPower.Mod` bridge/runtime and their tests together. Reuse canonical wire definitions across languages. Native Sea Power `TrackSample`/contact objects may remain **inside the source adapter** because they accurately represent native sensor processing; they are not public NavalCommand identities or a second domain.

Existing verified route/waypoint/aircraft-launch workflows must remain functionally operable through new types. **No new missile orders, catalog RPC, equipment extraction, environment simulation or replay-storage system in this task.** No unauthorized live game runs or vendor binary modifications.

Do not make observer data by serializing ground truth and mechanically redacting fields when that would bypass native sensing/intelligence. Truth-to-observation association for privileged ML labels is a **separate, explicitly privileged** concern.

## 5. Enforce architecture, not just documentation

- Add a maintained automated architecture test/check executed through CTest/CI which checks public header includes and declared CMake INTERFACE target edges **against the exact allowlist in §2**, detects cycles, and reports actual offending files/edges. A forbidden header include must fail even if everything still compiles via a shared include root.
- Independently compile public headers and link populated domain targets; no domain source includes generated protobuf, Unity or gRPC.
- Tests should explicitly prove **no `Units → Uncertainty`, `Units → Observations`, `Observations → Units`, `Units → Orders`, `World → Orders`**, or other forbidden dependencies.
- Update normative `Docs/ARCHITECTURE.md`, `MODULE_STRUCTURE.md`, `AGENTS.md` and accepted domain proposal where needed to record truth-vs-observer separation, the eight domains, and projectile-as-unit support. Do not rewrite Task 014 report or historical game evidence.
- Delete superseded public headers/messages and old dual-model compatibility adapters. No backwards compatibility obligations.

## 6. Verification and acceptance

Create meaningful model/behavior tests beyond codec round trips:

1. **Authoritative vs observer:** definitive `UnitInstance` has no uncertainty types; uncertain `UnitObservation` has no truth ID; false/fused/duplicate observations representable without an actual 1:1 unit mapping.
2. **Multi-faction/replay:** one truth entity with different observation identities/knowledge in two independent observer snapshots; ordinary faction view cannot retrieve privileged truth or hidden correlation. Privileged recorder/replay contracts can hold both snapshots and associate decisions/outcomes without implying deterministic replay.
3. **Projectile as unit:** missile or another independently simulated projectile is representable as a true unit with its own ID/kinematics and can be observed as an uncertain contact, with **no mandatory crew/aircraft/air-operations state**. Distinguish missile inventory/launcher equipment from launched missile unit; supported commandability remains independently checked, not derived from its class or selectability.
4. **Equipment/resource:** missile deployment capability, radar, ECM, chaff, sonobuoy, tank; shared typed effects/operations, discrete versus continuous and reservable resources, plain reported inventory lists with no fabricated completeness, `Exact` all-or-nothing stock when applicable, no parallel capability index.
5. **World/air operations:** airfield/carrier world entity with supported air-operations state, and corresponding filtered observation; faction memberships as overlays, no separate public `facilities` collection, no duplicated launch eligibility.
6. **Orders:** per-observer overlay, exactly one typed order payload, reject unauthorized hostile/false/stale handles after current-state check; preserve protected native actions and order lifecycle.
7. **Observability:** two-state `Exact<T>` (`Known(0)`/`Known(false)` vs Unknown) survives C++/C# encode/decode; partial localization uses specialized observation shapes; an empty reported equipment list is not proof of disarmament; observer and sensor contributors are represented without leaking authoritative IDs or inventing unsupported provenance; snapshot versus measurement times remain distinct.
8. **Boundaries:** automated include/CMake graph rejection for an injected forbidden edge; independent header compilation and correct target graph.
9. **Integration:** build/test all affected native/managed repositories, mock host/CLI movement and discovery, offline managed provider/bridge behavior. Report which checks were actually performed and all blockers. **Do not claim live simulator verification.**

**Deliverable checklist:**

- [ ] Latest repositories pulled safely; owner TODOs reread; baseline revisions recorded.
- [ ] Eight acyclic domain boundaries and automated include/target allowlist enforced.
- [ ] Separate certain `Units.Api::UnitInstance` and uncertain `Observations.Api::UnitObservation` with separate IDs and privileges.
- [ ] `Uncertainty.Api::Exact<T>` is only Known(value)/Unknown; generic Estimated, Source and collection-coverage models are removed, with specialized partial observations and typed available observer/sensor contributors instead.
- [ ] Uncrewed projectiles, including missile-as-unit, representable without platform assumptions; launch equipment remains separate from deployed unit.
- [ ] Orders and faction/task-force associations modeled as overlays, no reverse dependencies.
- [ ] No duplicate domain identity, equipment/resource meaning, facility/stock world entities, redundant navigation inspection, or order eligibility inside a unit.
- [ ] Single new canonical Protobuf contract; affected C++/C# consumers migrated without legacy aliases.
- [ ] Meaningful model, privilege, DAG and supported operational regression tests run and honestly reported.
- [ ] `Docs/Tasks/015/REPORT.md` documents decisions, exact files, removed types, DAG, builds, tests, unresolved source limitations and repository commit IDs; task index updated.
- [ ] Changes committed **locally per repository; do not push** without owner approval.

Proceed with refactoring in this assignment, without an additional approval loop. If a requirement conflicts with verified game-engine safety/observability, document evidence and smallest justified deviation, rather than silently leaking privileged data or inventing values.
