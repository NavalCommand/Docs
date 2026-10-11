# NavalCommand — Composed unit capabilities and observations

**Status:** Type-first domain design in discussion; not an approved wire schema or implementation. This is the active successor to the [earlier method-first API sketch](API_DESIGN_PROPOSAL.md). Focus on domain meaning, composition, identities and partial information; API methods and repository organization will follow.

**Evidence:** Tasks 001 and 003 established Sea Power equipment metadata, weapon/magazine state and alternative aircraft loadouts; Task 012 mapped the player weapon-selection and native engagement calculations. The shapes below are **design proposals**, not claims that the game has exact corresponding classes.

## 1. Design principles

1. **Composition, not a platform inheritance hierarchy.** A `UnitType` has a platform-specific description and a set of alternative, resolved `Loadout` definitions. Different platform types carry different descriptions without having to inherit different public base classes.
2. **Equipment means capabilities/contracts first, not physical devices.** `WeaponType` represents an **active effect-producing or deployable capability**: missiles and guns, active jammers, chaff/flares, towed or launched decoys and similar countermeasures. `SensorType` represents sensing/observation. Do **not** introduce an `ElectronicWarfareType` parallel hierarchy: active electronic attack/deception is a weapon effect, while passive ESM is sensing. A multifunction native device may back both capabilities. The API need not expose a hardpoint, launcher, dispenser, tube, or magazine as a physical entity merely because the backend uses one. Physical attributes are **optional attached components** when they affect behavior.
3. **One observer-facing runtime shape.** A `UnitInstance` represents what the selected observer knows about a friendly unit or an enemy contact. Enemy fields can be unknown or estimated. The two subjects may have different identifiers and command permissions without requiring two incompatible data models.
4. **Catalog ≠ current unit state.** A `Loadout` gives nominal equipment and full-stock quantities. A `UnitInstance` gives currently known equipment, operational state, damage and remaining stock, if available.
5. **Information availability is explicit.** Unknown equipment is not absent equipment; a class's possible loadout is not proof that a contact actually carries it. Backend unsupported features are different from per-contact unknown observations.
6. **One catalog retrieval operation is compatible with separate types.** The catalog may return unit definitions with embedded or ID-referenced capability definitions. The number of public methods is **not decided** by the number of distinct domain types.

## 2. Static domain types

### 2.1 UnitType

```cpp
struct UnitType
{
    UnitTypeId id;

    std::variant<
        AircraftTypeDescription,
        ShipTypeDescription,
        SubmarineTypeDescription,
        OtherPlatformTypeDescription
    > platformDescription;

    std::vector<Loadout> loadouts;
};
```

A `UnitType` is a class/type **definition**, not a unit in the simulation. Platform descriptions carry applicable attributes (e.g., flight performance for aircraft, hull/underwater characteristics for ships/submarines), without mandatory meaningless fields for other categories. This is composition through a tagged variant, **not public class inheritance**.

A `Loadout` is a **complete, resolved set of capabilities** for that type. The catalog can expose many possible fits, from different sensor suites and weapons to different mission payloads. For now, **do not impose an extra `UnitConfiguration` layer**. The backend may internally have refits, upgrade inheritance or nested loadout definitions; flatten these to resolved public loadouts unless there is demonstrated value in another level.

The catalog may identify a default loadout **only when the simulator source defines one**; do not invent a universal default. A loadout is not proof that every instance of the type actually uses it.

### 2.2 Loadout and capability fits

```cpp
struct Loadout
{
    LoadoutId id;

    std::vector<WeaponFit> weapons; // Attacks, active EW, countermeasures, decoys
    std::vector<SensorFit> sensors; // Radar, sonar, ESM, etc.
    std::vector<CommunicationFit> communications;
    // Other meaningful capability families only as justified.
};

struct WeaponFit
{
    WeaponTypeId type;
    std::optional<std::uint32_t> nominalFullAmmunitionCount;
    std::optional<FiringGeometry> firingGeometry; // Only if relevant to this fit
};
```

