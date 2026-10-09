# Task 002: Sea Power runtime findings

2026-10-09. **Scoped automated launch and protected aircraft command rejection are demonstrated. Hook safety, faction observation, AI durability and pause ownership have runtime evidence with explicit limits.** No production API/client architecture was implemented.

The user approved Anchor Chain/BepInEx installation and cleanup. Steam connectivity is intentionally disabled in this development package. Its export exception remains, but missions succeed; no platform checks/binaries were changed. This supersedes the initial approval-blocked checkpoint.

## Build and integrity

Installation: C:\Games\Sea_Power_8_3. Runtime: **0.8.3-261001**, Unity **6000.0.67f1**, development build false. Seapower-Scripts.dll: 8,647,680 bytes, assembly version 0.0.0.0; SHA-256 **0FE6AAF2A6D9982E7E2EB41FD6035CB97C149773202C90821D71C00A9FDEB01A**, matching Task 001. **B** below means this exact build/hash.

[Baseline](../../Evidence/Task002/binary-baseline.json) and [final comparison](../../Evidence/Task002/binary-verification.json) cover **233** vendor executable/UnityPlayer/Managed files; zero changes. BepInEx logs in-memory UnityEngine.CoreModule chainloader patching, without disk alteration. No proprietary DLL/decompiled source is in deliverables. Native startup/loaders write logs/cache/content metadata; the binary comparison does not certify every profile file unchanged.

## Gates and milestones

| Gate / milestone | Status | Precise scope and limit |
|---|---|---|
| A / 0 launch | **Passed for fixtures/native player side** | Consecutive fresh runs 25.74/26.03 s, refined readiness 25.26 s, final normal run 26.49 s; no UI input. General briefing dismissal/arbitrary side swapping unverified |
| B / 1 safety/lifecycle | **Partially tested** | Callbacks, checked job-hook samples and scene reload on thread 1; plain-data worker; old references/lease cleared. Global ECS coherence and clean plugin unload not certified; BepInEx log writer incompatible |
| C / 2 visibility | **Partially tested; basic whitelist demonstrated** | Hidden far opponent absent; near surfaced opponent yields estimates on both tables. Separate fixtures, not same-session detection transition; sharing/dormant/reacquisition unresolved |
| D / 3 AI durability | **Partially tested** | Nonplayer submarine retains waypoint and moves in native Sprint state. Strategic AIPlayer forced-disable flag true; active strategic conflict and same-unit emergency defense not proven |
| E / 4 evasion protection | **Passed for single-aircraft waypoint guard; milestone partially tested** | Native evasion activates; command rejected without finishing state. Separate CeaseFire ends evasion immediately. Aircraft later destroyed; route resumption and formation/family coverage unverified |
| F / 5 pause | **Partially tested** | Paused insertion with unchanged tick; owned release, synthetic repeated competing native Pause refuses release, existing pause refused; lease invalidated on unload. Actual menu/time-setter intent surface incomplete |
| 6 lifecycle/incidents | **Partially tested** | Native unload/reload; entry/exit incidents persist beyond polling. No independent two-cursor incident service |

## Test/evidence matrix

Evidence filenames below are under Docs/Evidence/Task002. [Reproduction](TASK002_REPRODUCTION.md) specifies wrapper commands/switches; [launch report](AUTOMATED_LAUNCH.md) specifies executable/environment/readiness.

