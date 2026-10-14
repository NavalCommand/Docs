# Task 018 — Sea Power runtime classification and observation

**Date:** 2026-10-14
**Status:** Bounded investigation complete; integration remains unimplemented.
**Scope:** Runtime and read-only source investigation. No production API, protobuf schema, codec, adapter, or TacticalDisplay source was changed.

## Findings

1. **The normal contact wrapper does not expose a dimension/category/subcategory ladder.** It exposes a nullable native class key. In the inspected build, the key becomes a single exact class when one candidate remains; the wrapper does not publish the broader candidate set as a parent category. The live contacts we sampled went from no class to an exact native key without a dimension-only or category-only state.
2. **Affiliation is a separate contact field.** Sea Power stores detected side separately from class. In valid live contacts, class and relationship appeared together, while the aircraft run also produced a contact with an empty class value but a known hostile relationship. Do not encode relation as a subtype.
3. **Exact platform identities are visible in selected-side plot tables.** Both sides identified the Perry and Krivak by their exact native INI keys in matched runs. A curated source mapping can refine those keys for NavalCommand; Sea Power itself did not supply a generic “Frigate” or “Destroyer” level in the contact wrapper.
4. **The plot table contained separately simulated object contacts.** The aircraft run exposed a missile, fuel-tank stores and pilot objects, each under separate contact IDs. That run developed into combat despite `WeaponStatus=Hold`, so it establishes that those rows appeared in the observer table, but not the cause or a safe no-fire progression.
5. **There is an unresolved observer leak risk.** `Vehicle.Class` is observer-side contact data, but the inspected class-transfer path uses the true object’s INI name for weapons and forced identification. A populated field alone does not prove that the value is an appropriate faction observation. The runtime probe never read `Vehicle.Object` or `BaseObject`.

The Domain.Api hierarchy remains useful and unchanged. An exact, non-empty, observer-safe native class key can map to a curated `TypeId`; unresolved levels should remain unresolved unless a verified observer-side source supports them. Keep dimension/category/subcategory concepts and alignment separate.

## Baseline and method

- Read [Docs contributor rules](../../AGENTS.md), [architecture](../../ARCHITECTURE.md), [development boundaries](../../DEVELOPMENT.md), and the historical [Task 016 findings](../016/CLASSIFICATION_FINDINGS.md), [Task 013 runtime findings](../013/RUNTIME_FINDINGS.md), [Task 013 static findings](../013/STATIC_FINDINGS.md), and [TacticalDisplay implementation record](../SeaPower-TacticalDisplay/IMPLEMENTATION.md).
- Domain.Api was on `feature/classification` at `57dee1e`. No Domain.Api or other production repository files were edited.
- Sea Power was 0.8.3 Build 261001 Public Release, Unity 6000.0.67f1. `Seapower-Scripts.dll` SHA-256 was `0FE6AAF2A6D9982E7E2EB41FD6035CB97C149773202C90821D71C00A9FDEB01A` before and after the runs.
- ILSpy 11.1.0.9782 was used read-only against the installed assembly. No vendor assembly or decompiled source was added to the repository.
- A task-scoped probe under [Fixture](Fixture/) sampled only `Globals._playerTaskforce.PlottingTable.Vehicles` on `Globals.mainThread`: `Identified`, `Class`, `Side.HasValue`, the relationship calculated from the reporting side and detected side, `HasLiveTrack`, and `PositionEstimate.HasValue`. It did not inspect `Object`, `BaseObject`, target equipment, the global object registry, or authoritative positions. The probe source and build output are evidence artifacts, not production or reusable runtime code.
- Runs used the existing [Start-Live.ps1](../../../SeaPower.Mod/scripts/Start-Live.ps1) in `Harness` mode, so the production NavalCommand runtime was disabled. The harness loaded a temporary fixture DLL and ran local disposable INIs. Existing `LaunchCandidate.dll` and Anchor Chain files were left in place. The Task 002 plugin’s source gates all work on `NC_TASK002_ENABLED`; the launcher cleared inherited `NC_*` variables and did not set that variable.

## Experiment A — contact identification progression

### Static behavior in the inspected build

