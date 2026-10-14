# Task 020 — Observer Classification, UI Presentation, and Destruction Status

**Date:** 2026-10-14

**Status:** Static source trace and typed recorder build complete; one authorized short Harness run verified the typed observer-state path on a live contact. Ordinary non-override classification and live UI text/color remain unverified.
**Scope:** Investigation only. No production API, protobuf schema, codec, adapter, or TacticalDisplay code was changed.

## Executive findings

1. Task 019's `reflection-unavailable` marker was caused by looking up `Vehicle.Entity` as a property. In Sea Power 0.8.3 Build 261001 it is a public `Unity.Entities.Entity` field. The other required types and generic ECS methods resolve. The correct path is `Vehicle.Entity` → `VehicleComponent.CurrentState` → observer-state components.
2. `ClassInformation` is a candidate bit set on the contact's current observer state. The classification system combines/intersects candidate sets from the contact's current and prior states and track history. The ordinary wrapper path exposes a `Vehicle.Class` key only for one remaining candidate; it does not expose intermediate classification levels. Weapon and override paths can copy the true object's INI identity, so `Vehicle.Class` alone is not a safe export condition.
3. The pre-identification `VisualType=Submarine` map symbol comes from `MapUnitViewModel.GetMapType(ObjectBase)`, which switches on the underlying object's native family. `IncomingSignalInfo()` is a separate text path; it reads the best track's `IncomingSignalData`, but for non-sonar/non-ESM contacts it also reads `Object._sensorData._size`. Neither the map type nor the combined text is a safe classification source.
4. Sea Power's `DetectingSensors` is an observer-side modality bit set aggregated from active track records. The UI localizes it into source labels such as visual, radar, sonar, ESM, and MAD. `HasLiveTrack` independently evaluates whether the position's associated `TrackComponent` is fresh. `IncomingSignalInfo()` has its own best-track cache and does not check `HasLiveTrack`; the Task 019 82-second mismatch is therefore possible, but its exact runtime cause remains unverified.
5. The black symbol is sourced from authoritative state. `MapContactVisuals.Resolve(IUnit)` reads `unit.BaseObject.IsDestroyed` and overrides the relation/dormant color with `TM_DestroyedColor_Brush`. The installed default day palette defines `DestroyedColor=255,0,0,0` (opaque black); its night palette uses gray. The map resolver has no observer-confirmed-destruction component or kill-confidence check.
6. The plotting-table contact has a separate age-out lifecycle. Sea Power can keep a destroyed object's contact plotted while its object reference and position state remain. The age-out job uses missing object-reference and position state, position age, and family-specific expiry thresholds; it does not check `ObjectBase.IsDestroyed`.
7. The corrected strongly typed fixture read a real selected-side contact during one authorized short run. Contact 7001 yielded one candidate bit (ID 611), a valid detected-side taskforce reference, and separate sensor/freshness transitions. The contact had `overrideIdentification=true`, so this does not validate the ordinary non-override class path. The run did not populate contact-info text/color properties or exercise destruction.

The NavalCommand hierarchy remains `Dimension → Category → SubCategory → TypeId`. A less expressive game wrapper is a source limitation, not a reason to collapse the hierarchy.

## A. ECS instrumentation correction

### Exact Task 019 failure

`Task019ClassificationRecorder.CacheReflectionMembers()` called `typeof(Vehicle).GetProperty("Entity", ...)`. In the installed assembly, `Vehicle.Entity` is a public field of type `Unity.Entities.Entity`, and `GetProperty("Entity")` returns null. `DescribeObserverEcsState()` treated that null as a general prerequisite failure and returned `reflection-unavailable` before trying to read any contact ECS state. That explains all 597 markers.

The remaining prerequisites are present in this build:

