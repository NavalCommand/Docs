# Task 019 — Interactive Sea Power Identification Experiment

**Date:** 2026-10-14
**Status:** Interactive run and owner-feedback analysis complete; observer-ECS and UI-source gaps remain.
**Scope:** Read-only runtime investigation. No production API, protobuf schema, codec, adapter, or TacticalDisplay source was changed.

## Executive findings

1. Contact `7001` first entered Taskforce1's plotting table without a class, identification flag, or detected side. At the owner's first reported visible cue, it still had no class or side; its position estimate was present. The owner saw a small visual cue and a submerged track, but no rendered 3D model.
2. At mission time `1385.0` (scenario clock `10:23:05`), the contact changed to `wp_ss_kilo`, `Identified=true`, side `Enemy`, relation `Hostile`, a few seconds before the owner reported the Paltus model and hostile alignment. The native `Vehicle` wrapper exposed an exact class key, not an observed dimension/category/subcategory ladder.
3. At the reported deep-evasion/fade, the exact class and hostile relation remained on the contact while `HasLiveTrack` was false and `PositionEstimate` remained present. The recorder did not see `HasLiveTrack` return until `10:30:42`, about 82 seconds after the reported `10:29:20` sound-sensed reappearance. The available records cannot determine whether the discrepancy is a timing difference or whether the UI's sound-sensed label differs from `HasLiveTrack`.
4. The recorder saw seven separate `usn_ssq-53` rows and a separate `usn_mk46_mod5_air` row in the selected observer's plotting table. The MK-46 row disappeared at `10:33:39.6`, within a second of the reported torpedo impact. These are actual observer-table entries; the recorder did not inspect object links or infer them from a simulator-wide entity list.
5. The target's contact row persisted with exact class, hostile relation, an estimate, and a live track after the owner reported the Kilo dead at `10:35:10`. It remained in the table through the run end at `10:36:51.7`. Contact presence and `HasLiveTrack` do not establish that a target is alive.
6. The first-level UI cue cannot yet be safely converted into a Domain classification. The recorder's ECS reflection path was unavailable, its sampled contact-info lines were null, and some view-model values are presentation properties whose source can use the underlying `BaseObject`. This experiment is informative but **not sufficient for a safe full observer-classification integration**.

The Domain hierarchy remains `Dimension → Category → SubCategory → TypeId`, represented for an observation by `Resolution<TypeId, SubCategory, Category, Dimension>`. Sea Power's simpler contact wrapper is a source limitation, not a reason to reduce the domain model.

## A. Experimental setup

- **Scenario:** [MinimalCombat.ini](../../../SeaPower.Mod/managed/harness/scenarios/MinimalCombat.ini), unchanged. The scenario starts at `10:00` on 1985-06-26 with a Taskforce1 `usn_ffg_oliver_hazard_perry` and a Taskforce2 `wp_ss_kilo` approximately 50 NM apart. The owner's manual notes describe operating a Sh-2F helicopter from Taskforce1.
- **Domain baseline:** `Domain.Api` was clean on `feature/classification` at `57dee1e`. No Domain production files were changed.
- **Observer:** `Taskforce1`; the recorder sampled only `Globals._playerTaskforce.PlottingTable.Vehicles`.
- **Game:** Sea Power 0.8.3 Build 261001 Public Release; Unity 6000.0.67f1; BepInEx 5.4.23.2. `Seapower-Scripts.dll` SHA-256: `0FE6AAF2A6D9982E7E2EB41FD6035CB97C149773202C90821D71C00A9FDEB01A`.
- **Instrumentation:** Task-scoped `Task019ClassificationRecorder`, built Release / `netstandard2.1`. It sampled the selected observer table on the game thread at a 0.25-second real-time interval, wrote lifecycle changes and periodic snapshots through a bounded queue, and recorded `Class`, `Identified`, side/relation, `HasLiveTrack`, estimate presence, and selected view-model properties. It did not read `Vehicle.Object`, `BaseObject`, hidden target configuration, or global object lists. Source is retained in [Fixture](Fixture/).
- **Run:** Existing `Start-Live.ps1`, Harness mode, no production runtime, one temporary fixture. Recording was active from mission time 0 through `2211.717` seconds (`10:36:51.7` scenario time). The owner controlled the game after readiness and exited it normally.
- **Build/run notes:** The task fixture built successfully with two `MSB3277` reference-version warnings (`System.IO.Compression` and `System.Net.Http`). BepInEx logged its known Unity log-writer startup error, but loaded the harness and Task019 recorder, which wrote through normal exit. `Doorstop` was restored to SHA-256 `3B5FBCF2C104F967F63687A0FD240CB79A5253D08F4338A358EE796C17B5AA0A`; cleanup recorded game exit code 0. No owned Sea Power process remained.
- **Evidence:** Raw recorder output and non-proprietary launch/build/cleanup receipts are in [Task 019 evidence](../../Evidence/Task019/README.md). No screenshot was captured; screen details below are the owner's written observations.

