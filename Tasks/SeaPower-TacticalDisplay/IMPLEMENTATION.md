# Sea Power integration: implementation and validation

Status: offline implementation, mock integration and authorized live data/control checks have passed. Complete native definition catalogs and manual GUI interaction remain outside the verified coverage below. This report supersedes the Stage A feasibility plan. Updated 2026-10-12.

## Scope and contracts

The new independent repositories are Gateway.Grpc.Client, World, TacticalDisplay and CommandCenter. Existing Gateway.Api, Gateway.Grpc.Api, Gateway.Grpc, SeaPower.Client and SeaPower.Mod have been migrated together. Docs describes the current architecture. Build.CMake retains shared build/source resolution. The owner authorized publication on 2026-10-12; the integration repositories are populated on their GitHub main branches.

Domain.Api's narrowly authorized resource correction is isolated in commit `9dec63d7ae1ed1db962f846bdf0e783aec442bb4`. Its 54 tests passed again after integration. Domain.Api remains clean and frozen at that reviewed commit. No other domain types, interfaces, namespaces, build files or architecture were changed by this integration.

Equipment Instance has optional ResourceInventory(ResourceId, Quantity); Fit has optional ResourceProvision(ResourceId, Capacity, InitialInventory); Operation has optional ResourceRequirement(Use, ResourceId, Amount). Absence means resource-free. Present real zero means depleted. Consumable and allocatable semantics remain intact. Wire schemas, codecs and consumers use these grouped contracts without compatibility adapters.

The process and module diagrams are in [ARCHITECTURE.md](../../ARCHITECTURE.md). CommandCenter links exactly the three client implementation libraries. Production World depends only on Domain.Api. TacticalDisplay depends only on Domain.Api and approved GUI libraries. Test dependencies do not alter those production edges.

## Provider coverage

| Existing Domain.Api interface | Transport and World | Native mapping |
|---|---|---|
| Units.IInstanceProvider | Exact RPC and retained cache | Taskforce units, own faction or omniscient |
| Observations.IInstanceProvider | Exact RPC and retained cache | Actual faction plotting-table estimates; separate IDs |
| Operations.IProvider | Exact RPC and retained cache | Verified native activity/route and flight-deck fields |
| Factions.IProvider | Exact RPC and retained cache | Native factions and relationships |
| Units.ITypeProvider | RPC; cache retains definition errors | Visible type IDs; complete loadout/type definitions not yet mapped |
| Equipment.IProvider | RPC; cache retains definition errors | Capability IDs and verified sensor/decoy definitions; other full definitions unavailable |
| Resources.IProvider | Exact RPC and retained cache | Actual ammunition IDs and counted consumable definitions |
| Environment.ITimeProvider | Exact RPC and retained cache | Native elapsed mission seconds |
| Simulation.IPauseProvider | Exact RPC and retained cache | Cooperative effective pause |
| Simulation.IPauseController | Forwarded controller | Game-thread native pause ownership |
| Simulation.ITimeCompressionProvider | Exact RPC and retained cache | Selected native resume factor |
| Simulation.ITimeCompressionController | Forwarded controller | Validated native factor selection, including while paused |

V1 does not implement gameplay orders, environment/weather queries, AI scheduling or events. The wire service has Connect, Acquire, Read and Apply. Client reads are bounded RPC calls; World performs replication and the renderer reads only its retained cached providers.

## Equipment and native data

One physical weapon system produces one logical equipment capability per distinct supported weapon/ammunition combination. No per-round instances, invented aggregate resources or aggregate launcher inventories are created. Native system associations are retained internally. Operational state follows actual shared destruction/inoperability, reload/offline/engagement state and ammunition-specific readiness. Selected engaging ammunition can be active while competing capabilities remain unavailable. These capabilities do not imply independent firing or reload mechanisms.

Ammunition quantities come from native loaded counts; magazine-fed guns use ammunition-specific native magazine counts instead of duplicating their shared ready-round counter. Supported depleted capabilities retain zero. Sensors, reusable towed decoys and snorkel controls retain real resource-free states. The adapter merges native system collections by object identity to avoid duplicates. It does not treat every damage zone or mechanical component as an independently addressable capability.

All radar/sonar/ESM and other native sensor instances are represented. Verified effects are radar, sonar, ESM, visual/IR/periscope sensing, ECM jamming and reusable decoy deception. No invented capacities, range limits or guidance metadata are published. Complete weapon operation costs/constraints and full unit loadout catalogs currently return NotReady. Snorkel, MAD, laser and other definitions whose complete effects have not been mapped from verified sources also return NotReady; their instance states are retained.