| Prerequisite | Installed member |
|---|---|
| Contact entity | Public `Vehicle.Entity : Entity` field |
| Vehicle component | Public `SeaPower.VehicleComponent` struct; public `CurrentState : Entity` field |
| Component read | Public generic `EntityManager.GetComponentData<T>(Entity)` |
| Component presence | Public generic `EntityManager.HasComponent<T>(Entity)` |
| Classification state | Public `SeaPower.ClassInformation` struct; public `Bits`, `overrideIdentification`, `_preventTaskforceAnalysis`, and `isFalse` members |
| Detected side | Public `SeaPower.DetectedSide` struct; public `Side : Entity` field |

### Correct state path and source behavior

The replacement fixture in [Fixture](Fixture/Task020ObserverRecorder.cs) uses typed ECS calls on the game thread, guarded by `Globals.mainThread == Thread.CurrentThread`:

```text
Vehicle.Entity
  └─ GetComponentData<VehicleComponent>()
       └─ CurrentState
            ├─ ClassInformation
            ├─ DetectedSide
            ├─ DetectedPosition / EstimatedPosition
            └─ DetectingSensors
```

This matches `Vehicle.UpdateFromECS()` and `Vehicle.TransferFromComponents()`. The classification job reads `ClassInformation` from the contact's current/last state and track entities, combines/intersects the sets, and writes the result to `VehicleComponent.CurrentState`. `VehicleSideUpdate` similarly rolls side state from contact history/track records into the current state. The fixture records candidate words and candidate bit indexes, all three identification flags, the detected-side entity and `TaskforceReference` presence, current-state identity, sensor-source flags, live-track/dormant state, and same-timestamp view-model text/color. It records changes plus periodic snapshots through a bounded queue.

The fixture does not read `Vehicle.Object`, `BaseObject`, authoritative positions, or a global entity list. It records only whether `Vehicle.Class` is present, not its key, so the investigation can compare the wrapper's identification transition to candidate bits without exporting a key from a weapon/override path.

`Vehicle.TransferFromComponents()` has two material provenance hazards:

- With `ClassInformation.overrideIdentification=true`, or for a weapon, the wrapper sets `Vehicle.Class` from `Object.getObjectIniName()` rather than from a singleton observer candidate bit.
- When a `DetectedSide.Side` entity lacks `TaskforceReference`, the wrapper falls back to `Object._taskforce`. The new fixture logs the component entity and reference presence, and only interprets the wrapper side when the component link is present.

`ClassInformation.isFalse` changes how the classifier combines false-contact evidence. Its meaning must remain explicit; do not treat a singleton candidate as trustworthy while ignoring this flag. `_preventTaskforceAnalysis` controls the nationality-analysis route that can add `DetectedSide`.

**Runtime verification:** the corrected fixture compiled and then read observer state from contact `7001` in the Taskforce1 plotting table during one authorized short Harness run. Across 54 contact records from mission time 3.223 to 49.616 seconds, `ClassInformation` remained present with one candidate (`candidateIds=[611]`), `isFalse=false`, `_preventTaskforceAnalysis=false`, and `overrideIdentification=true`. The wrapper reported `Class` present and `Identified=true`, but the fixture intentionally did not log its key. Because the override flag selects a provenance-sensitive wrapper path, this run does not prove that an ordinary classifier candidate safely supplies the wrapper's exact class.

The same contact had a `DetectedSide` component whose entity existed and had `TaskforceReference`; the wrapper resolved `Enemy` / `Hostile`. `HasLiveTrack` was initially true while an estimate and Visual/SearchRadar/ESM sources were present. At mission 42.468 it was false while the estimate, candidate, and side remained; at 43.186 the contact was dormant and `DetectingSensors` was Unknown. The row had not aged out when the session ended. `ContactInfoLine1`–`4` and `UnitColorBrush` stayed null throughout, so the short run did not validate displayed label or color text. Contact-only evidence and cleanup receipts are in [Task 020 evidence](../../Evidence/Task020/README.md).

BepInEx reported that its Unity log writer could not start because the Unity internal log calls were unavailable. The Harness still signaled ready, the recorder's separate file writer completed the capture, and its last queue summary reported zero dropped records. The game exited normally and the launcher completed cleanup.

## B. Classification source mapping