The scenario's configured start time is `10:00`, and the impact, surfacing, and death observations align with `missionSeconds` to within about one second. I therefore interpret the owner's `10:xx` timestamps as the in-scenario clock. The first identification and sound-reacquisition points differ by a few seconds or more; those differences are retained rather than rounded away.

### Instrumentation gaps

- All 597 contact creation/update/snapshot records reported `observerEcs=reflection-unavailable`; the removal event contained no ECS sample. The fixture collapses four initialization prerequisites into that marker (`Vehicle.Entity`, `VehicleComponent`, and the ECS `GetComponentData` and `HasComponent` methods); it did not log which prerequisite was missing. No live `ClassInformation` candidate bits, `DetectedSide` component payload, or other observer ECS classification state was captured.
- For contact `7001`, all 67 recorded rows had `ContactInfoLine1`–`ContactInfoLine4` null. This does not establish that the player-facing lines were blank; it means this probe did not capture their displayed text or establish why those properties were null.
- The probe recorded `PositionEstimate.HasValue`, not estimated coordinates, uncertainty geometry, depth, or sensor contributors. Manual values such as `12 ft` and `12 nm` are therefore not corroborated by native position data.

## B. Owner-observed identification stages

The following is a faithful summary of the owner's notes; it is not a transcription from the recorder:

| Owner clock | Player-visible information reported |
|---|---|
| `10:22:48` | First visible contact, ID `7001`; `0 kts`, `000 deg`, depth `12 ft`; no 3D model; alignment unknown; track info “Visual: semi small (V)”; track displayed as submerged; about `12 nm` from the helo. |
| `10:23:08` | Model rendered as “Prj.877 Paltus”; alignment Hostile; submerged track. |
| `10:29:20` | After the helo dropped a torpedo, the Kilo evaded by going deep. The track faded but remained displayed. At the same timestamp the owner reports it reappeared as a sound-sensed track with type known as Kilo. |
| `10:33:40` | Torpedo hit the Kilo. |
| `10:35:00` | Kilo surfaced and the owner reports “V+S tack info.” The phrase is retained as written; it may mean “V+S track info,” but that correction is not confirmed. |
| `10:35:10` | Kilo died. |

## C. Native classification timeline

`missionSeconds` is relative to the scenario's `10:00` start. In the native state column, “estimate” means only that `PositionEstimate.HasValue` was true; no position value was logged.

