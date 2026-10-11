# Task 013 — domain mapping

**Design basis:** [Task 012 domain model proposal](../012/DOMAIN_MODEL_PROPOSAL.md). The sketches below illustrate mappings; they are not C++ declarations or an approved wire schema. Numeric metadata IDs and example capability IDs are not durable public identifiers.

## Recommended shape

Use one `UnitType` definition with alternative `Loadout` records. Resolve each fit from the base platform definition, the selected loadout overrides, date-based ammunition and applicable stores. Keep catalog definitions separate from deployed equipment state. A current content revision must scope catalog identifiers and invalidate cached values after mod/content changes.

An additional `UnitConfiguration` layer is not justified by the inspected code. Some vessel/submarine loadouts appear as synthetic `UnitData` rows, while aircraft loadouts are embedded alternatives; normalize those native shapes into the proposed model. Preserve service-year/date applicability where a single fit name resolves to different munitions by era. If full resolution fails, mark the loadout/capability coverage incomplete rather than silently treating missing entries as zero.

## Source-backed examples

### F-14A aircraft, alternative fits and own-unit state

Installed content and the runtime sample support a conceptual record like this:

```text
UnitType
  id: catalogRevision / aircraft/usn_f-14a
  platformDescription: Aircraft
    maxSpeedAtSeaLevel: 794 knots
    ceiling: 55,000 feet
  loadouts:
    AirToAir
      AIM-7M: nominal 4
      date-resolved Sidewinder: nominal 4
      chaff: nominal 25
      20 mm ammunition: nominal 675
      F-14 external fuel tanks: 2 stores
      common sensor fits: AWG-9 radar, Phoenix radar, RWR/ESM, visual systems
    AirToAirLongRange: 2 AIM-7M and 1 AIM-54A in the sampled cache
    AirToAirIntercept: 2 AIM-7M and 4 AIM-54A in the sampled cache
    Empty
    Ferry
```

`UnitType=Aircraft`, the performance values, alternative names and system definitions come from `aircraft/usn_f-14a.ini` and the runtime metadata cache. The 1985 deployed instance actually reported four `usn_aim-9m` and two `usn_tank_267_f-14`; the catalog station uses a date-based Sidewinder reference, so an era-independent resolved fit would be misleading. The run’s `AirToAir` metadata also reported four AIM-7M, 25 chaff charges and 675 rounds of `usn_cal_20mm`.

The runtime F-14 had six sensor systems: `AN/AWG-9`, `AN/AWG-9_Phoenix`, `AircraftRWR`, `AircraftDECM_Med`, `Eyes` and `TCS`. The two radar systems had different on/off states; ECM and ESM were on. Map the radar, ESM and visual sensing definitions to `SensorType`/`SensorFit`. Map active jamming by `AircraftDECM_Med` to an effect-producing `WeaponType`/`WeaponFit`, while keeping its native `SensorSystemECM` backing private to the adapter.

The cache’s `AirToAirLongRange` and `AirToAirIntercept` samples include the counts above plus chaff and gun ammunition; the fixture intentionally logged only a representative ammunition subset for those alternatives.

A bounded `UnitInstance` sketch for that own unit can therefore include `type=Known(F-14A)`, `loadout=Known(AirToAir)`, current ammunition values, and sensor operating states. Report `Complete` collection coverage only after the adapter has reconciled every installed system, ammunition source and extra store for that platform; the sample alone is not a cross-platform completeness guarantee.

### Surface vessel and submarine types

`vessels/usn_ffg_oliver_hazard_perry.ini` supplies a surface-vessel example with `Default` and `Late` alternatives and radar, sonar and ESM system definitions. `vessels/usn_ssn_los_angeles.ini` supplies a submarine example with periscope depth 50 feet, crush depth 1,225 feet, sonar/ESM systems, torpedo and missile ammunition, MOSS decoys and noisemakers. Its runtime metadata gives different `Default`/`Late` ammunition fits, including 12 versus 14 `usn_mk48_mod4_adcap` and `usn_ugm-84a` versus `usn_ugm-84c`.

These records fit `ShipTypeDescription`/`SubmarineTypeDescription` plus alternative loadouts. The carrier `AirGroup` is different: it is facility stock aggregated by aircraft class/squadron, not the carrier’s own weapon loadout or a collection of live aircraft instances.

### Weapon and sensor capability examples

```text
WeaponType: usn_aim-7m
  targeting: map native target category, designation and guidance fields
  employment: launch/attack
  effect: kinetic damage
  guidance: native missile guidance data

WeaponType: active ECM effect backed by AircraftDECM_Med
  targeting: observed contact OR geographic position
  employment: sustained jam; switch/stop target
  effect: jamming
  ammunition count: not applicable
  optional capacity: free jam channels, if useful and well-defined

WeaponType: all_air_chaff_standard
  targeting: none for the direct release action
  employment: deploy
  effect: deception/countermeasure
  nominal quantity: 25 in the sampled F-14 fit

SensorType: AircraftRWR
  category: ESM/passive sensing
  runtime state: on and operable in the sampled own F-14
```

