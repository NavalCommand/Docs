# NavalCommand — Unit, equipment and configuration domain model

**Status:** Type-first design proposal for owner review; not approved or implemented. The discussion originated in Task 012 but defines concepts shared by the catalog, world/track observations, equipment intelligence, and engagement analytics. **Do not derive or freeze RPC names from this document yet.**

**Grounding:** `Docs/ARCHITECTURE.md`; Tasks 001 and 003 for game metadata, ammunition/loadout and magazine semantics; Task 012 for player-visible engagement values. This document proposes a *generic model*; it does not claim the installed Sea Power build has identical class names or can populate every field.

## 1. First define the boundaries

The ambiguous phrase "unit equipment" must mean **more than weapons**.

| Domain concept | What it represents | What it is not |
|---|---|---|
| **Unit class** | A catalog platform type: ship, submarine, aircraft, installation, etc. | A live unit or observed enemy track |
| **Class configuration / variant** | One defined equipment fit or refit for a class (fixed systems, sensors, launchers, modifications) | An assertion that every instance has this fit |
| **Loadout definition** | One possible mission-specific payload/stores arrangement for a class/configuration; may add removable pods/containers | A live aircraft's currently remaining ammunition |
| **Equipment definition** | Catalog specification of a fitted system: weapon/launcher, sensor, EW/countermeasure, communication, etc. | Its present status, readiness, or emission state |
| **Munition/consumable definition** | Catalog type of expendable stock: missile, torpedo, shell, chaff, etc. | A weapon mount or the current count of rounds |
| **Equipment fit** | Nominal installations/mounts and carried-stock quantities associated with a configuration/loadout | Direct observation of an individual unit |
| **Unit state** | Actual observer-authorized facts for an owned/friendly runtime unit | Catalog nominal equipment |
| **Track** | An observer's possibly uncertain contact record with its own opaque, scoped identity | A reference to authoritative enemy unit state |
| **Equipment picture** | What the observer currently knows or estimates about installations, sensors, weapons and stores on *one unit or track* | The class catalog or omniscient enemy inventory |
| **Analytics capability** | What a backend is able/willing to calculate in general | Equipment definitions, unit loadouts or current state |

A catalog **enumerates all definitions and variants known to the simulator installation** (report incomplete coverage if enumeration fails), while a world/track equipment picture is always scoped to a specific observer and observation time.

## 2. Static catalog definitions

### 2.1 UnitClass

A unit class has a `UnitClassId`, display name, platform category and supported static specifications. Its possible class configurations are explicit catalog references. A class may have several configurations; no configuration is assumed to be universal or the default unless source data explicitly says so.

The class represents the design family or platform entry in that simulator's catalog. A simulator that treats every refit as a separate class may map each to its own `UnitClassId` rather than inventing a higher-level relationship.

### 2.2 UnitConfiguration and UnitLoadout

These are **different dimensions** even when a particular game flattens them:

- `UnitConfiguration` describes a structural/equipment **fit** (e.g. radar and sonar suite, permanent launchers or gun systems, upgrades/refits). It is a catalog definition, not a mutable deployed unit.
- `UnitLoadout` describes a possible **mission payload** (selected munition mixture, external pods, fuel tanks, sonobuoys, expendable stores). A loadout can be associated with a configuration or directly with a class if no separate configuration level exists.
- A defined variant may be represented as a fully resolved set of installations and nominal stores. Inheritance/delta encoding is an implementation detail, not a public requirement.

Each has a stable **catalog-scoped ID**, a source/role/name when available, and relationships to compatible class/configuration IDs. The source may not supply a named default, exact ammunition counts, a complete list of variants, or any meaningful loadout concept for a particular platform; model those omissions honestly.

### 2.3 EquipmentDefinition and its families

**Equipment** means installed or fitted functional systems, not just ordnance. Prefer a small common core (`EquipmentId`, `Kind`, designation, manufacturer/source as available) and **specialized descriptions** rather than one giant union of hundreds of nullable fields.

Core families to represent as the backend supports them:

