# Task 014 — Composable equipment and unified world domain migration

**Status:** Approved design direction; implementation pending. **Owner-approved decisions in this task supersede the older type sketches in Task 012 and the source-mapping suggestions in Task 013 when they differ.** This task implements C++ domain contracts **and coordinated migration of existing world-domain consumers**. It is not a Sea Power adapter, catalog extractor or public RPC redesign. **Latest owner correction:** do not preserve the old mixed-purpose `World::Api::Unit`/`Track` as parallel domain representations; migrate their useful fields into components of `UnitInstance`.

## 0. Start with repository synchronization

Before editing, synchronize local NavalCommand checkouts with their remote branches, **especially**:

- `NavalCommand/Docs` — this Task 014 specification and latest project conventions.
- `NavalCommand/Domain.Api` — the domain contracts and unified world shape.
- `NavalCommand/Gateway.Api` — existing domain ↔ wire codecs currently consume `World::Api::Unit`, `Track` and `View`.
- `NavalCommand/Gateway.Grpc` — the mock provider, server and tests consume the old `View`.
- Any other repository you need to inspect or build against. Keep the `Gateway.Grpc.Api`/`SeaPower.Client` public wire behavior unchanged unless a verified blocker is escalated.

Inspect working-tree status and current branches first. Pull/fast-forward safely where possible; do **not** reset, discard, overwrite or rebase uncommitted/unpublished user work without explicit authorization. Report a synchronization conflict/blocker instead of forcing it. Do not assume an old checkout contains the current task. Work from the synchronized `Docs/Tasks/014/TASK.md`.

Read `Docs/AGENTS.md`, `ARCHITECTURE.md`, `MODULE_STRUCTURE.md`, `CODE_STYLE.md`, `CodeStyle.xml`, `LOCAL_SOURCES.md`, and the current `Domain.Api` headers, CMake definitions and tests. Review `Docs/Tasks/012/DOMAIN_MODEL_PROPOSAL.md` and `Docs/Tasks/013/{STATIC_FINDINGS.md,DOMAIN_MAPPING.md}` for background, **but apply the corrections below**.

## 1. Objective and implementation boundary

Define a small, compositional, simulator-independent C++ domain model representing:

1. Authoritative unit/equipment **catalog definitions** for the current simulation context.
2. Alternative **fully resolved loadouts** for a unit type.
3. **Equipment types** composed from independently meaningful capabilities and employment/operation traits, covering sensors, conventional weapons, active EW, expendable countermeasures and auxiliary equipment.
4. **Observer-scoped unit instances**, with equipment as **capability instances**, uncertainty/knowledge and optional physical traits.
5. Explicit distinction between **known empty** and **unknown/partially known** equipment collections.
6. **Replace legacy `World::Api::Unit` and `Track` with `UnitInstance`**, moving their current data into coherent components rather than keeping two competing domain models.
7. **Migrate affected native consumers** (`Gateway.Api` codec, `Gateway.Grpc` mock/server/tests) as part of the same coordinated change, retaining existing wire/service behavior.

Implement the simulator-independent types in `NavalCommand/Domain.Api` without Sea Power, Unity, protobuf, gRPC, model-provider or vendor dependencies. Transport/codecs remain in adapter repositories. **Do not preserve the old mixed-purpose public `World::Api::Unit` and `World::Api::Track` as permanent parallel types.** `World::Api::View` may remain as a snapshot envelope but should contain a single unified instance collection. Preserve existing `Orders.Api` contracts and behavior.

This is a domain implementation **plus synchronized native-consumer migration**, not a Sea Power integration. Keep V1 focused and composable; we do not need to express every simulator property immediately.

## 2. Accepted domain semantics

### 2.1 One EquipmentType family

Use **one composable `EquipmentType`**. Do **not** maintain separate root `WeaponType`, `SensorType`, `ElectronicWarfareType` or `MunitionDefinition` hierarchies in the new model.