The ECM mapping is supported by `ObjectEffectorsViewModel` and `WeaponEntry`: the UI exposes ECM through a weapon-style entry, reports jam channels rather than rounds, and opens an ECM targeting state. That state accepts a map contact or geographic position, while `JamTask` maintains jamming or changes/stops the target. It is a capability mapping across native families, not evidence that ECM is a physical launcher. Passive ESM remains sensing. Chaff/noisemaker/MOSS are ammunition-backed deployable effects. This build has no separately enumerated flare ammunition type.

`WeaponType` should describe the effect/deployment capability. Ammunition identity, compatible native systems, magazines, hardpoints and firing geometry are optional data that inform a `WeaponFit`; they do not need to become public launcher/hardpoint entities. `WeaponFit.nominalFullAmmunitionCount` must not be used for jammer channels or fuel-tank store count.

## Friendly and enemy `UnitInstance`

The same shape can represent an own unit and an enemy track if knowledge and collection coverage are field-level and identity/authority remain distinct:

```text
Own F-14A
  subject: OwnUnitId(...)
  type: Known(aircraft/usn_f-14a)
  loadout: Known(AirToAir)
  weapons: observed ammunition and deployment capabilities
  sensors: observed definitions and current operation state

Identified enemy contact
  subject: TrackId(...)
  type: Known(aircraft/wp_mig-23mld)
  loadout: Unknown
  weapons: Unknown coverage
  sensors: Unknown coverage
  position: Estimated/known with track uncertainty and age
```

The runtime player plot table contained an identified `wp_mig-23mld` with a live position estimate. The enemy contact “Information” action opens the class encyclopedia without supplying the enemy’s actual loadout reference. A known class is not evidence of its exact fit. An empty enemy weapons list is not “known unarmed”; use `Unknown` or `Partial` coverage, and only populate a capability when it is actually observed or a justified estimate. `Vehicle.DetectingSensors` names the observer’s sensors that detected a contact; it is not the target’s sensor inventory.

The native contact wrapper retains a reference to the real `ObjectBase` and exposes truth-bearing members. The adapter must construct the `UnitInstance` from a selected-side whitelist, such as contact class only when identified and observer-side position estimates. Do not serialize the native wrapper or read enemy equipment through its `BaseObject`. A tagged own-unit/track subject keeps command permission independent from the common data shape.

## Required assessments

1. **One `UnitType` with alternative complete loadouts — sufficient, with normalization.** Native loadout records combine with base sensors/platform data; aircraft and vessel/submarine configurations have different storage shapes. Include date applicability, pod/store additions and explicit resolution coverage. No extra configuration layer is justified now.
2. **Active EW and countermeasures as composed `WeaponType` — sufficient.** Chaff and decoys are ammunition-backed deployments; active ECM is a sustained effect with target/location input and no ammunition. Its native class happens to be a sensor system. Keep passive ESM in `SensorType`. Keep channel capacity separate from ammunition.
3. **Complete catalog enumeration — supported for active file-backed content after metadata load.** The runtime enumerated 642 unit rows, 443 ammunition definitions and 508 sensor definitions beyond the scenario’s two deployed units. Selected mods flow through the same file/cache path. Persistent IDs, malformed or plugin-created content, and a custom mod run remain unverified.
4. **Uniform `UnitInstance` without leaks — feasible with strict observation rules.** The plot table provides estimates and gated class knowledge but retains hidden object references. Use distinct subject identities, field-level `Knowledge<T>`, collection coverage and observer provenance; never populate enemy fit from catalog possibilities.
5. **Reliably obtainable versus unresolved — mixed by subject and field.** Once caches are ready, type keys, loadout alternatives, many nominal ammunition quantities and shared sensor/ammunition definitions are available. The sampled own F-14 exposed current loadout, ammunition, sensor presence and operating states. Full fit closure, ammo aggregation semantics across every weapon family, communications, all sensor performance units, transitions after firing/reload/damage, persistent content identity and enemy equipment remain to be proven.

## Recommended small model clarifications

- Name the helicopter case explicitly or document that `AircraftTypeDescription` includes the native `Helicopter` category and its separate performance fields.
- Preserve loadout service-year/date-based alternatives or identify the date for which a fit is resolved. Keep unknown/unresolved loadout coverage explicit.
- Allow fitted non-weapon stores such as external fuel tanks to appear in a loadout without coercing them into weapon effects or ammunition quantities.
- Treat the catalog revision/hash and stable content-scoped IDs as required when returning/cacheing catalog records.
- Keep `CommunicationFit` optional until its complete native definitions and field semantics are mapped.