A `WeaponFit` represents a weapon capability as available **on this platform in this loadout**, possibly with nominal/full ammunition or consumable charges. It is not obligatorily a physical mount. Platform-specific restrictions such as attack arcs, emitter pointing, minimum release altitude, number of simultaneous channels or firing offsets can be placed in optional attached components. Restrictions intrinsic to the capability type may live in `WeaponType`; platform-dependent constraints belong in `WeaponFit`. An active jammer may have **no ammunition count at all**: *not applicable* is distinct from *unknown ammunition count*. A countermeasure dispenser's count can be represented as a nominal resource of the resulting player-selectable countermeasure capability.

A loadout's full ammo count is distinct from both the weapon's nominal performance and the runtime remaining count. A meaningful `0` is not the same as an unavailable quantity. If a backend has multiple munition choices for one user-visible weapon capability, model that as a component/choice set where necessary, not a mandatory `Launcher` entity.

`SensorFit` can similarly carry installation-dependent coverage, orientation or location information, while `SensorType` describes sensing behavior. Presence does not assert the sensor is turned on, radiating, functional or independently known to an enemy observer.

### 2.3 Equipment types are behavior contracts

The core catalogue families are:

- **`WeaponType`:** an effect-producing or deployable capability: kinetic attack, continuous jamming, deceptive emission, chaff/flares, sonar countermeasures, decoys, etc. Its employment may be offensive or defensive, manual or automatic, expendable or continuously operating. No universal target or ammo requirement.
- **`SensorType`:** sensing capability: category, passive/active modes, nominal performance and optional field-of-view/coverage components. Passive ESM is a sensor, not a weapon merely because it belongs to an EW suite.
- **`CommunicationType`:** datalink and communications capabilities where relevant.
- Other subsystem descriptions only where operationally useful, not a mirror of every game-engine component.

Use small capability descriptions with composable optional traits rather than one universal `EquipmentDefinition` requiring every device to have irrelevant properties. The same capability type may be reused by many loadouts.

**A weapon's employment is composed, not determined by its label:**

```cpp
struct WeaponType
{
    WeaponTypeId id;

    std::optional<TargetingContract> targeting;
    std::optional<EmploymentProfile> employment;
    std::optional<EffectDescription> effect;
    std::optional<GuidanceDescription> guidance;
    std::optional<FiringGeometry> inherentGeometry;
    // Further traits only when justified by actual capabilities.
};
```

- **`TargetingContract`** describes accepted inputs and whether they are required: no designation, unit/contact track, location/area, bearing, sector, frequency/band, or combinations. Do **not** hard-code `requiresUnitTarget` or reduce the problem to a single boolean. Even conventional weapons may fire at geographic locations, and launched decoys may require a bearing.
- **`EmploymentProfile`** describes what invoking the capability does: single release/launch, salvo, sustained activation/deactivation, mode adjustment or automatic/native threat reaction. This is *not* the unit's separate weapons-free/tight/hold engagement posture and does not imply that the external controller is authorized to invoke every possible employment.
- **`EffectDescription`** may describe damage, jamming, deception, screening or distraction without treating every effect as a calibrated numerical probability.
- **`GuidanceDescription`** and **`FiringGeometry`** are optional; many countermeasures have no guidance, but a towed or launched decoy may have placement constraints. Installed platform geometry may override or refine the type-level envelope.

Examples: a missile may need a track or target point and launch; chaff may be released with no target; an active jammer may be toggled and pointed at a frequency/sector; an acoustic decoy may be launched to a bearing. The *targeting*, *employment* and *effect* dimensions are independent; no separate countermeasure/EW type hierarchy is necessary.

**Physical multifunction systems:** One physical device can implement both a sensor capability and an effect capability (for example ESM plus active ECM). The source adapter may correlate them privately; optional shared-equipment/provenance references can be added if coordination or damage propagation requires it. Do not force a one-to-one physical device identity on every capability.