- **Capabilities** describe *what equipment does*: e.g. sensing, damage, jamming, deception/countermeasure, communication, resource storage. One equipment type may expose multiple capabilities.
- **Operations/employment** describe *how a particular capability is used*: accepted targeting inputs (unit track, location, area, bearing, no target; other inputs when justified), deployment or activation, one-shot vs continuous operation, supported modes, and relevant costs/resources.
- These dimensions must be composed independently. An equipment type may have **multiple operations**. A deployed expendable sonobuoy can provide **continuous sensing** after a **one-shot deployment**. An ECM capability may be continuous with zero ammunition consumption. Do not use a single `IsWeapon`/`IsSensor`/`IsExpendable` flag to stand in for those contracts.
- Use **typed component/variant structures** with clear names and semantics rather than an unrestricted string-key bag or one enormous structure with irrelevant fields. Do not invent field-level performance metrics unsupported by a V1 use case.
- **Physical properties** (e.g. firing arc, installation orientation or geometry) are a **separate, optional dimension**, added to type/fit/instance only where operationally meaningful. Do not require a physical launcher, magazine, hardpoint, dispenser or one-to-one device identity for a public capability.

`EquipmentInstance` is **an operational capability instance**, *not* necessarily a physical object. One native physical device might provide multiple public capabilities, and multiple native systems might back one selectable capability. Any physical-instance correlation belongs to optional traits only if useful.

### 2.2 UnitType and Loadout

`UnitType` comprises an identity, a composed/platform-specific description (aircraft, helicopter, surface vessel, submarine, etc. to the extent needed in V1), and **alternative resolved `Loadout`s**. Prefer tagged composition/`std::variant` where mutually exclusive platform descriptions are useful; do not create platform inheritance hierarchies or the earlier extra `UnitConfiguration` layer without evidence.

`Loadout` represents a complete **equipment/capability fit within the V1 published schema**, including a collection of equipment references/fits and any modeled nominal resource capacities. Equipment definitions can be shared across unit types and loadouts; use catalog-scoped IDs or another consistent reference strategy. The catalog should be self-consistent and resolvable. A unified catalog value/object can contain the unit types and the equipment definitions they reference; no API retrieval method is needed in this task.

**Authoritative catalog invariant:**

- `Catalog`, `UnitType`, `Loadout`, `EquipmentType`, `EquipmentFit` and other *catalog definition* types contain **no knowledge/uncertainty states**. Every property exposed by them is definitive for the catalog's resolved simulation context.
- **This does not require all Sea Power parameters to be modeled in V1.** Deliberately omit unmodeled properties. An optional catalog component means **not represented/not applicable by the chosen schema**, never `Estimated` or `Unknown`.
- For any **modeled exhaustive collection**, `[]` means **truly empty**. Do not populate fabricated zeros or silently omit required equipment and label the resulting fit complete. If required catalog resolution fails, the **future retrieval/adapter operation** must report failure rather than insert unknowns into catalog types.
- Dates and service years are **Sea Power source-data resolution concerns**, not V1 domain fields. The future adapter resolves available fits and date-dependent ordnance to the **current scenario** before constructing a catalog; do not introduce public year-applicability types. A catalog snapshot should be scoped/revisioned to the resolved content/context, and identifiers must not rely on Sea Power's temporary numeric metadata indexes.
- Preserve a path to representing catalog-wide **all applicable known definitions**, including equipment types unreferenced by currently deployed units/loadouts; this does not imply multiple RPC methods.

### 2.3 UnitInstance and EquipmentInstance

Use **one common `UnitInstance` data shape** for an owned/friendly unit and an enemy contact/track. Their **observer knowledge differs**, not their fundamental type. An instance should be able to express:

- A known, estimated or unknown unit-type/catalog identity.
- A known/estimated/unknown position (with appropriate positional uncertainty where observed).
- **One observed equipment collection** of `EquipmentInstance` capabilities.
- Runtime capability state (activation/operational status, resource amounts and applicable damage/readiness) with **per-attribute knowledge**.
- Optional applicable platform-specific status/damage components, without requiring nonsensical fields for unrelated platforms.

Retain a **tagged observer/session-scoped subject** so an own-unit ID and an observer-visible enemy track ID cannot be confused or used interchangeably to authorize orders. A common representation does not grant order authority. The source of enemy information must remain the observer's legitimate perspective, not hidden authoritative game objects.

`EquipmentInstance` references a catalog capability/type where known and describes the **current observed capability**. It may be partially identified, damaged, active/inactive, have an unknown remaining consumable count, or use a different resource (e.g. occupied jamming channels). Its optional physical traits are independent of the capability identity. Do not assume `EquipmentInstance` = one physical launcher or require all instances to have ammunition.

Do **not** promote hypothetical class-standard equipment into a confirmed enemy instance. A known unit class with several possible loadouts can still have an entirely unknown installed equipment collection.

### 2.4 Existing World.Api fields become typed components