Observer exports whitelist PositionEstimate, ErrorEllipse, known Side and known Class. They never use hidden unit name, velocity, position or classification to fill missing observations. Missing heading/speed/elevation/contributor provenance stays unknown. Native ellipse values are inspected as semi-axis radii, converted at the adapter boundary to meters, with heading converted from degrees to radians. Display scales those radii in the local map projection.

Operations publish routes only for navigation-only task views; mixed/unknown task graphs leave navigation unavailable. Unknown activity is not fabricated idle. Flight-deck fields retain known onboard/pending/reservation/cooldown values and known inventory identifiers; unreserved counts are omitted because their meaning is not yet established accurately. These static mappings still require live validation.

## Thread, lifecycle and control validation

Sampling and mutations run only on engine callbacks after outstanding jobs are complete. Worker threads handle network, native transport, disk receipts and bounded byte queues. Sampling runs every 250 ms of wall-clock time; control draining runs each safe callback independently of that interval. Publications are immutable and bounded to 16 MiB without truncation. Control queues are bounded to 64 and command admission expires; results report engine execution rather than queue admission.

Pause combines native player and client claims. Unpause releases only its owner. Disconnect release has a priority bridge channel; reconnect uses fresh ownership. Actual application awaits the next safe game-thread callback. Network loss detection is bounded by transport behavior; no instantaneous network-loss detection is claimed. Scene unload, shutdown and adapter failure clear external ownership while preserving native player ownership.

While paused, selecting compression changes the native resume index without invoking the positive TimeCompression setter. Static inspection shows that setter unpauses and completes pending flight-deck preparations; avoiding it preserves ownership. Native input hooks observe player intent and selected-factor changes. Runtime hooks are reversible and are removed during teardown. Live native-method checks establish the ownership cases below; complete native UI coverage remains unverified.

## Offline build and test evidence

Windows Release builds use CMake 4.2, Ninja, VS 2026 Insiders MSVC 19.51, x64-windows-static and static CRT. Domain.Api retains its own declared C++ standard. Internal repositories use ignored Build.Local.cmake overrides and shared Build.CMake. gRPC/protobuf and ImGui are manifest-pinned. This is a Windows/MSVC validation, not a portability matrix.

| CTest directory | Passed entries |
|---|---:|
| Domain.Api | 54 |
| Gateway.Api | 8 |
| Gateway.Grpc | 1 |
| Gateway.Grpc.Client | 7 |
| World | 9 |
| TacticalDisplay | 1 |
| CommandCenter | 1 |
| SeaPower.Client | 1 managed suite |
| SeaPower.Mod | 4 native + 1 managed suite |

All 87 CTest entries passed. Managed suites contain multiple assertions and scenarios; the entry count does not inflate those into separate CTest cases. The codec suite includes optional resource groups, present zero, malformed present groups, known false/zero, partial coordinates, typed errors and bidirectional C++/C# conformance.

World tests cover immutable revisions, cached reads, perspective isolation, session replacement, controls, wall-clock polling while paused, unchanged-revision reuse, concurrent readers and exception recovery. Proxy tests cover hidden lookups, scoped contact identity, revisions/sessions, multiple owners, disconnect/reconnect, last-applied compression and bounded unreachable-endpoint errors. Bridge tests cover owned bytes, scope rejection, expiry, buffer capacity, control results, priority release, ABI mismatch and orderly shutdown.

CommandCenter's real mock-server test exercises gRPC -> World -> retained GUI providers and Win32/D3D11 rendering for ten seconds. It switches blue/red scopes, applies pause/compression through cached controllers, stops/restarts the server, verifies stale preservation and new ownership, and reloads the mock mission. The independent TacticalDisplay development app renders local providers. These tests call controllers programmatically; manual widget interaction and game behavior are not established by them.

Managed runtime and harness builds succeed with zero errors. Runtime build reports six MSB3277 framework-reference version warnings; harness reports two. Native builds also report gRPC dependency deprecation warnings. The assemblies loaded and the gateway operated in the inspected game's Mono runtime. BepInEx reports an unresolved Unity log-writer internal-call warning on Unity 6000.0.67; its own file log, runtime transport log and harness evidence remained available.

Reproduce from each module in an x64 developer shell:

```powershell
cmake --preset windows-msvc-release -DFETCHCONTENT_SOURCE_DIR_NCBUILD=/absolute/path/to/Build.CMake
cmake --build o/windows-msvc-release --parallel 8
ctest --test-dir o/windows-msvc-release --output-on-failure
```