**Weapon versus launcher:** The API **does not require separate public `LauncherType`, `MunitionDefinition` or per-hardpoint objects for normal weapons**. In Sea Power, Task 012 found that the player-facing `WeaponEntry` is an ammunition-group selection, which may aggregate several compatible native weapon systems. This justifies representing a single selectable **weapon capability** with aggregated ammunition and firing restrictions when that is what the player controls. For non-ammunition or sustained-effect capabilities, selection and invocation may instead be a toggle or mode change; no identical native UI path has yet been established.

Do not claim that all Sea Power weapon systems literally have one launcher or one ammo storage location. If an advanced weapon has selectable ammunition subtypes, separate magazine stocks or multiple firing arcs, introduce **optional parts of the capability description or runtime state** if they become operationally relevant. This is not a reason to mandate physical launcher entities across the public domain.

### 2.4 Catalog closure and relationships

The catalog is composed around `UnitType` definitions and their loadouts, plus any shared capability definitions referenced by ID. A single catalog query could return the entire graph.

One caveat for the earlier **complete-catalog requirement**: the backend may define a weapon or sensor that is **not referenced by any enumerated unit loadout** (e.g. content awaiting a platform/mod). An implementation must either demonstrate that all definitions are reachable through `UnitType` or include unreferenced definitions in the *same* catalog result. This does **not** require separate `GetWeaponCatalog` and `GetSensorCatalog` methods.

Records need catalog-scoped IDs, a content revision/hash and truthful enumeration completeness. Cached capability descriptions must invalidate when game/mod content changes. Do not duplicate incompatible copies of shared definitions in different loadouts.

## 3. Runtime: one UnitInstance representation

### 3.1 Shared shape, distinct observation identity

```cpp
struct UnitInstance
{
    ObservedUnitId id;     // Observer/session-scoped identity; friendly or track
    Knowledge<UnitTypeId> type;
    Knowledge<PositionWithUncertainty> position;
    PlatformDamage damage; // Tagged platform-specific description; may be unknown

    ObservedCollection<WeaponInstance> weapons; // Includes active EW/countermeasures
    ObservedCollection<SensorInstance> sensors;
    // Other supported runtime capability state as needed.
};

struct WeaponInstance
{
    Knowledge<WeaponTypeId> type;
    std::optional<Knowledge<std::uint32_t>> ammunitionRemaining; // Absent if not applicable
    Knowledge<Readiness> readiness;
    Knowledge<DamageState> damage;
    std::optional<Knowledge<ActivationState>> activation; // e.g. active jammer on/off
    // Additional resource, mode or employment state only if applicable/observable.
};

struct SensorInstance
{
    Knowledge<SensorTypeId> type;
    Knowledge<SensorOperatingState> operatingState;
    Knowledge<DamageState> damage;
};
```

These are conceptual shapes, **not compilable production headers**. `PlatformDamage` should compose a tagged aircraft/ship/submarine-specific description rather than force a common damage hierarchy. Some damage and operating values will be unknown even when the platform category is known.

A friendly unit and an enemy track both use **`UnitInstance`**. For a friendly unit, the API may know almost all relevant fields. For an enemy contact, it may know only a position distribution and that an unidentified emitter is present. The same payload type captures both without filling enemy fields with authoritative truth. `WeaponInstance` includes **effect-producing systems and expendable countermeasures**, including those that have no target, no ammunition, or a continuously active state. A sensor-emitter observation alone does not prove an exact EW capability or that a particular jammer is installed.

**Identity and authority:** A controlled own-unit handle and an observer-visible contact/track handle are **not the same authority**. The common type can use a tagged/discriminated identity or provide subject scope alongside it. The existing protocol's own-unit IDs and opaque track IDs must not be conflated or used interchangeably for orders. Common data shape does not grant command permissions to enemy tracks.