The current `World::Api::Unit` combines identity, position, motion, routes, order eligibility and aircraft state, while `Track` duplicates identity/position/provenance in a separate type. **Replace both with the common `UnitInstance` domain contract**, not an unused parallel `UnitInstance`.

| Existing domain property | New component responsibility |
|---|---|
| `Unit.id`, `Track.id` | **Tagged subject/identity:** own-unit or observer-visible track; authority remains distinct |
| `Unit.name` | **Identity/display:** named value, potentially unknown for a contact |
| `Unit.position`, `Track.detectedPosition`, `Unit.speedMetersPerSecond` | **Kinematics/observation:** position uncertainty and observed motion |
| `Unit.plannedRoute`, `Unit.routeInspected` | **Navigation:** route and explicit inspected/unknown semantics |
| `Unit.orders` | **Order eligibility:** reuse `Orders.Api::Eligibility`; missing data must not be interpreted as executable |
| `Unit.airborne` | **Aircraft/platform state:** optional and not applicable to ships |
| `Unit.sampledAtSeconds`, `Track.sampledAtSeconds`, `Track.provenance` | **Observation metadata:** sample age/time and observer source |
| New `EquipmentInstance` collection | **Equipment capabilities:** currently observed state, independently of physical installations |

Compose these using named, typed components (e.g. focused optional state structures or narrow variants), not a monolithic new `UnitInstance` with all the old unrelated fields, and not an untyped string-key bag. Do not build a general-purpose ECS runtime/framework. Separate inapplicable optional components from unknown observed values; do not invent default zeros, false values or complete empty lists.

**View shape:** prefer **one collection of `UnitInstance`** whose tagged identities distinguish owned units and observer-visible contacts. Do not maintain competing `units` and `tracks` *domain types*. `View` may retain session, observer, version, readiness and facility metadata. Only owned-unit identities can authorize unit orders.

**Behavior to preserve from V1:** unit name, position and optional speed; current route, route-inspected flag and order eligibility; explicitly known airborne `false` vs unknown; unit/track observation times; track position, provenance and opaque identifier. Tests must cover these mappings.

### 2.5 Knowledge and observed-collection semantics

Provide small reusable **instance-side** knowledge and collection primitives, with safe default states and unambiguous semantics. Concepts:

```text
Knowledge<T>:
    Known(value) | Estimated(value, provenance/quality if available) | Unknown

ObservedCollection<T>:
    coverage = Complete | Partial | Unknown
    items = list<T>
```

Normative invariants:

| State | Semantics |
|---|---|
| `Complete + []` | Observer definitively knows the collection is empty |
| `Complete + [A,B]` | All observed members of that category are enumerated |
| `Partial + [A,B]` | A/B are observed; other members may exist |
| `Unknown + []` | Observer has no equipment inventory knowledge |
| `Unknown + [A]` | **Invalid or not representable**: no contradictory nonempty collection under Unknown coverage |

A default-constructed observed collection must **not** silently mean `Complete + []`. If `Partial + []` is supported, document that it means incomplete enumeration with no individually reportable members, not definitely empty.

- `Knowledge<T>` must distinguish `Unknown` (no value) from an actual measured/estimated value. If `Estimated` includes confidence, it must be genuinely sourced, not invented.
- `Unknown` on a specific enemy unit is separate from **unsupported by backend/adapter**; the latter is a retrieval/feature-support property, not a pretense that the enemy is unarmed.
- Observation provenance/age may be represented minimally or deferred explicitly if not essential to the initial reusable type, but no contract may encourage silent truth/estimate conflation.
- Keep uncertainties **out of catalog definitions**; do not accidentally reuse `ObservedCollection<T>` for catalog loadouts.

## 3. Small demonstration fixtures and testing

Add tests/smoke examples using only domain values (no Sea Power dependency) covering these **modeling cases**:

1. A guided missile: track/location targeting as supported, one-shot launch, damage effect and finite nominal/current ammunition.
2. A search radar: sensing, continuous scanning/activation, no required ammunition.
3. Active ECM: jamming effect, target/position selection, sustained activation with channel/resource capacity rather than ammo.
4. Chaff: no designated target, one-shot deployment, finite expendable count.
5. Sonobuoy: one-shot deployment to an area/location, **then** continuing sensing as a capability; don't force `EquipmentType` into the weapon-vs-sensor binary.
6. External fuel tank: storage/resource and optional jettison/deployment, neither an attack nor a sensor.