Set SEAPOWER_GAME_ROOT through the configured local context for game-dependent managed builds. A provisioned dependency prefix can use VCPKG_INSTALLED_DIR and VCPKG_MANIFEST_INSTALL=OFF. Changing a managed context requires explicit NAVALCOMMAND_REPLACE_MANAGED_CONTEXT=ON and reconfiguration of its consumers. Build PackageRuntime and PackageHarness targets separately; both stages carry checksum manifests.

## Inspected installation

Read-only inspection used local ILSpy output in ignored Docs/o. No proprietary source or game assemblies are part of the repository changes or packages.

| Inspected file | SHA256 |
|---|---|
| Seapower-Scripts.dll | 0FE6AAF2A6D9982E7E2EB41FD6035CB97C149773202C90821D71C00A9FDEB01A |
| UnityEngine.CoreModule.dll | 99B548DFB84B43D392B58ACFBD0218ADD906AEEEE5A2543E985587D65543AC75 |
| BepInEx.dll, version 5.4.23.2 | C65B42034BC8FFB9F0B336E416DC3884E3F99FC5A5A89EB1F2FF7868412322CD |

Static evidence: GameTime.Pause/UnPause/TimeCompression and factor selectors; JobPerformer.CompleteFixedUpdateJobs; GameMain.Update and scene lifetime; TaskforceManager and PlottingTable; Vehicle observer properties; BaseSystem and native system collections; WeaponSystem/Gun inventory queries; ErrorEllipseViewModel; PendingLaunchTask and native flight-deck collections. Static source inspection supports the mapping rationale; it is not proof that hook ordering is safe in a live game.

## Reviewable live deployment and rollback

Prepared runtime: SeaPower.Mod/o/package/0.1.0. Its top-level files are SeaPower.Mod.dll, SeaPower.Client.dll, Gateway.Api.dll, Google.Protobuf.dll, SeaPower.Host.dll, config.json and manifest.json, plus licenses. Configuration schema/ABI are 2 and runtime activation is disabled by default. Separate development package: SeaPower.Mod/o/harness/0.1.0, containing SeaPower.Harness.dll and disposable scenario files. These stages were temporarily deployed for the owner-authorized live checks and removed afterward.

Start-Live.ps1 is the maintained opt-in launcher. It checks manifests and the inspected assembly hash, refuses an existing game session or deployment directories, temporarily creates BepInEx/plugins/NavalCommand.Runtime and NavalCommand.Harness, and enables the existing doorstop loader configuration. It does not install a loader or change original game binaries. Existing plugins remain untouched. The child alone receives NC_MOD_ENABLED/NC_HARNESS_ENABLED and isolated run paths; inherited NC_* activation variables are removed from that child.

The launcher preserves doorstop_config.ini bytes, writes launch/readiness/result receipts outside the game installation, owns the started game process, and removes only its checked plugin directories in finally. Normal exit is requested when selected; a forced cleanup fallback applies only to that owned process and is recorded. The loader configuration hash is verified after restoration. No saves or original assemblies are targeted. Both normal and failure rollback paths were exercised live.

## Authorized live verification

The owner authorized use of the game on 2026-10-12. Evidence is local and ignored under `SeaPower.Mod/o/live/`; no vendor code, game references or diagnostic binaries are committed. Each run records the staged checksums, owned PID, loader/harness/transport logs and exact loader restoration. Successful runs exited through normal window close with game exit code zero.

| Evidence directory | Result |
|---|---|
| 20261012-smoke01 | Playable MinimalCombat mission; CommandCenter alongside the real gateway for 30 seconds; client and game exit zero |
| 20261012-controls01 | Found paused control starvation; failure rollback restored loader and removed owned plugins |
| 20261012-controls02 | Corrected runtime: real unit/resource data, three faction scopes, two client pause claims, selected factor 2 while paused, disconnect and fresh reconnect passed |
| 20261012-native01 | Native Pause/UnPause/SetTimeCompression calls, frozen mission time, player claim preservation on disconnect, fresh ownership and actual native mission reload passed |
| 20261012-contacts01 | Probe incorrectly waited for observing-faction contacts in the omniscient view; explicit assertion failed and rollback completed |
| 20261012-contacts02 | Surface opponent detected naturally: Taskforce1 and Taskforce2 each had one own unit and one observer-specific contact; Neutral had neither; hidden truth and unknown kinematics checks passed |
| 20261012-air01 | Airfield, carrier and frigate facilities returned native air-operation state; three own units and one enemy unit remained correctly scoped; data/control checks and normal exit passed |
| 20261012-gui01 | CommandCenter alongside NearContact for 180 seconds; screenshot helper timed out, so widget interaction remained unverified; client/game exit zero and native worker shutdown status zero |