| Question | Build/hash | Setup & exact steps | Observed result | Evidence | Confidence | Remaining risk |
|---|---|---|---|---|---|---|
| Native execution? | B | Fresh hidden no-argument Start-Process baseline; later normal ProcessStartInfo launches | GPU/Unity initialize; later combat scenes advance time | native-launch.json; launch-3/4-result.json | Verified live | Intentional Steam diagnostic remains |
| One-command scenario start? | B | Start-Validation, MinimalCombat.ini, Taskforce1, fresh output, CloseAfterReady; consecutive runs | 25.7439586 and 26.0266696 s, selected unit/time advancement, PID/token match | launch-3/4-probe.log and result.json; gate-a-checkpoint.json; final-readiness-result.json | Verified scoped success | Briefings/arbitrary side changes unverified |
| Loader loading? | B | Audited release, source-built AC in user content, direct BepInEx probe | Awake/Unity thread 1, AnchorChain V1.1.0 loads | loader-noop-probe.log; launch-4-bepinex.log | Verified tested loading | UnityLogWriter fails; no blanket certification |
| Narrow safe callback? | B | Harmony postfix CompleteFixedUpdateJobs; require both object/collision completed flags | Repeated thread-1 samples, tick/time, flags true | diagnostics-1-probe.log | Verified cached managed slice | Not all ECS/sensor jobs fenced; native Complete may return early |
| Hidden opponent absent? | B | MinimalCombat, 50 NM/submerged opponent, sample both selected tables | foreignTracks=0 | diagnostics-1-probe.log | Verified fixture | Sharing/detection universe not exhaustive |
| Detected estimates? | B | NearContact, surfaced opponent 0.5 NM, radar active, RunProbes ObserveOnly | Both factions emit estimates; final run adds cache epoch/uncertainty | near-contact-1-probe.log; final-check-1-probe.log | Verified cached fields | Separate before/after fixtures; measurement age/source unresolved |
| Player waypoint execution? | B | ~8 s SetWaypointTask, northward offset, telegraph 2 | Inserted/retained, telegraph 2, increasing displacement | diagnostics-1-probe.log | Verified movement | Target arrival not observed |
| Nonplayer order retention? | B | ~18 s same waypoint on submarine; no AI switches changed | Retained through ~68 s, movement, Loitering→Sprint | diagnostics-1-privileged.log | Verified limited unit control | AIPlayer.IsForcedToDisable=True; no strategic overwrite challenge |
| Guard preserves evasion? | B | AircraftThreat native preset attack; guard in AdvancedMissileEvasion.onEnter | Entry 12.1768 s, rejected, stillEvasion=True/IsFinished=False; exit 19.4145 s then Destroyed | aircraft-guard-1-probe.log | Verified rejection/persistence | No surviving route-resumption proof |
| Cancellation interferes? | B | Separate AircraftThreat + DeliberateInterference; Aircraft.CeaseFire in evasion | IsFinished=True; exit same 12.2144 s timestamp | aircraft-interference-1-probe.log | Verified native cancellation | GUI movement chain statically traced only |
| Paused dispatch without tick? | B | ~65 s pause, Update waypoint, one wall-second observation | Inserted, tick 3889 unchanged, still paused, owned release | diagnostics-1-probe.log | Verified interval | Not every paused native/ECS operation certified |
| Competing pause intent? | B | Owned pause then another same-value native Pause; revision comparison | Revision changes, release refused; existing pause acquire refused | diagnostics-1-probe.log | Verified synthetic path | Actual menu setters may bypass it |
| References/lease on reload? | B | LifecycleTest owns pause then UnloadMissionAndReload | Scene 2 unload/load thread 1, old task removed, references/lease cleared, new samples | lifecycle-1-probe.log | Verified scoped lifecycle | No remote queues; forced process close not plugin unload proof |
| Normal-start deactivation? | B | Default Doorstop false; temporary enable/exact restoration | Readiness and pre/post config hash match | doorstop-restore-check.json; final-check-2-result.json | Verified restoration | Proxy remains until uninstall |
| Vendor integrity? | B | Rehash 233 baseline files after probes | Zero changes | binary-verification.json | Verified contents | Logs/cache/user metadata outside baseline can change |

## Actual methods and threading interpretation

**Startup:** SeaPower.BackgroundSceneController.Start/InitialLoadActions selects Game/menu. Session.OnStart's StandaloneMissionFilePath/config fallback is gated by Standalone mode. The helper waits for initial LoadScreen, sets Globals.currentMissionFilePath and invokes MissionManager.DoLoad, as SeapowerUI.ViewModels.ScenarioMenuViewModel.PlayCommand does. Session.SceneManager_missionLoaded calls GameInitializer.init; DoLoad waits for SceneCreator.WaitForLoadingDone and TerrainManager DEM/chunks. Readiness checks SceneCreator.IsLoadingDone/PlayerSideName, GameMain._initialized, LoadScreen.cameraObject/LoadPercent, controllable own unit/valid cached position and advancing unpaused time. No scenario switch was invented.

