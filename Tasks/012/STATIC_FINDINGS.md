# Task 012 — Initial static findings

**Status:** Partial static investigation. The managed UI path and principal calculation semantics are mapped below. Runtime validation (Gate C) and API feasibility/design (Gate D) have not been performed. Task 012 remains open.

## Inspection baseline

- Installed game: Sea Power **0.8.3 Build 261001 Public Release** (1 Oct 2026); Unity **6000.0.67f1**.
- `Sea Power_Data/Managed/Seapower-Scripts.dll`: 8,647,680 bytes; assembly version **0.0.0.0**; SHA-256 **`0FE6AAF2A6D9982E7E2EB41FD6035CB97C149773202C90821D71C00A9FDEB01A`**. This matches the Task 001–003 inspection baseline.
- Static inspection used ILSpy command-line **11.1.0.9782**. Decompiled files were temporary inspection material and are not part of this deliverable.
- Read the permanent `Docs/AGENTS.md`, architecture/module/code-style/local-source instructions, Task 001–003 findings, Task 006 UI-order path, and the Task 008 specification. No Task 008 implementation work was done.
- No game launch, live testing, instrumentation, production changes, or API/schema changes were made.

## UI call path

```text
SeapowerUI.ViewModels.WeaponEntry (one entry per ammunition group)
  -> Command
  -> SeapowerUI.ViewModels.AttackingState.TryStart(..., source, ammunition, ...)
  -> push AttackingState; MouseControlState action = EngageWith
  -> AttackingState.MouseAction over MapUnitViewModel
       -> target = mapUnit.Unit.BaseObject
       -> compatibility/seeker checks; target preview updates
       -> UpdateDisplayedRange(target, ammo) on target change / ~0.2 s cadence
          (AttackingState.Update also refreshes about once per unscaled second)
       -> ObjectBase.GetMaxEngagementRanges(...)
       -> NoEscapeRangePrecise, CalculateHitChance (or gun/CIWS override),
          CalculateExpectedDeviation
       -> MapUnitViewModel.UpdateContactMouseoverInfo(...)
       -> ContactMouseoverInfo / range-label view-model properties change
```

`WeaponEntry` carries an ammunition identity, not a selected launcher. `ObjectBase.GetWeaponSystemsForAmmunition` is later used to aggregate compatible systems; therefore these values are not necessarily for one uniquely selected mount. `TryStart` rejects invalid source/ammunition and pushes the transient order state, but does not require a positive ammunition count. The later order action checks stock and target compatibility.

On hover, `AttackingState.MouseAction` calls `UpdateWakeHomingForCurrentAim` and `UpdateTargetPreview`, then checks seeker and ammunition compatibility. A hard incompatibility clears the prior target's mouseover text and marks the cursor unavailable. A target with situational engagement issues can still have a readout while the cursor is unavailable. The UI text is therefore not itself proof that an order will be accepted.

The contact mouseover string and map range-label properties are produced in managed code. The final Noesis/XAML resource binding and exact on-screen placement were not identified in this static pass; those need UI/runtime confirmation.

## Identified display fields

Labels below are from the installed English `StreamingAssets/original/language_en/ui.ini`. Other languages may differ. “Hover text” means the rich-text value assembled by `MapUnitViewModel.UpdateContactMouseoverInfo`; “map label” means the range annotation exposed by `AttackingState` properties.