| Player-visible information | Native source | Observer-safe? | NavalCommand representation |
|---|---|---|---|
| Pre-identification `Submarine` map symbol | `MapUnitViewModel` initializes `VisualType` using `GetMapType(ObjectBase)`; the method switches directly on `ObjectBase._type` and related implementation fields. | No. This is the underlying object family exposed through presentation. | `Type=Unknown` unless observer candidate data independently supports a classification level. Do not derive `SeaSubsurface` from `VisualType`. |
| “Visual: semi small (V)” cue | `ContactInfoLine2` calls `Vehicle.IncomingSignalInfo()` while the contact is not identified. It reads localized `IncomingSignalData._contactType`; non-sonar/non-ESM paths append `Object._sensorData._size`. | Mixed and not safe as a whole. The method combines a track signal with an authoritative object-size lookup. | Keep `Type=Unknown`; preserve only separately validated observer track facts. |
| Exact model/class text after identification | `MapUnitViewModel.UpdateContactDisplay()` gates the class-name line on `Vehicle.Identified`; it displays `BaseObject.ClassNameShort` or a native variant name. | The UI line is not a trusted type source. The gate does not exclude the wrapper's weapon/override identity paths. | Map only a validated ordinary candidate key to a curated `TypeId`; otherwise Unknown. |
| Partial classification candidates | `ClassInformation.Bits` on `CurrentState`, produced by `VehicleClassificationUpdate` from current/previous contact state and track evidence. | Observer-state data by design, subject to `isFalse`, `overrideIdentification`, and the ordinary/weapon source distinction. The short live run read one candidate (ID 611), but the same contact had `overrideIdentification=true`; no multi-candidate or ordinary-path case was sampled. | Map candidates only after their provenance and active-content mapping are validated. Emit a level only when every safe candidate agrees there. A singleton may yield `TypeId` only on a validated ordinary path; keep the tested override path Unknown. |
| Hostile/Enemy posture and alignment | `Vehicle.CurrentRelationship()` uses `UnitTaskforce`/detected-side state or `ForcedRelationState`; the map converts that relation to posture/color. `VehicleSideUpdate` stores `DetectedSide` on the observer state. | The ECS component is an observer-system output, but wrapper fallback can read `Object._taskforce` if its side reference has no `TaskforceReference`. `VisualPosture` itself is a UI property. | Populate `ObservedFaction` only from a verified detected-side reference and selected-observer relationship. Keep it independent of `Type`. Do not copy `VisualPosture`. |
| Visual/sonar source label, including a possible `V+S` | `VehicleSourcesUpdate` aggregates sensor modalities from non-expired track records into `DetectingSensors`; `SensorSystem.MakeSourceDescription()` localizes those flags. `ContactInfoLine3` presents `IncomingEmissionsInfo` when the corresponding layer is visible. | The active modality set is observer-side. The displayed line is presentation and is visibility-gated. | Existing `Contributors` can carry sensor IDs only when actual sensor contributors are identified. A modality flag alone is not a sensor `TypeId`; otherwise leave contributors empty. It does not refine target classification. |
| Faded/dormant symbol | `VehicleAgeOut` adds `DormantVehicle` when the position fix ages beyond its max-age threshold; `Vehicle.IsDormant` reflects that component; `MapContactVisuals.Resolve()` selects `TM_DormantColor_Brush`. | Observer contact freshness/lifecycle presentation, separate from target life and identity. Runtime correlation to the owner's fade was not captured. | Keep freshness separate from classification. The current observation contract has no contact freshness field; do not encode it in `Type` or `ObservedFaction`. |
| Black destroyed symbol | `MapContactVisuals.Resolve()` reads authoritative `BaseObject.IsDestroyed` and selects the destroyed-color brush. | No. This is a privileged simulation-state read made by the UI. | No current observer field supports it. Leave destruction unknown; do not export `IsDestroyed` or the color. |

The game permits skipping intermediate levels in the domain model. A candidate set whose mapped types all share one curated category or dimension can support that level directly; no artificial sequence of display stages is required. A cue or symbol that comes from `BaseObject` does not supply such a candidate set.

