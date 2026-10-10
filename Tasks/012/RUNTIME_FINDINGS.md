# Task 012 — bounded runtime findings

**Status:** Partial. Two native diagnostic samples were collected without firing. The native hover was not reproduced onscreen, and a weapon-state fingerprint changed without field-level attribution. Gate D remains deferred; Task 012 is open.

## Conditions and method

- Sea Power **0.8.3 Build 261001 Public Release**, Unity **6000.0.67f1**. `SeaPower_Data/Managed/Seapower-Scripts.dll` SHA-256: `0FE6AAF2A6D9982E7E2EB41FD6035CB97C149773202C90821D71C00A9FDEB01A`; assembly version `0.0.0.0`.
- Used `Experiments/Task002/Start-Validation.ps1` with the existing opt-in harness and `Docs/Tasks/012/Fixture/AircraftEngagementHold.ini`, side `Taskforce1`. The mission starts 1985-06-26 10:00 with one player F-14A and one opposing aircraft; both have `WeaponStatus=Hold`, no attack task or trigger is assigned, and no weapon was fired.
- Selected `usn_aim-7m` (AIM-7M) as the diagnostic ammunition from the F-14A AirToAir loadout. `getAmmunitionAmountByName` reported four rounds, unchanged after each sample. The runtime weapon system was a `WeaponSystemHardpoint`. The probe did not click a `WeaponEntry` or start `AttackingState`, so this is a native calculation reproduction for an available munition, not a completed UI order selection.
- The player plotting table contained two tracks. The target track was an aircraft, classified and identified in both samples. Its error ellipse was absent in sample 1 and present in sample 2; the ellipse size was not captured, so the native 10%-of-kinematic-range display gate cannot be evaluated for sample 2. This was not an unidentified-contact comparison.
- The observer-only probe ran on `Globals.mainThread` and selected the target through the player's plotting table. It then passed the target's `Unit.BaseObject` to the native methods, as the statically inspected hover path does. This reproduces derived values from the same privileged target object used by the UI; no raw coordinates or hidden properties are included here.
- The probe did not invoke `AttackingState.UpdateDisplayedRange` or `MapUnitViewModel.UpdateContactMouseoverInfo`, and no screenshot/GUI hover was captured. Earlier window captures timed out repeatedly and exposed only a root pane, so a usable game viewport was unavailable. The presentation column below describes the statically mapped UI correspondence, not a verified onscreen observation.
- The game process has exited and the harness restored the game-root `doorstop_config.ini` to `enabled = false`. Removal of the temporary `BepInEx/plugins/Task012EngagementProbe.dll` was rejected by the execution policy; the file remains in the install and must be removed before another launch. No vendor assembly was changed.

## Measurements

Native distance values were converted from Unity distance units using `0.03628513216972351 NM/unit`. The times are mission elapsed seconds from the 10:00 start. `Single.MaxValue` is treated as an unbounded/no-finite-limit sentinel.

| Field / native type | 10:00:03.478 (sample 1) | 10:00:18.481 (sample 2) | UI correspondence and verification |
|---|---:|---:|---|
| Current target distance (`Single`, Nmi) | 6.8959 horizontal / 6.8959 3D | 3.5049 horizontal / 3.5050 3D | Hover comparison uses current distance in the range ratios. The native hover text was not observed. |
| Minimum launch range (`Single`, Nmi) | 0.8000 | 0.8000 | Static map label: `${WeaponName} Min range`; sourced from `_launchRangesInUnity.x`. Not a fire-permission result. |
| Target-specific maximum (`Single`, Nmi) | 11.9763 | 12.0858 | `min(KinematicRange, SensorRange, TerrainRange)`; kinematic range was limiting in both samples. Static map label: `${WeaponName} Max range`; one decimal, so 12.0 and 12.1 Nmi. |
| Kinematic limit (`Single`, Nmi) | 11.9763 | 12.0858 | Static hover label `Kinematic Range:` uses a limit/current ratio when the uncertainty gate allows the range group. |
| Sensor limit (`Single`, Nmi) | 28.5727 | 37.8976 | Static hover label `Sensor Range:` uses a limit/current ratio. `EngagementIssues` was empty in both results. |
| LOS/terrain limit (`Single`, Nmi) | `Single.MaxValue` | `Single.MaxValue` | No finite terrain/LOS cap was returned. Static hover label is `LOS Range:`; final formatting or suppression was not verified. |
| No-escape range (`Single`, Nmi) | 6.3939 | 6.2183 | Static map label `${WeaponName} No-escape range` and hover label `No-Escape Range:`. Probe used `NoEscapeRangePrecise` with the returned kinematic range as cap and 16 iterations; exact UI arguments were not captured. |
| Time of flight (`Single`, seconds) | 16.9447 | 9.8396 | Static hover label `Time Of Flight:`; shown with the range group when its track-uncertainty gate passes. |
| Hit score (`Single`, unitless) | 0.6568 | 0.6568 | `CalculateHitChance` returned the same raw factor. Static code scales it to the UI percent presentation: approximately 65.68, which would round to `Hit Chance: 66%` in `Full`, or `Effectiveness: Moderate` in `Rough`; `Off` suppresses that line. These are mapped outputs, not observed GUI text, and the cosmetic option value was not read. The score is not established as calibrated P(hit). |
| 50% Radius (`Single`, feet) | NaN / unavailable | NaN / unavailable | `CalculateExpectedDeviation` returned NaN; static UI shows `50% Radius:` only for a positive result, so this field would be hidden. |
| Engagement issues (enum list) | none returned | none returned | No issue lines are expected from this result. The hover call did not run, so no final rendered issue text was observed. |