| Surface / exact label | Value, units and formatting | Visibility / interpretation |
|---|---|---|
| Map label — `${WeaponName} Min range: ${Distance} ${NauticalMiles}` | Numeric distance, Nmi, at most one decimal; weapon name is the ammunition display name | `MinRangeInLat > 0`. Taken from the selected ammunition's `_launchRangesInUnity.x`; it is a nominal minimum launch boundary. |
| Map label — `${WeaponName} Max range: ${Distance} ${NauticalMiles}` | Numeric distance, Nmi, at most one decimal | `MaxRangeInLat > 0`. Without a hovered target it is built from the precise kinematic/sensor envelope (and FCR cap where applicable); with a target it is the minimum of kinematic, adjusted sensor and terrain ranges. It is not simply catalog maximum range. |
| Map label — `${WeaponName} No-escape range: ${Distance} ${NauticalMiles}` | Numeric distance, Nmi, at most one decimal | `NoEscapeRangeInLat > 0`. Target-specific result; the map ring is suppressed for zero, negative or unsupported results. |
| Map label — `Radar horizon (20 ft target): ${Distance} ${NauticalMiles}` | Numeric distance, Nmi, at most one decimal | `HorizonMaxRangeInLat > 0`. In the no-hover state it uses a reference horizon and is shown only when lower than the launch range. On hover it is updated for an FCR-horizon-limited weapon using `FcrTrackedAltitude(target)` and shown only when it caps the displayed range. The fixed English label can therefore describe a target-adjusted value. The separate `Min radar horizon` localization key exists, but no use was found on this `AttackingState` path. |
| Hover — `Hit Chance:` | Percentage, rounded to integer `%`, color-coded | Only when the calculated score is below 100 and `OptionsManager.HitChanceDisplay == Full`. Requires the contact to be classified and identified for calculation. This is not always a probability of hitting; see semantics below. |
| Hover — `Effectiveness:` | `Poor`, `Moderate`, or `Good`, color-coded | With display mode `Rough`, if the score is below 100. `GetMissileEffectiveness` maps <37 to Poor/red, 37–<75 to Moderate/yellow, and >=75 to Good/green. `Off` suppresses the chance/effectiveness line. The helper name says “Missile,” but the caller supplies the result of the general display calculation, including gun/CIWS overrides. |
| Hover — `50% Radius:` | Expected deviation radius, rounded integer feet (`Ft`) | Shown when `CalculateExpectedDeviation(...) > 0`. It is an impact-spread/deviation estimate, not a range boundary. |
| Hover — `Time Of Flight:` | Seconds, rounded integer (`S`) | Shown when the returned time is >0 and the range-information group is allowed by the track-uncertainty condition below. |
| Hover — `Kinematic Range:` | `limit/current Nmi`; zero decimals when current range is at least 10 Nmi, otherwise one decimal | Shown when the kinematic-range percentage is <100 and the range-information group is allowed. This is a colored relative range comparison. |
| Hover — `No-Escape Range:` | `limit/current Nmi`, same Nmi formatting | Shown when the no-escape percentage is >=0 and the range-information group is allowed. A supported zero result may appear in hover text even though its map ring is hidden. |
| Hover — `Sensor Range:` | `limit/current Nmi`, same Nmi formatting | Shown when the sensor-range percentage is <100 and the range-information group is allowed. |
| Hover — `LOS Range:` | `limit/current Nmi`, same Nmi formatting | Shown when terrain/line-of-sight percentage is <100 and the range-information group is allowed. |
| Hover — engagement issues | Localized lines: `Obscured Vision`, `In Lookdown`, `Not Emitting`, `Incorrect Emitted Band`, `Jammed`, `Backup sensor`, `Out Of Altitude`, `Target Too Fast`, `Target Too Deep`, `Can't See Target`, `Defensive Jamming`, `Home on Jam` | Each line is present when its corresponding `ObjectBase.EngagementIssue` is returned. `Home on Jam` is green; other recognized issues are yellow. Some issues are removed by the unidentified-target display adjustment described below. |
| Target-specific wake-torpedo preview — `Total distance:`, `Total time:`, `Wake velocity:`, `Hit Chance:` | Distance in Nmi (F1), time in seconds, wake velocity in knots (F0); chance value is `High`, `Moderate` or `Low` | Only for a previewable wake-homing torpedo target with a localized contact and a successful profile/solution. The “Hit Chance” text is a bucketed geometry/safety-offset heuristic (High above 0.75 Nmi, Moderate above 0.4 Nmi, otherwise Low), not a probability. It is distinct from the hover “Hit Chance”/“Effectiveness” line. |
| Target preview cue | Valid/invalid bearing or target-preview geometry and cursor availability; no additional numeric field in the ordinary target preview | Depends on selected ammunition, seeker compatibility, target compatibility and preview solver success. Wake-homing preview additionally checks a localized contact. |