## C. Tactical Display presentation and detection

### Symbols and contact text

`MapUnitViewModel` publishes `VisualType`, `VisualPosture`, `UnitColorBrush`, and four contact-information lines. Its type-selection method switches on the underlying `ObjectBase` native family: a submarine object maps to `MapVisualType.Submarine`; a torpedo maps to `Torpedo`; a sonobuoy implemented as a bomb is recognized from its ammunition parameters; decoy/noisemaker entries are also mapped through object family and relationship. Those map categories are not the semantic Domain classification and are not evidence that the observer knows the underlying family.

`MapContactVisuals.Resolve()` supplies posture and brush. For a `Vehicle`, the relation is taken from the detected taskforce wrapper when present, or from `ForcedRelationState` when no detected taskforce is available. This explains how a UI posture can exist without an exact class. Task 019 recorded `VisualPosture=Enemy` before `Vehicle.Side` was populated, but did not record the ECS state or forced relation. The new fixture records the forced-relation component so a live repeat can identify that specific path.

The map's exact class-information line is a separate branch: it shows a base-object class/name only after `Identified`; otherwise it calls `IncomingSignalInfo()`. That method gets the selected `_bestTrack`'s `IncomingSignalData`, localizes `_contactType`, and formats the signal value. For non-sonar/non-ESM sensors, it also reads the underlying object's `_sensorData._size`. The string can be useful as a player-visible cue, but it is not a safe replacement for observer classification state.

### Sensor labels and freshness

`VehicleSourcesUpdate` walks the vehicle's track buffer, ignores track records older than each `TrackComponent.MaxAge`, ORs the active source modalities into `DetectingSensors`, and computes strongest sonar and ESM values. `SensorSystem.MakeSourceDescription()` maps those flags to localized labels (`DetectedByVisual`, `DetectedByTVS`, `DetectedByRadar`, `DetectedByARS`, `DetectedBySonar`, `DetectedByESM`, and `DetectedByMAD`) joined by the configured separator. The sonar mask combines active and passive sonar. If the owner's “V+S” note refers to that source line, it indicates visual plus sonar sources, not a target class; the note alone is insufficient to identify the exact UI line.

`HasLiveTrack` has a separate rule: a detected position must exist and its associated `TrackComponent` age must be within `MaxAge`; the own-unit self-track is a special case. `PositionEstimate`, `Class`, and `DetectedSide` can remain populated while `HasLiveTrack` is false. `IncomingSignalInfo()` can return its cached text for the same best-track entity and does not test `HasLiveTrack`. Thus the owner's reported sound label and the later wrapper freshness transition can differ without contradiction. The Task 020 run sampled sensor flags and freshness on one mission clock, but its contact-info line stayed null and it did not reproduce the reported sound-reacquisition sequence; the exact 82-second difference remains unresolved.

For the faded track, `VehicleAgeOut` uses the age of its latest detected/estimated position: it marks the contact dormant after the fix exceeds `MaxAge × dilation`, and the map selects a separate dormant brush. The installed default day color is ARGB `255,151,152,153`, an opaque gray. This is a plausible source of a visually faded symbol, but Task 019 did not record `IsDormant`; the exact cause of that specific manual observation remains unverified. It is not a direct `HasLiveTrack` alpha/fade rule in the inspected `MapContactVisuals` path.

The short Task 020 run independently showed these states can diverge on one real contact: at mission `42.468`, `HasLiveTrack=false` while `PositionEstimate` and the candidate bit remained; by `43.186`, `IsDormant=true` and `DetectingSensors=Unknown`, while the row, candidate, and detected side were still present. This validates the wrapper/ECS separation for that scenario. It did not capture rendered alpha, a non-null info line, or a UI color.

## D. Destroyed-contact status

### Source trace

