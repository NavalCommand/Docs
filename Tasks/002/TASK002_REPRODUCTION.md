# Task 002 reproduction

The user explicitly approved community loading/process cleanup. These are disposable development experiments, not production API/client behavior.

## Prerequisites and source builds

Windows PowerShell, .NET SDK 10.0.300, approved BepInEx 5.4.23.2/preloader files, and Sea Power at C:\Games\Sea_Power_8_3 matching the report hash. Steam is intentionally disconnected; no platform changes are required.

The V1.1.0 ACPreloader archive's URL/hash/entries are in AUTOMATED_LAUNCH and loader-archive-manifest.json. For reinstall, review every target and refuse pre-existing loader paths; extract only into the explicitly approved root. Do not replace vendor assemblies/user files. Default Doorstop enabled=false deactivates the installed loader.

Repeat the **offline source build** for Anchor Chain:

~~~powershell
& C:\Software\GitHub\NavalCommand\Experiments\Task002\Build-AnchorChain.ps1 -WorkDirectory "$env:TEMP\naval-task002-new-anchor-build"
~~~

This was executed successfully. It verifies pinned Main.cs hash, references local approved loader/game DLLs read-only and prints its output path; it does not install/activate. Tested destination: Sea Power_Data/StreamingAssets/user/Task002AnchorChain/AnchorChain.dll. BepInEx/plugins alone does not satisfy AnchorChain.Preloader's content search.

Build our own probe:

~~~powershell
dotnet build C:\Software\GitHub\NavalCommand\Experiments\Task002\LaunchCandidate.csproj -c Release --artifacts-path "$env:TEMP\naval-task002-build" --nologo
~~~

Expected: zero errors, two known MSB3277 warnings. Only own bin/LaunchCandidate/release/LaunchCandidate.dll is installed into approved BepInEx/plugins/LaunchCandidate.dll, with the game closed. Never copy its game references. Allow Windows to release mapped DLLs after stopping the game before changing the plugin; an immediate overwrite failed in an intermediate retry.

## Launch and diagnostics

From C:\Software\GitHub\NavalCommand:

~~~powershell
& .\Experiments\Task002\Start-Validation.ps1 -Scenario "$PWD\Experiments\Task002\MinimalCombat.ini" -Side Taskforce1 -OutputDirectory "$env:TEMP\naval-task002-new-launch" -CloseAfterReady
~~~

Every output directory is fresh/outside installation. The wrapper temporarily enables added loader config and restores original bytes in finally, validates child PID/token readiness, records timing, and enforces timeout. CloseAfterReady stops only its child. Omit it to collect later evidence. Existing sessions are refused. The supported side equals mission PlayerTaskforce. General briefings may time out; no silent unpause/general side swapping occurs. Prepare-LaunchRequest alone validates offline and never launches.

Use the same command with the following fixture/switch combinations and new output directories:

| Fixture/switches | Already executed observations |
|---|---|
| MinimalCombat.ini, RunProbes | Far hidden opponent: zero foreign tracks. Player waypoint ~8 s, nonplayer waypoint ~18 s; retain/move across intervals. Pause ~65 s, insertion/no tick advance; competing repeat pause ~70 s leaves world paused |
| NearContact.ini, RunProbes ObserveOnly | Surfaced opponent 0.5 NM/radar active: estimates on both tables; final source also logs cache epoch/uncertainty. No normal waypoint/pause script |
| AircraftThreat.ini, RunProbes ObserveOnly | Native preset attack triggers own evasion, guard rejects waypoint, active state remains unfinished; incidents persist. Aircraft later destroyed |
| AircraftThreat.ini, RunProbes ObserveOnly DeliberateInterference | Separate intentional native CeaseFire cancellation during evasion: finished=true/immediate exit |
| MinimalCombat.ini, RunProbes LifecycleTest | Own test pause then native unload/reload at ~15 s; references/lease invalidated, new-generation samples/tasks |

Switches use PowerShell leading hyphens, for example:

~~~powershell
& .\Experiments\Task002\Start-Validation.ps1 -Scenario "$PWD\Experiments\Task002\NearContact.ini" -Side Taskforce1 -OutputDirectory "$env:TEMP\naval-task002-new-observation" -RunProbes -ObserveOnly
~~~

ObserveOnly suppresses normal waypoint/pause scripts, but aircraft evasion instrumentation still exercises the requested guard/interference comparison. Use only disposable scenarios. No AI/defense flag is disabled by our probes; native AIPlayer forced-disable state limits strategic conclusions.

## Evidence boundaries and cleanup

probe.log holds lifecycle/status/whitelisted OBS lines. privileged.log holds separately routed nonplayer truth-derived control/displacement; exclude it from ordinary observer/model traces. Cache epoch is not sensor measurement age; native uncertainty units remain unresolved. No BaseObject/UID/name/true velocity/Position fallback/destruction truth enters OBS fields. Snapshot-local labels are not production persistent IDs.

The job hook checks object/collision completion flags for a cached managed slice; it does not certify all ECS dependencies. Paused commands use Update without forcing FixedUpdate. Native loader logging reports UnityLogWriter incompatibility; the independent plain-data worker supplies evidence. Overflow is explicit failure. Detailed confidence/untested conditions are in SEAPOWER_RUNTIME_FINDINGS.

Close only experiment PIDs from launch.json and wait for termination before a new launch/DLL update. The user authorized closing leftover Sea Power test processes, not unrelated applications. Final task state has no installation-owned test processes left intentionally running and Doorstop disabled. No compiled DLL/proprietary asset is in repository deliverables.

Full rollback uses installed-files.json: remove only added root .doorstop_version/doorstop_config.ini/winhttp.dll, added BepInEx paths, dedicated user/Task002AnchorChain DLL/folder and empty created ACConfigs, after verifying resolved targets. Preserve existing user tree/profile/saves/original content. Retain evidence and recompare all 233 vendor hashes. Own TEMP build/source/request artifacts are not runtime dependencies and can be cleaned later.