The same platform, ammunition and target were sampled at two different separations. As distance fell by 3.3910 Nmi, kinematic range rose by 0.1095 Nmi, sensor range rose by 9.3249 Nmi, time of flight fell by 7.1051 seconds, and no-escape range fell by 0.1756 Nmi. The raw hit score stayed at 0.6568. These are observed changes in the moving scenario; this two-point sample does not isolate which individual target-geometry inputs caused each change.

The earlier temporary trace labeled the no-escape value as latitude and multiplied it by 60, producing an invalid conversion. The values in this report retain the native results (176.2120 and 171.3735 Unity units) and apply the distance conversion above. The diagnostic source is corrected; the previously built DLL that produced the temporary trace was not rebuilt.

## Native methods and side effects

The probe called these installed-build methods directly on the game thread:

- `SeaPower.ObjectBase.GetMaxEngagementRanges(ObjectBase targetObject, Ammunition ammo, bool requireOpenSensorChannel, bool shortCircuitTerrainCheck, bool usePreciseKinematics, Nullable<Vector3> bearingFirePos)` with `false, false, true, null`.
- `SeaPower.AmmunitionParameters.CalculateHitChance(ObjectBase shooter, ObjectBase target)`.
- `SeaPower.AmmunitionParameters.NoEscapeRangePrecise(ObjectBase launcher, ObjectBase targetObject, float maxSearchRange, int iterations)` with the returned kinematic range cap and 16 iterations.
- `SeaPower.AmmunitionParameters.CalculateExpectedDeviation(ObjectBase shooter, Vector3 targetPos, ObjectBase target)`.

The platform did not expose a concrete `Ammunition` object through the inspected hardpoint graph. The probe wrapped the exact `usn_aim-7m` parameters found in `AmmunitionParameters._sharedByType` using `Ammunition(AmmunitionParameters)`. This was sufficient to call the native methods, but equivalence to the instance selected by a live `WeaponEntry` remains to be confirmed.

| Check | Observed result | Limit |
|---|---|---|
| Ammunition and task count | Four AIM-7M rounds before/after; task-view-model count stayed 1/1 | No ammunition decrement or task-count change detected. |
| Simulation tick | Delta 0 during each measurement block | Does not prove every native field was unchanged between frames. |
| Simulation RNG | Probe fingerprints for `Globals._rnd` and Unity random state were unchanged | Fingerprints are diagnostic checks, not a formal purity proof. Static inspection found no global RNG call in the direct range and hit-score paths; the no-escape transitive path was not exhaustively audited. |
| Captured static caches | Probe cache fingerprint unchanged | Only the selected known cache fields were covered. |
| Weapon-system state | Fingerprint changed in both before/after blocks | The probe recorded only a hash of selected `WeaponSystemHardpoint` fields, around all calls together. It did not retain per-field/per-call deltas; this cannot establish whether a native method changed weapon/target reservation state or an ordinary update changed a field. Read-only status for those fields remains unresolved. |
| Main-thread cost | Sample 1: range 14.548 ms, hit 1.264 ms, no-escape 4.018 ms, deviation 0.487 ms. Sample 2: 0.894 ms, 0.017 ms, 1.971 ms, 0.004 ms, respectively. | Timings include reflected invocation and first-use/JIT effects; two samples do not establish steady-state cost. `GetMaxEngagementRanges` may use Unity physics, and the native hover path is a game-thread/UI path. No worker-thread call was attempted. |

## Remaining runtime questions and gate status

1. Reproduce the actual weapon-order hover and record the live range labels, hover text, colors, option mode and target preview. Compare the rendered values against the diagnostic inputs.
2. Capture a less certain or unidentified contact with its observable ellipse size; establish whether the numeric and range groups are hidden or adjusted at the native gates.
3. Use per-call, field-level snapshots plus an idle baseline to attribute the weapon-state fingerprint change and verify target assignment, reservations, cache and RNG behavior.
4. Confirm the exact no-escape cap/iteration arguments and the concrete ammunition instance used by `AttackingState.UpdateDisplayedRange`.
5. Gate D (backend-independent API feasibility/design) remains for a subsequent assignment after these findings are reviewed.

| Gate | Result | Evidence / gap |
|---|---|---|
| A — native hover path | Partial | Managed path and labels are in [STATIC_FINDINGS.md](STATIC_FINDINGS.md); no on-screen hover observation. |
| B — calculation semantics and safety | Partial | Native missile results and limited side-effect checks recorded here; weapon-state mutation is unattributed and broader purity is unproven. |
| C — bounded runtime validation | Partial | Two geometry samples from one no-fire scenario; no actual GUI capture and no unidentified-contact case. |
| D — API feasibility/design | Deferred | Not started; remains the next assignment after review. |