**Jobs:** GameMain.PerformFixedUpdate completes jobs, runs native fixed work, then starts jobs; its postfix is not a snapshot fence. JobPerformer.CompleteFixedUpdateJobs calls WaveSample/ObjectCalculations/CollisionAvoidance completion, but the latter schedulers' Complete can return early for unfinished handles. The probe checks both HasPreviousJobCompleted flags before its cached managed slice. It reads no EntityManager/NativeArray/live ECS component. Other sensor/GeoPosition sync jobs are not globally fenced; cache epochs expose older data instead of claiming atomic state.

Awake/Update, scene events and Harmony callbacks access native state only on verified thread 1. Worker logging receives immutable strings and a separate plain WriterState, with no Unity references. Our code performs no game-thread disk/network/inference or synchronous worker join. Overflow becomes explicit evidence failure. A deliberately unwritable log target produced writer-failed/UnauthorizedAccessException and no ready signal; see [fault evidence](../../Evidence/Task002/writer-fault-result.json). Writer failure disables further probe work on Update. If no error marker can be written, the wrapper still cannot pass without readiness. Native loader/engine logging has its own overhead; frame cost was not benchmarked. No-op loading was observed before gameplay probes.

**Observation:** selected Taskforce.PlottingTable.Vehicles is the only contact source. PlottingTable.Ingest assigns own/foreign side-local key ranges; the version-specific experiment filters foreign Id >=7001 internally but exports snapshot-local opaque labels. Vehicle.DetectedPosition supplies nullable GeoPosition and PositionEigpairs.MajorStdDev/MinorStdDev; the tuple's Entity handle is excluded. Vehicle.UpdateEpoch is a cached transfer epoch, not proven measurement age. OBS fields include side/opaque label, nullable estimate, detected latitude/longitude, cache epoch, observed mission time and native uncertainty scalars. Native uncertainty units, source attribution/fusion and allied sharing remain unresolved.

Vehicle.Object/BaseObject, authoritative UID/name, raw Velocity, Position fallback, destruction truth and global registries are not serialized. Privileged nonplayer control/state/displacement is routed separately to privileged.log, excluded from operational/model traces. Snapshot labels are not stable public IDs. Aircraft runs produced multiple foreign tracks with two aircraft; do not assume one track is one unit. Counts later fell, but dormant/reacquisition was not characterized. Hidden/detected comparisons use separate native fixtures, not forced truth insertion.

**Orders/AI:** ObjectBase.SetWaypointTask(WaypointData) creates GoToWaypointTask and inserts it through UserRootNode.AddTask. Tests specify Current height, native Ahead_2_3, small latitude offset, no formation maneuver. They do not replace all routes/disable AI. TaskViewModels membership, telegraph and displacement distinguish acceptance from execution. ReachedWaypoint/RemovedOrFinished events are retained; removal is not a success receipt. GeoPosition.GetDistance is a native coordinate metric, not asserted SI distance. Arrival was unobserved; unload removal was observed.

AIPlayer.InitStaticFields sets IsForcedToDisable=true, matching runtime. Nonplayer submarine accepts AI orders and runs Loitering/Sprint, but this is not ownership against active strategic planning. Taskforce.IsAIControlled exists, yet no assignment was found in its inspected body; do not rely on it alone. No selective suppression was implemented. Next establish active planner/conflicting mission decisions while retaining same-unit navigation/defense.

**Defense:** AircraftStates.AdvancedMissileEvasion.onEnter/onExit, StateMachine.CurrentState/StateChangeTime and Aircraft.CeaseFire are actual members. The dispatcher rechecks typed state immediately before mutation and rejects without replay. Formation propagation is disabled in its waypoint data; this is not a general formation guard. Incidents persist beyond five-second polling. Native speed/altitude changes were recorded. Guarded aircraft was destroyed; route count 1→0 cannot certify resumed execution. Surface/submarine guards remain untested.