Also test:
- Multiple loadouts with different fully known nominal fits for one `UnitType`.
- One equipment definition referenced by more than one loadout.
- Catalog values expose no unknown/estimated state, and representative references resolve.
- `Known(0)` stock is different from `Unknown` stock, and `not applicable` is different from either where modeled.
- `Complete + []`, `Partial + [A]`, `Unknown + []`, invalid `Unknown + [A]` and default-unknown behavior.
- A known-type enemy contact whose equipment remains unknown, and tagged own-unit versus track identifiers.
- Header-level independent consumption/build and no regressions to existing `Orders.Api`/`World.Api` tests.

Do not claim to reproduce every Sea Power mechanic with these synthetic fixtures; use them to prove the generic model composes sensibly.

## 4. Files, ownership, migration and documentation

1. **Update the active conceptual design first:** `Docs/Tasks/012/DOMAIN_MODEL_PROPOSAL.md` to reflect the **single composed `EquipmentType`**, capability/operation separation, equipment-instance-as-capability, fully authoritative-but-selective catalog, scenario-resolved loadouts and observer-only unknowns. Treat Task 013's suggestion of uncertain/incomplete catalog values and public date applicability as **superseded design recommendations**, not errors in its recorded game findings.
2. Align relevant normative `Docs/ARCHITECTURE.md` language with the accepted concepts. Keep Task 012's old method-first `API_DESIGN_PROPOSAL.md` explicitly **deferred**; do not silently promote its RPC surfaces or overwrite historical Task 013 findings.
3. Implement the smallest coherent new headers under `Domain.Api/public/NavalCommand/...`, with namespace-mirrored PascalCase one-type-per-header organization. Determine whether a small separate `Catalog.Api` INTERFACE target is warranted or whether existing `World.Api` is sufficient; document your choice. **Do not create a new functional Git repository**, a duplicate domain/transport model or needless framework.
4. Adjust `Domain.Api/src/CMakeLists.txt` and tests only as required, preserving existing independently consumable targets and transitive dependencies.
5. Write `Docs/Tasks/014/REPORT.md`: added types/components, invariants, design tradeoffs, exact files, test commands/results, gaps/omissions and any deferred follow-up work. Update the Task 014 entry in `Docs/Tasks/README.md` to record actual outcome.
6. Apply `Docs/CODE_STYLE.md` and `CodeStyle.xml` (PascalCase public C++, tab indentation, readable tests, no closing namespace comments in added files).

## 5. Explicit exclusions

**Do not**:
- Change current `Gateway.Api` Protobuf messages or `Gateway.Grpc.Api` RPC services.
- Modify `SeaPower.Client`, `SeaPower.Mod`, installed game files or run live experiments.
- Replace `World.Api::Unit/Track/View` or change existing order/availability semantics.
- Implement `GetUnitTypes`, `GetCatalog`, `GetUnitEquipment`, `AssessEngagement`, command dispatch, engagement analysis or any transport API.
- Build a full physical simulation of launcher slots, magazine partitions, sonobuoy life cycles, electricity/fuel networks or speculative radar performance.
- Invent unspecified catalog values, unsupported device modes or operational AI behavior.
- Introduce unknowns, probabilities or estimated properties into definition/catalog types.

## 6. Acceptance criteria

- [ ] Latest repositories pulled safely; current checkout/revisions recorded and any working-tree conflicts reported.
- [ ] Updated active conceptual proposal and normative architecture agree with accepted composition/knowledge semantics.
- [ ] New independent domain-only C++ types compile with existing C++26/toolchain conventions.
- [ ] Single composable `EquipmentType`, `EquipmentInstance` as capability, optional physical dimension and multiple equipment operations represented without weapon/sensor/EW roots.
- [ ] Authoritative catalog has a small, definitive property subset and fully resolved loadouts; no catalog-level Unknown/Estimated states or generic historical year schema.
- [ ] Shared `UnitInstance` correctly differentiates own-unit and observer track identity with instance-only uncertainty.
- [ ] Empty-vs-unknown equipment distinction is structural and tested (including contradictory states).
- [ ] All six representative equipment scenarios and cross-referencing/multiple loadouts tested with legible code.
- [ ] Existing Domain.Api targets/tests preserved; actual tests executed and results reported honestly.
- [ ] Documentation report and task index updated; code, docs and tests committed **locally** by repository; **do not push** unless the owner explicitly asks.

**Completion is domain-only.** A separate reviewed assignment will handle serialization, catalog construction/validation against the installed Sea Power build, enemy perspective filtering, real-time observations and API methods.