- **Weapon systems and mounts:** gun, launcher, missile/torpedo tube, hardpoint, CIWS, and compatible munition types. An installed launcher and a loaded missile are different things.
- **Sensors:** radar, sonar, ESM/passive RF, electro-optical/IR, visual and other detection/track/fire-control systems. Define nominal sensing roles separately from whether an installed sensor is currently active, damaged, tracking, transmitting or emitting.
- **Electronic warfare and countermeasure equipment:** jammers, decoys, dispensers; their consumables are stock, not the dispenser itself.
- **Communications and data links:** installed capabilities, to the degree relevant and exposed.
- **Other platform systems:** propulsion, aircraft handling, navigation or support systems when they have meaningful exposed capabilities. Do **not** require every internal Unity component to become an API equipment object.

A sensor's presence in the catalog does **not** imply the observer can see its current emissions or attribute detections to it. A weapon mount's existence does **not** imply it is loaded, ready or authorized to fire. Nominal sensor/weapon performance is distinct from current target-specific effectiveness.

### 2.4 MunitionDefinition, EquipmentInstallation and NominalStores

`MunitionDefinition` describes an expendable type (missile, shell, torpedo, sonobuoy, chaff, etc.) and nominal specifications; some consumables are not fired by a weapon in the ordinary sense.

A catalog `EquipmentInstallation` / fit entry connects a configuration or loadout to `EquipmentId`, with optional slot/station/location and nominal count. `NominalStores` separately connects a `MunitionId` to its intended location and **nominal quantity**. Keep launcher-loaded and magazine/reserve quantities distinct where the game makes that distinction; do not double-count.

**Relationships (conceptual, not source-level inheritance):**

```text
UnitClass
  ├── UnitConfiguration(s) ----------┐
  │      └── nominal EquipmentFit ---┼---> EquipmentDefinition(s)
  │                                  │       ├── WeaponSystem / Launcher
  └── UnitLoadout(s) ----------------┘       ├── Sensor
         ├── optional fitted pods/mounts     ├── EW / Countermeasure / Comms
         └── NominalStores ----------------> MunitionDefinition(s)
                                              ▲
                weapon/launcher compatibility ┘
```

The same `EquipmentDefinition` can appear on multiple classes/configurations; the same `MunitionDefinition` can be carried by multiple loadouts and fired from compatible launchers. Provide content revision/hash and **catalog completeness**. Catalog IDs should not be assumed globally stable through mods, incompatible game versions or renaming.

## 3. Runtime instances and observer knowledge

### 3.1 Unit and Track are not the same public identity

A controllable/friendly `Unit` has a session-scoped unit ID and may have a known catalog `UnitClassId` and current `UnitConfigurationId`/`UnitLoadoutId`, if legitimately verifiable. Its actual systems and stores may diverge from the catalog because of mission editing, launch-time fitting, use, damage, reloading or reconfiguration.

An enemy `Track` has an **observer/session-scoped opaque track ID**, not a public `Unit` or `BaseObject` ID. It may have estimated or identified class/configuration information, or none at all. Different observers may have different equipment knowledge for the same physical unit. Do not force all observed tracks into a globally authoritative unit entity.

### 3.2 EquipmentPicture (the desired answer to "what equipment does this unit have?")

The central runtime/observation concept is **`EquipmentPicture`**, associated with exactly one friendly `UnitId` *or* one observable `TrackId`. Its contents include:

- **InstalledSystemObservation(s)** — which weapon mounts, sensors, EW systems, communication systems and other equipment are actually confirmed/estimated present; optional locations/mount identities and condition/readiness as observer-visible.
- **MunitionStockObservation(s)** — observed/known munition types and quantities; separately represent rounds *loaded in launchers*, *stored in magazines/reserves* and *total* only when trustworthy. A quantity may be unknown or estimated.
- **OperatingStateObservation(s)** — *separate* from installation: sensor emitting/on/off/unknown; fire-control and tracking modes; weapon mount readiness/reload/damage/disabled state where known. Do not infer these from the nominal catalog.
- **Class/configuration/loadout identification**, when known; the classification itself may be estimated, not confirmed.
- Observation timestamp, observer/session/observation version, provenance and **coverage per equipment family** (not one global "everything known" switch).

A runtime installation is *an instance or observation of* a catalog definition; if its exact catalog type is unknown it can still be recorded as an unknown-type sensor/launcher contact without fabricating a catalog ID.

### 3.3 Knowledge, evidence and completeness are separate axes

Conceptual value wrapper:

```text
Knowledge<T> {
    state: Known | Estimated | Unknown
    value?: T
    observedAtSeconds?: number
    evidenceSource?: SourceKind
    confidence?: number           // only if defensible and actually available
}
CollectionCoverage = Complete | Partial | Unknown
MetricSupport = Available | Unavailable | Unsupported  // different dimension
```

`Known` means **the observer is justified in reporting it**, not that the software has certified universal ground truth. `Estimated` must expose its inferential nature. `Unknown` means absence of knowledge, **not absence of equipment**.

Examples:

- `installedSensors = []` with **Unknown** coverage: no sensor information; **not** evidence that the target has no sensors.
- `installedWeapons = []` with **Complete** coverage: no weapon systems observed within a genuinely complete authorized inventory.
- Radar emission detected: may justify evidence of a compatible radar/emitter family, **not automatically an exact sensor model or a complete onboard suite**.
- Enemy identified as class X, which has four catalog loadout options: those four are **possibilities**, not four observed loadouts or confirmed carried rounds.
- Own unit launcher known present with 2 loaded rounds: distinguishes mount existence, present inventory, capacity and source/measurement time.

A backend may support some equipment families but not other ones (`Unsupported`), while an individual enemy contact may simply have **no observed information** about a supported family (`Unknown`). Do not collapse backend inability, uncertainty and confirmed zero into a single null/status.

### 3.4 Sea Power-specific evidence and open questions

- Task 001 mapped `UnitMetadataProvider.UnitData`, `AmmunitionData`, launcher compatibility and native weapon/magazine state. Loaded ammunition and magazine quantity are distinct; exact semantics need validation for representative platforms.
- Task 003 found `FlightDeck.Loadout` variants for aircraft and **aggregate hosted airframes by class/squadron**, not persistent pre-equipped aircraft while stored. A selected loadout affects launch preparation, and deployed/spawned aircraft then become ordinary unit instances.
- Task 012 found the player targeting UI uses true target `BaseObject` to calculate derived engagement estimates. The owner's approval to expose **UI-visible derived analytics** does **not** imply permission to disclose hidden raw enemy sensor fits, ammunition reserves or exact installed weapons. For the enemy `EquipmentPicture`, determine each field's ordinary player visibility independently.
- No earlier task established a complete enumeration/mapping of all unit classes, their possible variants, all sensor types or complete enemy equipment visibility. These are **design requirements**, not verified implementation capabilities.

## 4. Separation from engagement analytics and orders

- **Catalog:** what a class/configuration/loadout may *normally* have (plus definitions/capabilities).
- **EquipmentPicture:** what this particular friendly unit or enemy track is *currently believed to have*, as observed by the selected side.
- **EngagementAssessment:** what an actual friendly shooter/munition pairing is estimated to achieve against one observable target track under current conditions.
- **Order eligibility:** what command can currently be submitted/executed; it must be validated on dispatch.
- **Backend capability discovery:** what data families and calculations a simulator supports *in principle*.

These concepts refer to one another via IDs/provenance but must not be combined into one giant snapshot, nor should a hypothetical catalog loadout be promoted silently into an observed enemy state.

The next design step should refine **types, relationships, identity, optionality and evidence provenance** before specifying methods, RPCs or wire topology. The method-first [API_DESIGN_PROPOSAL.md](API_DESIGN_PROPOSAL.md) is retained as historical discussion, **not the currently approved contract**. No schemas or implementation change as a consequence of this document.

## 5. Questions for owner approval (type-level only)

1. Use the umbrella name **Equipment** to include weapon systems, **sensors**, EW/countermeasures, communications and additional exposed fitted systems; model *ammunition/consumables* as distinct stores?
2. Distinguish **UnitClass → optional UnitConfiguration → optional UnitLoadout** to capture permanent fits/refits versus mission-specific payloads, while permitting flatter simulator source data?
3. Keep **EquipmentPicture** as one observer-scoped description usable for own units and enemy tracks, with per-family completeness and per-field known/estimated/unknown provenance?
4. Should the first implementation cover weapon systems, sensors, EW and their stores, adding other platform equipment families when justified, or require additional equipment domains immediately?

**Status:** Design proposal only. No API method names, repository boundaries, Protobuf fields or actual implementation are approved by this document.