```text
ObjectBase.IsDestroyed (authoritative isDestroyed flag)
  → MapContactVisuals.Resolve(IUnit)
      → TM_DestroyedColor_Brush overrides relation/dormant brush
          → MapUnitViewModel.UnitColorBrush
              → Tactical Map symbol
```

The map resolver reads `unit.BaseObject.IsDestroyed` before posture selection and checks it again after relation colors are selected. If true, the final brush is the destroyed brush. The installed `UI_Parameters.TacticalUI_ReadUIParameters()` reads `DestroyedColor` from the tactical-map color settings and constructs the brush. In the installed default daytime settings, `DestroyedColor=255,0,0,0` is opaque black; the configured night value is `NightDestroyedColor=255,151,152,153`, gray. The state's meaning is consistent in this resolver, while its color depends on the active theme/settings.

This is authoritative destruction presented in the player's UI, not a distinct observer-side confirmation. The resolver has no hit report, kill-confidence, or faction knowledge check; it reads `BaseObject.IsDestroyed`. The owner-reported black symbol therefore matches the actual destruction branch in the installed build, but it is not safe to treat as a replicated observer fact. The short Task 020 runtime scenario did not destroy a unit and its view-model brush was null, so the color conclusion is static-source evidence plus the owner's Task 019 observation. The resolver accepts the shared `IUnit` interface and applies the same destroyed override to each resolved unit; no separate ship/submarine/aircraft runtime matrix was run.

### Why the contact remains plotted

`VehicleAgeOut` ages the observer's position fix and schedules contact deletion for a missing underlying `SPObjectRef`, no remaining detected/estimated position, or a position fix beyond its family-specific death-age limit. It separately marks a contact dormant after the shorter position-age threshold. The age-out method does not inspect `ObjectBase.IsDestroyed`. Consequently, the UI can render a destroyed color while the contact row remains in the plotting table. Task 019 observed exactly that persistence at the row level: contact `7001` remained through `10:36:51.7`.

`HasLiveTrack` also does not consult `IsDestroyed`, so a destroyed object may still have a fresh track; conversely, an undestroyed contact can have no fresh track. Table membership, detection freshness, object survival, identity, alignment, and UI destruction color are separate facts.

### Domain representation

`Observations::Api::Instance` currently has no destruction/liveness property. `Equipment::Api::Damage::Destroyed` applies to equipment state and is not an entity destruction status. Do not add a Domain field in this task or map the UI color into an existing property. If NavalCommand later needs observer-confirmed destruction, it needs a separate architectural discussion covering confirmation semantics, confidence/uncertainty, and source attribution.

## E. Updated Task 019 interpretation