**Sensor/weapon observation:** The catalog lists what a type *may carry*; a runtime `WeaponInstance` or `SensorInstance` describes what an observer **currently believes is present** and any known state. Do not inject catalog-derived hypothetical enemy equipment into the confirmed runtime collection.

### 3.2 Known, estimated, unknown and collection coverage

```text
Knowledge<T> {
    state: Known | Estimated | Unknown
    value?: T
    observedAtSeconds?: number
    source?: ObservationSource
    confidence?: number         // Only when meaningfully provided
}
CollectionCoverage = Complete | Partial | Unknown
BackendSupport = Supported | Unsupported   // Separate from contact knowledge
```

An empty `weapons` collection with **Complete** coverage means known unarmed. An empty collection with **Unknown** coverage means no information. An enemy contact known to emit radar may justify a radar-family inference, but **not** exact hardware identity, a full sensor suite, or a weapon inventory.

Position uncertainty is a first-class value (not simply nullable `Position`) and may be nearly zero for own units or substantial/unknown for enemy tracks. Mark data age/provenance on fields or coherent snapshots.

Distinguish per-field unknown information from a metric being unsupported by the simulator. Report actual zero ammunition as zero, not unknown, and do not double-count launcher-loaded rounds and magazine/reserve rounds if an advanced model adds those details.

### 3.3 Relevant Sea Power evidence and limits

- Task 001 identified `UnitMetadataProvider.UnitData`, `AmmunitionData`, launcher compatibility and internal weapon/magazine counts. This is evidence of native structure, **not a requirement to replicate that structure publicly**.
- Task 003 identified multiple `FlightDeck.Loadout` choices and aggregate hosted-aircraft stock by class/squadron. A hosted aircraft need not exist yet as a persistently fitted `UnitInstance`; selecting a loadout can be part of spawn/launch preparation.
- Task 012 mapped `WeaponEntry` to ammunition-group choices and native compatible-system aggregation. The runtime probe did **not** prove exact UI hover equivalence or read-only purity of all native methods.
- Owner-approved player-visible parity for **derived engagement estimates** does not authorize publishing raw hidden enemy weapon stocks/sensor types. Every `UnitInstance` observation must respect the selected faction's legitimately available information.

## 4. Relationship to analytics, commands and eventual methods

- **`UnitType` / `Loadout` / capability descriptions:** possible defined platform functionality, including passive sensors and active effect/deployment capabilities, and nominal capacities.
- **`UnitInstance` and component instances:** actual observer-known or observer-estimated capabilities and current state.
- **`EngagementAssessment`:** what a particular friendly weapon capability may accomplish against an observer-visible target, not a live-fire command.
- **Order eligibility:** whether a desired action is currently allowed/executable; reevaluated when dispatching.
- **Backend capability discovery:** which concepts and analytics a simulator supports in general.

**Methods come after the model.** The existing [method-first proposal](API_DESIGN_PROPOSAL.md) is **deferred**. Its specific named RPCs—including separate unit/weapon catalog methods and an extra equipment query—are not approved. The model permits a **single complete catalog retrieval** and the same `UnitInstance` type for friendly and enemy observations. Whether some large/expensive observations deserve dedicated queries is a performance/transport design decision for later, not a reason to split the domain now.

## 5. Remaining type questions

1. Should a loadout always expose **fully resolved** capabilities and nominal stock (recommended), even if the simulator stores variants as diffs?
2. Should each weapon be keyed by a **player-addressable effect/deployment capability** (recommended for Sea Power), allowing optional components to describe multiple backing native mounts, dispensers or emitters?
3. Which composable employment traits need to be public first: optional target designation, one-shot versus sustained activation, firing geometry, effect kind or selectable ammunition modes? Do not introduce a mandatory `engagementMode` enum that conflates these dimensions or the unit's weapons-control posture.
4. How should distinct friendly IDs and observer track IDs be tagged within a uniform `UnitInstance` while keeping command authority separate?

**Status:** Domain proposal only; no public C++ types, Protobuf schemas, RPCs or simulator code changed.