| Source | Behavior inspected | Confidence and relevance |
|---|---|---|
| `Seapower-Scripts.dll`, `SeaPower.Vehicle.TransferFromComponents` | Reads `ClassInformation` from the contact’s current state. For ordinary classifications, it sets `Vehicle.Class` only when exactly one class bit remains, resolving the bit through `UnitMetadataProvider.UnitIdMap` to `_objectIniName`. Multiple or zero bits leave `Class` absent. | **Verified, high.** The contact wrapper provides an exact key or no key; it does not expose the remaining candidate set as a category/subcategory. |
| `Seapower-Scripts.dll`, `SeaPower.Vehicle.UpdateFromECS` | Sets `Identified.Value` from `Class.HasValue`. The snapshot path likewise transfers a class bit or an override into `Class`. | **Verified, high.** `Identified` is not an independent confidence scale in this build. Validate both the field and that its class string is non-empty. |
| `Seapower-Scripts.dll`, `SeaPower.VehicleClassificationUpdate.OriginalLambdaBody` | Combines/intersects observer classification bits from track history and writes the synthesized `ClassInformation` to the contact state. It may also add `DetectedSide` based on the candidate-nationality set, subject to `_preventTaskforceAnalysis`. | **Verified, high.** Classification and side have distinct storage and synthesis paths. Runtime probe did not inspect the internal ECS bitset. |
| `Seapower-Scripts.dll`, `SeaPower.Vehicle.TransferFromComponents` | For `overrideIdentification` or a weapon object, it fills `Class` from `Object.getObjectIniName()` rather than requiring one observer candidate bit. | **Verified, high.** This reads authoritative object identity on a path that fills an observer wrapper. A populated weapon/override class needs additional observer/UI validation before export. |
| `Seapower-Scripts.dll`, `SeapowerUI.MapUnitViewModel` | The class text path checks `Identified`; otherwise the contact line uses incoming-signal information. The “Information” encyclopedia action is gated on `Identified`; the separate contact-information action is available for a contact. | **Verified by source inspection, not screen capture.** The inspected UI does not establish a dimension-only or category-only display. |

`ClassInformation` contains candidate bits and flags including `overrideIdentification`, `_preventTaskforceAnalysis`, and `isFalse`. That internal ECS state could support further observer-side research, but this investigation did not read it live: its job/update safety and exact relationship to the rendered label were not validated. No partial level should be inferred from hidden target metadata.

### Live results

The matched surface scenario placed a USN Perry and a Soviet Krivak 0.5 NM apart, stationary, with the Perry’s radar active. Each capture lasted 12 wall-clock seconds after the selected side’s plot table became available.

| Observer | Contact timeline (mission seconds) | Result |
|---|---|---|
| Taskforce2 | Own contact began without class/side at `0.0`, then became `wp_skr_krivak1`, Friendly, at `0.1`. Foreign contact had no class/side at `3.0`, then became `usn_ffg_oliver_hazard_perry`, Hostile, at `3.3`. | Exact native keys for both units; no sampled intermediate dimension/category/subcategory. |
| Taskforce1 | Own contact began without class/side at `0.0`, then became `usn_ffg_oliver_hazard_perry`, Friendly, at `0.1`. Foreign contact was first sampled at `3.2` as `wp_skr_krivak1`, Hostile. | Reverse perspective agrees with Taskforce2’s contact identity and relation. |

Both foreign contacts had a live track and position estimate when the exact class and relationship were first logged. The player-side air scenario also began with a blank own row at `0.0`, then recorded `usn_f-14a` and Friendly at `0.1`; its foreign row first appeared as `wp_mig-23mld` and Hostile at mission time `3.3`.

The classification-stage answers for this build and these samples are:

| Requested level | Runtime answer |
|---|---|
| Dimension only | No direct dimension field or such stage was exposed by the sampled `Vehicle` wrapper. |
| Category or subcategory only | No direct field or such stage was observed. The internal candidate bitset was not sampled live. |
| Specific platform class | Yes. The wrapper returned exact platform INI keys, including Perry and Krivak. |
| Exact native unit type | Yes, as a native class key when populated. This is not yet a NavalCommand `TypeId`; map only through an approved catalog mapping. |
| Alignment/affiliation | Separately represented by detected side and the reporting side’s relationship to it. This is not a subtype. |

One additional aircraft contact had `Identified=true`, `Side` known as Hostile, and `Class` present as an **empty string**. Treat this as an invalid/unknown class value, not as a generic classification. It shows that checking only the `Identified` boolean is insufficient.

## Experiment B — information by observing faction

The two surface runs used the same two platforms, positions, environment and observer wrapper; only the scenario’s selected player side changed. Each side’s own unit was classified immediately. The other side’s exact class and hostile relationship appeared together shortly after contact creation. We did not query the opposing side’s table from inside one run, so the measurements remain separated by selected observer as intended.

This establishes symmetric exact-class and relation availability for these two sides in this fixture. It does not establish allied sharing, neutral factions, AI-side plotting tables, taskforce-specific detection ranges, or whether all fields remain available in every mission. The Taskforce2 process printed a native Noesis reflection crash report after the probe had completed its 12-second capture; the owned game was then closed through its normal window path and exited with code 0. No screenshot or widget-interaction claim is made.

## Experiment C — independently simulated objects in the contact table

The longer Taskforce1 aircraft run used the F-14/MiG scenario with both aircraft marked `WeaponStatus=Hold` and no `Attack` task. The run nevertheless developed into a combat/damage event. The probe read only actual rows in the selected side’s plot table; it did not inspect what spawned them or their underlying objects.