The owner correction is recorded in [Task 019 findings](../019/IDENTIFICATION_RUNTIME_FINDINGS.md#supplemental-owner-correction--2026-10-14). It is explicitly labeled as a manual UI observation. The prior JSONL and its hashes were not changed. The native recorder independently proves that the plotting-table row persisted and did not record color or destruction status. Task 020's static source trace explains the black UI as authoritative `IsDestroyed` presentation; it does not convert the earlier recorder output into destruction evidence.

## F. Recommended integration mappings

| Concern | Recommendation | Existing contract support |
|---|---|---|
| Classification | Preserve `Resolution<TypeId, SubCategory, Category, Dimension>`. The live run proves the corrected fixture reads candidate bits, but its single candidate had `overrideIdentification=true`; it does not validate ordinary-path exact classification. Map candidates only through curated, active-content mappings after provenance validation. Emit exact `TypeId` only for a singleton ordinary observer candidate with safe flags/source. Keep empty, disjoint, false, override, and weapon-truth paths Unknown until validated. | Supported by existing `Resolution` including direct dimension/category/subcategory values. No Domain change is indicated. |
| Alignment | Resolve `DetectedSide` through its `TaskforceReference` and calculate relation from the selected observer. Keep it orthogonal to `Type`; do not use `VisualPosture` as the source. The short runtime run verified the reference and `Enemy` / `Hostile` relationship. | `ObservedFaction` already represents a known faction independently. |
| Detection sources | Treat the `DetectingSensors` set from active, non-expired tracks as modality evidence. The short run observed Visual, SearchRadar, ESM, and PassiveSonar, then Unknown as tracks aged. Do not infer target class from those labels. Populate `Contributors` only when actual contributing observer/sensor identities can be mapped, not from modality names alone. | `Contributors` can carry observer and sensor type IDs. It has no freshness timestamp or active/inactive lifecycle. |
| Detection freshness | Keep `HasLiveTrack`/track age independent of identification and position estimate. | No observation freshness/status field exists. Any explicit freshness contract would require separate API discussion; do not overload `Type` or `ObservedFaction`. |
| Contact lifecycle | Create contacts only from the selected faction's plotting table. Keep them while Sea Power keeps the row; remove them when the row is removed. Contact retention does not prove survival. | Observation identity/list membership already supplies contact lifecycle at the provider boundary. The `Instance` value has no stale/destroyed status. |
| Destruction | Do not copy `BaseObject.IsDestroyed`, black color, or `IsDormant` into an observer kill fact. Preserve the contact if its row remains and leave destruction unreported. | No appropriate destruction-status field exists. Any observer-confirmed kill model needs separate architectural discussion. |

These recommendations do not change the approved semantic mappings for MOSS decoys, sonobuoys, or native implementation families. The Task 020 run has not tested those entities.

## G. Integration readiness

**Safe observation portions are ready for conservative integration; full classification integration remains blocked on ordinary-path provenance.** Static analysis establishes the Task 019 failure, the correct typed access path, the source of the pre-identification symbol and contact text, the sensor/freshness separation, and the authoritative destroyed-color leak. The authorized short live run verified candidate-bit/flag reads, a valid detected-side reference, sensor modalities, and same-clock position/live/dormant transitions without reading target objects.

The only live class candidate had `overrideIdentification=true`, so its singleton bit cannot validate ordinary exact-class assignment. Do not publish a `TypeId` from that path; leave resolution Unknown unless a separate safe observer source supports a broader level. The short run also left UI text/color properties null and did not test destruction. Contact membership and verified side can proceed using existing contracts. Emit detection contributors only after mapping actual observer/sensor identities; the captured modality flags alone do not supply those IDs. `HasLiveTrack` may remain adapter-internal because the observation contract has no freshness field. Full exact classification mapping needs an ordinary non-override contact or another validated observer-owned source. Freshness and observer-confirmed destruction remain separate future contract discussions; no new fields are justified by this evidence.

### Verified and unverified

- **Verified statically:** installed Sea Power build and assembly hash; the `Vehicle.Entity` field/property mismatch; typed ECS member availability; current-state component path; class/side system behavior; map type/text/color source paths; default day/night destroyed colors; age-out separation; existing Domain observation fields.
- **Verified offline:** Task 020 recorder compiles Release / `netstandard2.1`; two game-reference version-conflict warnings remain. The authorized short launch used Harness mode and the Task 020 fixture only; production runtime was disabled.
- **Verified at runtime:** the typed ECS path returned a real selected-side contact's candidate words/ID and flags, a referenced detected side, active sensor modalities, and same-clock estimate/live/dormant transitions. The game exited normally; the original loader hash was restored and all temporary NavalCommand plugin directories were removed.
- **Still unverified:** ordinary non-override class provenance and multi-candidate consensus; visible contact-info text and symbol color from a non-null view model; destroyed-state rendering in a runtime scenario; cross-family destruction behavior. The owner confirmed that the scenario ran automatically and closed without issues, but supplied no additional UI observation from this short run.
- **Not required for this integration:** reproduce the full Task 019 engagement, test every entity family, infer destruction from presumed hits, or reproduce every Tactical Display theme/template detail.

## Scope and changes

Only this report, the Task 019 documentation correction, the Task 020 task-scoped fixture, and non-proprietary Docs evidence are in scope. No production API, adapter, protobuf schema, game assembly, gameplay order, mission file, or TacticalDisplay source was modified. Proprietary assemblies and decompiled vendor source are not included in the evidence.