| Mission time | Contact ID | Native state | Player-observed classification | Interpretation |
|---|---:|---|---|---|
| `10:22:21.5` | 7001 | Created with `Identified=false`, no `Class`, no side, no live track, and no estimate. The recorder's view-model values already said `VisualType=Submarine`, `VisualPosture=Enemy`. | Owner did not report seeing the contact until `10:22:48`. | Native table membership preceded the owner's first visible cue by about 26.5 seconds. View-model values are presentation data, not validated observer classifications; see Section D. |
| `10:22:26.6`–`10:22:48.4` | 7001 | Estimate became present at `10:22:26.6`; `HasLiveTrack` toggled several times. At the nearest update to the owner's `10:22:48` observation, `Identified=false`, class absent, side unknown, estimate present, and `HasLiveTrack=false`; it was true again 0.3 seconds later. | “Visual: semi small (V),” submerged track, no rendered 3D model; alignment unknown. | A visible sensor/UI cue existed while the wrapper had no exact class or side. The cue does not establish a Domain dimension, category, or subcategory. The record does not explain the brief live-track changes. |
| `10:23:05.0` | 7001 | Transition to `Identified=true`, `Class=wp_ss_kilo`, `Side=Enemy`, relation `Hostile`, live track and estimate present. | At `10:23:08`, model rendered as “Prj.877 Paltus”; Hostile; submerged track. | Exact native class and affiliation were both present about three seconds before the reported UI change. This run does not establish whether they share a cause; they remain separate facts. The model-render event itself was not sampled. |
| `10:29:16.1`–`10:30:36.0` | 7001 | `Class=wp_ss_kilo`, `Identified=true`, side Enemy and relation Hostile persisted. `HasLiveTrack=false`; estimate stayed present. The last sampled false state was at `10:30:36.0`. | At `10:29:20`, owner reports deep evasion, a faded-but-retained track, then a sound-sensed Kilo. | The recorded fade is consistent with loss of a live track while retaining an estimated contact. The recorder did **not** observe live-track restoration at the reported sound-sensed time, and it did not capture sound/visual sensor provenance. |
| `10:30:42.2` | 7001 | `HasLiveTrack=true` again; exact class, side, relation, and estimate unchanged. | No separate later UI change reported. | This is the first recorded live-track restoration after the `10:29:16.1` loss, 82.2 seconds after the owner's reported reappearance. A flag/UI-semantic difference is possible but unproven. |
| `10:29:16.1`–`10:29:18.7` | 1009 | A separate table entry was created, then reported `Class=usn_mk46_mod5_air`, side Player, relation Friendly, live track and estimate. | Owner reports dropping a torpedo at `10:29:20`. | A friendly weapon-like contact appeared at the same time. Timing and class are consistent with the reported torpedo, but the recorder did not inspect object links, so it cannot prove this row is that weapon. |
| `10:33:39.6` | 1009 | Contact 1009 was removed from the sampled table. | Owner reports the torpedo hit at `10:33:40`. | Removal coincides with impact to within 0.4 seconds. The Kilo row was present in snapshots immediately before and after this event. No direct weapon-to-target link was recorded. |
| `10:35:00.1` and `10:35:10.3` | 7001 | Exact Kilo class, Enemy side, Hostile relation, live track and estimate remained. No corresponding wrapper transition was recorded. | Owner reports surfacing and “V+S tack info,” then death ten seconds later. | The reported sensor/presentation change and death were not represented by the sampled class/side/live/estimate fields. |
| `10:36:51.7` | 7001 | Still in the selected plotting table with exact Kilo class, Enemy side, Hostile relation, live track and estimate. No removal event for 7001 occurred. | Owner had reported the Kilo dead at `10:35:10`. | Contact-table persistence continued for at least 101.7 seconds after the reported death. Contact presence and live-track state are not a liveness field. |

### Other independently exposed contacts

The selected Taskforce1 table also contained a friendly `usn_sh-2f` row (ID `1002`), seven separate `usn_ssq-53` rows (IDs `1003`–`1008` and `1010`), and the separate `usn_mk46_mod5_air` row (ID `1009`). Each class became available on an actual row in the selected observer table. The buoy and weapon IDs are not synthesized from the Kilo's hidden configuration. The owner's notes do not describe whether these rows were visually noticed, so the result establishes table membership, not a particular screen rendering or who deployed each object.

## D. Native source analysis

The source-level findings below reuse the verified static analysis in [Task 018](../018/REPORT.md) and the UI paths inspected for that build. The runtime confirmed the wrapper transitions but could not inspect the observer-owned ECS classification payload.