| Contact class key observed in the plot table | Active content definition | Observed contact fields | Assessment |
|---|---|---|---|
| `wp_aa-11` | `ammunition/wp_aa-11.ini`, `Type=Missile` | Separate hostile track; class field non-empty, side known, position estimate present at mission time `6.3`. | A missile-class object appeared as a contact. The exact class still crosses the weapon/true-object path noted above, so UI legitimacy remains unverified. |
| `usn_tank_267_f-14` | `ammunition/usn_tank_267_f-14.ini`, `Type=Fueltank` | Separate friendly track IDs; exact key and side became available at mission time `7.3`. | Fuel-tank objects appeared as contacts. The run does not establish the event that created them. |
| `usn_pilot_f-14a` | `ammunition/usn_pilot_f-14a.ini`, `Type=Paratrooper` | Two separate friendly track IDs; exact key and side appeared at mission times `19.2` and `20.2`. | Paratrooper/pilot objects appeared as contacts during the later damage event. Duplicate track rows are not a proven duplicate truth-unit count. |
| Empty class value | No class-key mapping was asserted. | Separate hostile track with `Identified=true`, `Side` known, and an empty `Class` string at mission time `6.3`. | Invalid/ambiguous classification; do not publish a fabricated type. |

All of the above are observed contact-table entries, not contacts synthesized from a global entity list. The aircraft run’s later contact set disappeared by mission time `27.3`. Because `WeaponStatus=Hold` did not keep the scenario isolated, this run is not evidence that the displayed timing is a general passive-sensor progression or that a specific order launched any object.

Task 016’s static findings remain relevant: `ObjectBase.IsTrackableObject` requires a controlled unit or eligible weapon and excludes `Noisemaker`, `ObjectType.Decoy`, `UnitRoles.Deco`, and `UnitRoles.SeaMine` from the non-unit weapon path. That is a filter on possible plot-table membership, not proof of sensor detection. This task did **not** deploy or test MOSS, sonobuoys, noisemakers, mines, other decoys, or biological entities. Their absence from these scenarios is not evidence that they can never be contacts.

## Integration implications

- Preserve `Dimension → Category → SubCategory → TypeId` and the current observer `Resolution` model. The game’s simpler wrapper is a limitation of one source, not a reason to collapse the domain hierarchy.
- A non-empty class key can support an exact mapped type only when the observer provenance and weapon/forced-identification cases are validated. Resolve from the active native catalog plus curated mappings; do not use target `BaseObject` as a fallback.
- When the wrapper has no usable class, keep unresolved levels unknown unless a validated observer-side source supplies a candidate set or lower-level fact. The runtime sample provides no direct generic dimension/category result.
- Keep detected side/relation orthogonal to class. Reject an empty class string even if the native `Identified` flag is true.
- Add an observation only for a real entry in the selected observer’s contact system. The observed missile, fuel-tank and pilot rows demonstrate why independently simulated objects cannot be excluded merely because they are not ordinary platforms; each semantic mapping still needs its own validation.

## Confidence, limitations, and follow-up

- **Verified, high confidence:** installed assembly identity; source behavior of `Vehicle`, `VehicleClassificationUpdate`, and the UI class-text gate; exact class and side/relation values in the two matched surface runs; separate missile, fuel-tank and pilot class keys in the aircraft plot table.
- **Inferred, medium confidence:** that the exact contact key is suitable for the visible UI or for NavalCommand observation in the weapon/override branches. The native wrapper populates it, but the source reaches the true object and this task captured no UI screenshot.
- **Unverified:** dimension/category-only classification through internal candidate sets; MOSS/sonobuoy/noisemaker/mine/decoy/biologic contact membership; contact loss/reacquisition semantics; allied and AI-side views; whether the unusual aircraft contacts are exposed the same way in the rendered display.
- The Taskforce2 runtime printed a Noesis native reflection crash report after its capture; no UI visual acceptance is claimed. The installed BepInEx loader also reported its previously known `UnityLogWriter` internal-call incompatibility. Both runs reached readiness and the cleanup receipts record normal exit.
- The disposable probe compiled successfully against the installed game references with two `MSB3277` version-conflict warnings (`System.IO.Compression` and `System.Net.Http`). No unit-test suite was run for this investigation.

Run receipts, source, scenarios, build output, package manifests, and raw logs are retained in [Task 018 evidence](../../Evidence/Task018/). Each live run restored `doorstop_config.ini` to SHA-256 `3B5FBCF2C104F967F63687A0FD240CB79A5253D08F4338A358EE796C17B5AA0A`; the game assembly hash remained unchanged; all temporary `NavalCommand.*` plugin directories were removed; and no owned Sea Power process remained.