The range/TOF hover group is emitted only when `Unit.ErrorEllipse == null` or its semi-major axis is less than 10% of the kinematic range. That condition suppresses TOF and all four range lines, but not the chance/effectiveness, bomb radius or issue lines. Full/rough/off chance display is controlled by the player's `HitChanceDisplay` option. `NumberToColorConverter` maps the full chance score to red through green at raw thresholds 16/32/48/64%; range ratios use thresholds 40/80/120/160%. Range lines are colored ratios against current distance, not a separate fire-permission flag.

## Calculation sources and meanings

| Display result | Native source / calculation | Static interpretation and limits |
|---|---|---|
| Current target envelope | `AttackingState.UpdateDisplayedRange` calls `Source.GetMaxEngagementRanges(target, ammo, requireOpenSensorChannel:false, shortCircuitTerrainCheck:false, usePreciseKinematics:true)`. `SeaPower.ObjectBase.GetMaxEngagementRanges` composes `GetKinematicRange`, `GetSensorRange`, `GetTerrainRange`, time-of-flight and `SensorData`. | The map max is `min(KinematicRange, adjusted SensorRange, TerrainRange)`. The target distance and range denominators are computed from live transforms: 3D distance for sensor/LOS comparisons, horizontal distance for kinematic/no-escape and displayed Nmi denominator. Hover passes `shortCircuitTerrainCheck:false`; `GetTerrainRange` can use Unity `Physics.Raycast`. For vertically separated targets, the different 3D/horizontal denominators can make the rendered ratios non-intuitive; confirm against the live UI. |
| Kinematic range | `ObjectBase.GetKinematicRange` and `AmmunitionParameters.MaxRangePrecise` (precise mode is explicitly requested). | Uses target's current velocity vector, air/target category, current position/altitude and evasive-target assumptions. This is a modeled target-adjusted kinematic limit, not just the ammunition's nominal maximum. |
| Sensor range and issue list | `ObjectBase.GetSensorRange` and its `SensorData`; hover code adjusts the returned sensor range for some unidentified contacts. | If unscreened sensor range exceeds the current sensor range and the target is unidentified and farther than the unscreened range, the UI substitutes the unscreened value and removes `DefensiveJamming`. For an unidentified target, `HomeOnJam` is removed unless the source is offensively jammed by that target in a matching passive-radar band. These presentation adjustments do not replace the target object with track estimates. |
| No-escape range | `AmmunitionParameters.NoEscapeRangePrecise(source, target, cappedMax)`. | Returns unsupported (`-1`) except for a missile and air target. For supported cases, it searches the capped range with `MissileSimulator.EstimateShotNoEscape`, using launcher and target positions/velocities plus target `GetFleePerformance` (speed, G, velocity gain, escape altitude and dive angle). It is a target- and current-geometry-specific modeled boundary, not a universal weapon property. The search uses a bounded/bisection calculation; a zero means no positive catchable range was found within the tested cap. |
| Hover chance/effectiveness | `AmmunitionParameters.CalculateHitChance(source,target)`; for a weapon/air target when source has weapon systems, `AttackingState` overrides it with `WeaponSystemCIWS.PredictCombinedKillChance` or `WeaponSystemGun.PredictCombinedKillChance`. | The generic calculation multiplies included model factors: for an air target and missile it includes `Utils.GetInterceptionChance`, which adjusts ammunition `_killProbability` for guidance penalty, difficulty, size, speed, altitude and SAM defense; it also models ECM/jamming and missile-target chaff where applicable. With no included factors the empty-factor product is 1 (100%). Thus the name `Hit Chance` is not a calibrated, universal P(hit). The gun/CIWS branches instead estimate combined kill chance from expected hits/damage and target durability. |
| Gun combined chance | `WeaponSystemGun.PredictCombinedKillChance` combines matching gun systems' `PredictAaEngagementExpectedHits` and damage against target durability. | Uses a static cached result for the same shooter/target/ammunition for <0.5 mission seconds; the key does not include a target-state version. Its shell predictor uses a local fixed-seed `System.Random(783241)` for sampled salvos, so the inspected hover path does not advance `Globals._rnd` through that predictor. Cache staleness and timing need live validation. |
| CIWS combined chance | `WeaponSystemCIWS.PredictCombinedKillChance` and its expected-hit/kill calculation. | Read the direct prediction path; the complete transitive side-effect audit was not finished. Do not assume it shares the generic missile-hit semantics. |
| Bomb radius | `AmmunitionParameters.CalculateExpectedDeviation(source, target position, target)`. | Target-dependent expected deviation. Exact on-screen meaning is the localized “50% Radius”; runtime validation should confirm the presentation and the model's statistical interpretation. |
| Wake-torpedo preview | `AttackingState.UpdateTargetPreview`, `BearingLaunchPrediction.TorpedoLaunchProfile` / `TrySolveWakeTarget`. | The preview uses the target's actual transform, velocity and forward direction. The `High/Moderate/Low` “Hit Chance” bucket thresholds a solution safety-offset margin, so it is not a probability estimate. |