**Pause/lifecycle:** GameTime.Pause/UnPause/IsPaused, simulationTick, missionElapsedTime, Unity realtime, MissionManager.UnloadMissionAndReload/DoUnload and scene callbacks were exercised. A Pause prefix counts repeated native calls; the script releases only an unchanged lease revision, refuses existing/competing intent and invalidates lease/references on unload. This is a native-call experiment, not comprehensive player/menu ownership. Alternate time setters remain uninstrumented. Native unload resets time through its own operations. Paused dispatch uses Update without forced FixedUpdate/job completion. No safe production pause-release contract is claimed.

## Modding-method evidence

| Mechanism | Classification/source | Operations established | Installation changes | Build compatibility/test | Rollback |
|---|---|---|---|---|---|
| Mission editor/local content/Workshop | **Official content support**; installed English EA manual pp. 4–5, 8–10; [Triassic announcement](https://www.triassicgames.com/2024/09/16/sea-power-early-access-release/) | Content hierarchy/missions/triggers, not arbitrary managed plugin API | Own external INIs; AC user DLL is community usage | 2024 manual may lag; fixtures loaded on B | Remove own content only |
| Standalone config | **Internal native**, not verified executable CLI; Session.OnStart | Standalone field/config fallback | None | Static only; normal mode Game | None |
| Native scripting/RPC | **Native implementation; external contract unverified**; global ScriptRuntime/VueplexServer/RPCServer | Embedded WebView MessageEmitted/PostMessage RPC, launch.html present | None; no debugger/listener enabled | Static transport traced, not tested as shell endpoint | None |
| BepInEx/Doorstop | **Unofficial community**; [audited release](https://github.com/SeaPower-Modders/AnchorChain/releases/tag/V1.1.0) | Actual callbacks/in-memory Harmony hooks | Manifested proxy/config/BepInEx tree/own DLL | 5.4.23.2 executes; UnityLogWriter fails; own worker works | Disable own config/remove only added manifest paths |
| Anchor Chain | **Unofficial**; [pinned preloader](https://github.com/SeaPower-Modders/AnchorChain/blob/28d225be917533fcc25e19cf8f25ac44010f8abd/AnchorChain.Preloader/Preloader.cs), [pinned loader](https://github.com/SeaPower-Modders/AnchorChain/blob/28d225be917533fcc25e19cf8f25ac44010f8abd/AnchorChain/Main.cs) | FileManager content search/discovery; V1.1.0 startup | Dedicated user DLL, empty ACConfigs directory | Startup verified; custom AC attributed-plugin callbacks untested | Remove dedicated additions; preserve user tree |
| Binary/platform alteration | **Prohibited** | None | None | Not attempted; hashes unchanged | Not applicable |

Package/source hashes, exact additions/rollback are in AUTOMATED_LAUNCH and installed-files.json. Original approval requirement was satisfied by subsequent user authorization; no repeat approval was requested.

## Architecture delta and continuation

No public contracts were changed. Task 001 identity, waypoint execution and CeaseFire cancellation now have runtime support. Helper startup works in tested scope; embedded RPC remains no demonstrated shell endpoint. Fixed-update postfix remains unsuitable as blanket fence. Cached faction estimates work with unresolved age/source/fusion. Native unit state AI operates while strategic AIPlayer is forced disabled, narrowing the earlier ownership hypothesis.

Proposed next architecture revision: mark scoped startup demonstrated; describe field-level cache timestamps rather than presumed atomic snapshots; keep startup/pause/privileged diagnostics separate from operational observations; retain uncertainty/many-to-many track associations. Do not expose production pause release from this prototype.

Risk-ranked follow-up: strategic-AI conflicts/same-unit emergency defense; full running/paused snapshot boundaries; same-session detection/loss/sharing/source/uncertainty units; actual menu/time-control intent interception; surviving route resumption/formations; general briefing startup/independent incident cursors. Synchronous/hybrid/async modes remain plausible, not implemented/certified.

Final state: loader installed but disabled by default, experiments closed, own source/evidence checkpointed. [Final normal launch](../../Evidence/Task002/final-normal-result.json), [cleanup](../../Evidence/Task002/cleanup.json) and [summary](../../Evidence/Task002/result-summary.json) record the final checks. No Git repository/commit or production framework exists.