### Exact wrapper class and identification flag

In the inspected build, `Vehicle.TransferFromComponents` reads observer-side `ClassInformation`. In the ordinary candidate path, it sets `Vehicle.Class` only when one class bit remains, resolving it to an INI class key; zero or multiple remaining bits leave the wrapper class absent. The wrapper does not expose those candidate bits as a broader category or subcategory. `Vehicle.Identified` follows whether `Class` has a value, so it is not an independent confidence scale. Task 019 reproduced the absent-to-exact pattern for contact 7001: no class and `Identified=false`, then `wp_ss_kilo` and `Identified=true`.

There are separate override and weapon paths that can fill `Class` from `Object.getObjectIniName()` rather than from a single observer candidate. That is a known provenance risk from Task 018. The ECS failure in Task 019 means we cannot identify the branch or candidate set for this particular runtime transition. Do not treat every non-empty class on every object family as observer-safe without validating these paths.

### Alignment and sensor presentation

Detected side and relation are separate from class in the contact model. In this run both were unknown before exact class and changed to Enemy/Hostile with the exact key. Task 018 also observed a known relationship with an empty class on a different contact. This supports retaining alignment independently; simultaneity in one transition does not make it a subtype or prove a dependency.

When the UI's class-info layer is visible, the inspected `MapUnitViewModel` sets an unidentified contact's class-info line through `Unit.IncomingSignalInfo()`; after identification it uses a class/name path. This is the likely UI route for the owner's “Visual: semi small (V)” cue, but Task 019 did not capture the line text, layer visibility, or the implementation inputs to `IncomingSignalInfo()`. The sound-sensed label and the owner's “V+S” note likewise have no matching recorded sensor field in this run.

The recorder's `VisualType=Submarine` was present before `Vehicle.Class` and `Side` were known; `VisualPosture=Enemy` was also present while the wrapper relationship was unknown. The inspected view-model's visual-type path maps `ObjectBase.ObjectType` into a map type, and its contact-display logic also reads `unit.BaseObject`. These are UI presentation values, not a validated observer candidate set. In particular, `VisualType=Submarine` must not be translated directly into known Domain classification, and `VisualPosture=Enemy` must not replace detected side or relationship. The manual “submerged track” and model visibility were not independently recorded as native wrapper properties.

### Track retention and object membership

`HasLiveTrack` and `PositionEstimate.HasValue` changed independently of table membership. Contact 7001 could have no live track while an estimate and its previously learned exact class/relation remained. The contact also persisted past the owner's death observation. Treat live/fading contact status, estimated position, class, relation, and object existence as distinct observations. This run has no target-destruction field and does not say when Sea Power should remove a stale contact.

The separate sonobuoy and MK-46 rows corroborate that independently simulated objects can surface through the legitimate observer table. Their semantic NavalCommand types should come from curated mappings of observer-safe class keys, not from a weapon's implementation class or an inferred association to the Kilo. This evidence does not test MOSS, mines, noisemakers, or other decoys.

## Why Task 018 did not show this progression

Task 018's matched surface scenario placed a Perry and Krivak about 0.5 NM apart with the Perry radar active; its short captures sampled the foreign contact only after the exact key was already available. The air capture was also bounded to 12 seconds and was not designed around a long, manually operated submarine-identification sequence. Task 019 instead ran the unchanged 50-NM Perry/Kilo scenario for over 36 minutes of mission time while the owner operated the helicopter and observed the Tactical Display; separate sonobuoy-class and torpedo-class rows appeared in the table. The longer and different sensor/contact history exposed an unknown-class period, a live-track fade, and UI information not captured by the short wrapper-only samples. Task 018 therefore did not falsify the progression reported here; it sampled different conditions and did not record these UI stages.

## E. NavalCommand mapping recommendations