## Track provenance and side effects

**Verified information-boundary issue:** the hovered UI item is a `SeapowerUI.MapUnitViewModel`, but `UpdateDisplayedRange` passes `mapUnitViewModel.Unit.BaseObject` to range, no-escape, hit-chance, deviation, horizon and preview calculations. The inspected path does not use `Vehicle.PositionEstimate`, `DetectedPosition` or `ExtrapolatedPosition` as the calculation input. Those calculations read the underlying object's transform/velocity, air/weapon/type state, altitude, sensor/ECM/emission/chaff state, durability and flee performance. Classification, identification and error ellipse are used as presentation gates; they do not make the calculations track-only. The primary hover numbers are therefore privileged target-state calculations even when shown beside a contact track. The chance line is hidden unless the contact is classified and identified, but range/no-escape/deviation calculations are still performed from the underlying object.

Static inspection found no fire/order dispatch, ammunition decrement, weapon reservation or simulation-target assignment in the hover handler; it changes view-model state, target preview and map annotations. The direct range and generic chance paths inspected showed no call to the global simulation RNG. The gun predictor has the local seeded sampling and mutable static cache noted above. The direct CIWS predictor and all transitive getters were not audited deeply enough to certify full purity or zero cache/RNG effects.

This is a Unity/Noesis UI path: mouse handling, property notifications, transforms and potential physics raycasts are accessed from `AttackingState`/view-model callbacks. Treat it as game-thread-dependent; this static pass does not establish a safe external worker/query boundary. Hover refreshes on target changes and at roughly 0.2-second intervals, with a roughly 1-second state update. The gun prediction can sample many shell trajectories; actual main-thread cost and hover smoothness are unknown because no runtime profiling was done.

## Evidence status

- **Verified statically:** managed call path, localized labels found in the installed English file, field formatting/conditions in the view-model methods, direct native calculation calls, and the use of `Unit.BaseObject` as target input.
- **Interpretation, not a runtime guarantee:** what the game designers intend `Effectiveness`, `50% Radius` and the colored range ratios to promise. The method implementations support the limited meanings stated above, but do not establish calibration against observed combat outcomes.
- **Unverified:** final rendered UI/resource bindings, live values, latency/thread behavior under play, and any safe observer-track-only way to obtain equivalent values.

## Remaining runtime questions

1. Confirm the exact final Noesis/XAML placement, visibility, colors and live formatting for the hover text and map range rings; establish whether the `Min radar horizon` localization is used elsewhere in this UI.
2. Reproduce one identified compatible track and one uncertain/unidentified track. Compare displayed values with the actual feeding objects and with observer-visible estimates; verify the information-boundary behavior and whether it changes by mode/options.
3. Validate numeric behavior at different ranges and target altitudes, especially the 3D-versus-horizontal denominators, target-specific FCR horizon label, and zero/unsupported no-escape cases.
4. Verify `HitChanceDisplay` modes and the two unrelated “Hit Chance” presentations (numeric/rough generic result versus wake-torpedo bucket); test the scoring against representative weapon/target cases before making any probabilistic claim.
5. Measure hover latency and check cache/RNG effects around gun and CIWS predictions; confirm required game-thread and object lifetime constraints with the supported runtime harness.
6. Confirm the actual launcher aggregation, available-ammunition behavior and target compatibility cues in a live order flow. Static code shows ammunition-level selection and aggregated compatible weapon systems, not a unique launcher choice.

No runtime questions above were answered by this static pass. Gates C and D remain deferred for review and a later assignment.