The first pause was applied in the native job-completion postfix. The enclosing fixed update then scheduled its final jobs, leaving completion flags pending when pause stopped subsequent fixed updates. The corrected GameMain.Update postfix completes outstanding jobs while paused on the verified engine thread, without enforcing another simulation tick. This restores safe control draining and wall-clock publication. Live native evidence held mission time at 6.3016245844773948 seconds across a 600 ms pause check; native compression stayed effectively zero while selected compression changed to 2. After disconnect, the remaining native player's pause remained effective.

The actual reload used MissionManager.UnloadMissionAndReload. The old remote view became invalid, a different session supplied new unit data, and pause ownership started fresh. Engine controls in these checks were invoked programmatically through the real game methods, not by clicking native UI widgets. GUI smoke verifies process/window lifetime and normal exit; it does not establish visual correctness of every widget or uncertainty ellipse. A computer-use attempt against the live GUI could enumerate both windows, but screenshot capture failed: `FrameArrived timed out: timed out waiting on channel` for the game and `window capture timed out: timed out waiting on channel` for CommandCenter, including a fresh-window retry. No screenshot-derived input was attempted. This is a visual verification limitation, not evidence that rendering failed.

The reusable `NavalCommand.Gateway.Grpc.Client.LiveProbe` is a separately built development executable under the proxy test tree, never an unattended CTest. It accepts an explicit endpoint and checks real RPC data and controllers. Optional `--require-contacts true`, `--require-air-operations true` and `--expect-reload true` enable scenario-specific assertions. The separate `NavalCommand.SeaPower.Mod.LiveFixture` target builds `test/managed/live/SeaPower.LiveFixture.csproj`; it has no production dependency and is absent from runtime/harness packages. Explicit `-FixtureAssembly` deployment enables its native player-control requests via immutable queues. Its worker performs evidence I/O; native method calls stay on Unity's main thread. Both tools are retained as automated regression fixtures with explicit activation and bounded waits.

Reproduce a native ownership/reload run from the workspace root after building the live probe and fixture:

```powershell
& ./SeaPower.Mod/scripts/Start-Live.ps1 `
  -GameRoot C:/Games/Sea_Power_8_3 `
  -OutputDirectory C:/Software/GitHub/NavalCommand/SeaPower.Mod/o/live/new-run `
  -ClientExecutable C:/Software/GitHub/NavalCommand/Gateway.Grpc.Client/o/windows-msvc-release/test/NavalCommand.Gateway.Grpc.Client.LiveProbe.exe `
  -ClientArguments @('--fixture-directory','C:/Software/GitHub/NavalCommand/SeaPower.Mod/o/live/new-run','--expect-reload','true') `
  -FixtureAssembly C:/Software/GitHub/NavalCommand/SeaPower.Mod/o/managed/SeaPower.LiveFixture/bin/Release/netstandard2.1/SeaPower.LiveFixture.dll `
  -ReloadTest -NormalExit -ApproveLoaderChanges
```

Deployment permission remains a per-session owner decision. The example's explicit approval switch does not authorize future runs on its own. Domain.Api remains frozen and unmodified. Complete unit type/loadout and weapon metadata remain explicitly unavailable, rather than being fabricated.

Final rollback verification: no owned game/client process or NavalCommand plugin directories remained. The two preexisting plugins remained in place. Doorstop remained disabled and its restored SHA256 was `3B5FBCF2C104F967F63687A0FD240CB79A5253D08F4338A358EE796C17B5AA0A`; Seapower-Scripts.dll retained the inspected hash above. Domain.Api remained clean at `9dec63d7ae1ed1db962f846bdf0e783aec442bb4`, with all 54 tests passing again. The affected five mod tests, seven proxy tests and CommandCenter mock GUI integration test also passed again. Local follow-up commits are `75fe5d3` (paused job completion and separate native fixture) and `687d5dd` (explicit live RPC regression probe). No commits were pushed during live verification; the owner subsequently authorized publication.

Further visual acceptance should use Combined mode with an explicit disposable scenario/player side, CommandCenter executable, bounded duration and NormalExit. Check widget input, responsive rendering, heading/speed/partial ellipse presentation, native UI coverage and equipment damage/reload edge cases. Preserve screenshots and rollback receipts when extending the evidence.

## Remaining acceptance gates

The authorized Release runs establish the recorded data/control/lifecycle behavior on this inspected installation, not exhaustive real-game safety or every native UI path. Manual widget interaction, visual ellipse correctness, complete native catalog definitions and equipment damage/reload edge cases remain unverified. Complete loadout/weapon definitions and other unsupported metadata remain explicit provider failures. Debug teardown from historical builds has not been retested; no Debug acceptance claim is made. The owner-authorized integration and launcher changes are published on the main branch of each repository.