- Keep `Resolution<TypeId, SubCategory, Category, Dimension>` intact. This run exposes an exact key or no key through the ordinary wrapper; it does not make the domain hierarchy redundant.
- Create observations only for entries in the selected faction's legitimate contact system. This run supplies actual table rows for the Kilo, SH-2F, sonobuoys, and MK-46. Do not synthesize a contact from a known scenario unit or a `BaseObject` reference.
- When an exact, non-empty class key has verified observer provenance and an approved catalog mapping, resolve it to a curated `TypeId` and its domain subcategory; derive category and dimension from the Domain model. `wp_ss_kilo` can map to the curated Kilo/Paltus platform identity after its ordinary observer-classification path is validated. For absent, empty, ambiguous, or unsafe class values, leave resolution unknown unless a verified observer-owned source supports a less precise level.
- Do not populate Dimension or Category from `VisualType`, `VisualPosture`, “Submerged track,” “Visual: semi small (V),” or the sound-sensed label until their native source and observer provenance are demonstrated. Keep sensor modality and track posture as separate sourced properties if the API supports them.
- Represent `DetectedSide`/relationship independently of `Resolution`. The run's Unknown → Enemy/Hostile transition and Task 018's empty-class/known-relation example both oppose encoding affiliation as a subtype.
- Keep estimate presence/freshness separate from `HasLiveTrack`; neither indicates target survival. A contact row retained after death remains an observer contact, but this probe supplies no authoritative destruction or liveness fact.
- Map `usn_ssq-53` to the semantic sonobuoy and `usn_mk46_mod5_air` to a curated torpedo identity where those exact observer-visible keys are verified. Do not copy their native implementation families as the semantic category and do not assume contact rows have been visually noticed just because they exist in the plotting table.

## F. Unresolved questions and smallest follow-up

1. **Which observer classification candidates produce the visual cue and the exact Kilo class?** The live ECS read failed before it reported which reflected prerequisite was missing. Before integration, run a short instrumented repeat with per-prerequisite reflection diagnostics and a read-only observer-state capture of `ClassInformation`, its candidate bits/flags, and `DetectedSide` at the relevant contact state. Confirm the ECS access path on the game thread; do not log target objects or hidden configurations.
2. **What native values drive the labels “Visual: semi small (V),” “sound-sensed,” and “V+S”?** The recorder's contact-info properties were null. Inspect `IncomingSignalInfo()` and the display path for `MapContactVisuals.Resolve`, then make a brief repeat with contact selection/info-layer state and the resulting line text recorded alongside observer-side sensor/classification state. The existing observations do not justify mapping those labels to Domain classification levels.
3. **Why does the reported sound reappearance precede `HasLiveTrack=true` by about 82 seconds?** The native table retained the class and estimate throughout the interval. A repeat should record the display line and live-track transitions on the same mission clock; until then the report leaves the timing/semantic difference unresolved.
4. **What governs contact expiry after destruction?** The Kilo row remained through the run end after the reported death. A later bounded run can continue the same legitimate table sampling through the normal stale-track expiry period, without reading hidden target liveness. This is needed to distinguish contact-retention policy from object survival.
5. **Is `VisualType=Submarine` intentionally visible before exact identification?** It appears in the view model from contact creation, but its mapping path consumes the native `ObjectBase` type. Inspecting the map-symbol render path and comparing the owner's actual first-contact screen is necessary before treating it as legitimate observer knowledge.

## G. Integration readiness

**Not sufficient for full safe implementation of observer classification in `SeaPower.Client`.** The run validates an unknown-to-exact wrapper transition and separate affiliation, and confirms actual observer-table rows for separately exposed equipment. It does not reveal the candidate bits or establish the provenance of every class path, the displayed pre-identification signal labels, vertical-track presentation, the sound reacquisition transition, or contact expiry. The report supports a narrow design rule—use only observer-table entries and map only validated, non-empty observer-safe keys—but a follow-up read-only observer-state/UI experiment is still needed before implementing the integration end to end.

No production mappings or code were implemented. No production repository was committed or pushed.
