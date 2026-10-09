# Task 003 reproduction

Results: unattended startup **26.020 seconds** in the completed run; one land aircraft, carrier aircraft and ship helicopter completed native preparation, launch, generic RTB, recovery and entry into cooldown. Cooldown completion/relaunch was not tested. See [findings](SEAPOWER_AIROPS_FINDINGS.md) and [machine-readable lifecycle](../../Evidence/Task003/lifecycle-summary.json).

## Prerequisites and reversible setup

Use the existing approved Task 002 installation at **C:\Games\Sea_Power_8_3**, Sea Power **0.8.3-261001** / Unity **6000.0.67f1**. Gameplay DLL SHA-256 must be **0FE6AAF2A6D9982E7E2EB41FD6035CB97C149773202C90821D71C00A9FDEB01A**. Windows PowerShell and .NET SDK 10.0.300 were used.

Read [Task 002 loading/launch](../002/AUTOMATED_LAUNCH.md) and [reproduction](../002/TASK002_REPRODUCTION.md). First-party content supplies the fixtures; arbitrary C# callbacks use the existing unofficial BepInEx 5.4.23.2/Anchor Chain setup. No new loader/preloader installation is required or was performed. The loader's UnityLogWriter mismatch remains; our independent worker supplies evidence. Steam is intentionally disconnected in this package.

The final Task 003 cleanup **restored the original Task 002 experimental plugin** and left Doorstop disabled. To repeat AirOps, build and temporarily install the expanded own plugin first. The source addition does not run unless the child receives NC_TASK003_AIROPS=1 via -AirOps. Task 002 diagnostic scripts and AirOps are mutually exclusive.

With the game closed, from C:\Software\GitHub\NavalCommand:

~~~powershell
$airOpsBuild = Join-Path $env:TEMP 'naval-task003-repeat-build'
dotnet build .\Experiments\Task002\LaunchCandidate.csproj -c Release --artifacts-path $airOpsBuild --nologo
if ($LASTEXITCODE -ne 0) { throw 'Probe build failed' }
$airOpsPlugin = 'C:\Games\Sea_Power_8_3\BepInEx\plugins\LaunchCandidate.dll'
$airOpsBackup = Join-Path $env:TEMP 'naval-task003-repeat-plugin-backup.dll'
if (Test-Path -LiteralPath $airOpsBackup) { throw 'Choose a fresh backup path' }
Copy-Item -LiteralPath $airOpsPlugin -Destination $airOpsBackup
Copy-Item -LiteralPath (Join-Path $airOpsBuild 'bin\LaunchCandidate\release\LaunchCandidate.dll') -Destination $airOpsPlugin
~~~

Expected compile result: **zero errors**, two known MSB3277 System.Net.Http/System.IO.Compression reference warnings. References have Private=false; install only our DLL, never a vendor reference. Exact before/initial/final probe hashes are in [setup.json](../../Evidence/Task003/setup.json). The initial probe lacked running checked samples; the final probe adds the job-completion callback. Retained original binary backup is in TEMP and is not a repository deliverable.

## Fixture and invocation

Own-authored [AirFacilities.ini](../Experiments/Task002/AirFacilities.ini) has Taskforce1 as PlayerTaskforce, Taskforce2 as EnemyTaskforce, no briefing/triggers, sea state 3 and disabled background content. It uses game-installed class assets, without redistributing them:

| Role | Class | Authored inventory / placement |
|---|---|---|
| Own land | airfield_small_1 / Variant1 | Two usaf_f-4e, Squadron1; map center 63.98° N, 22.6° W |
| Own carrier | usn_cvn_nimitz | Two usn_f-14a, Squadron5; 15 NM southwest of center, heading 220, telegraph 2 |
| Own helicopter ship | usn_ffg_oliver_hazard_perry | Two usn_sh-2f, Default; nearby, heading 220, telegraph 2 |
| Nonplayer comparison | usn_cvn_nimitz | Two F-14A and one E-2C, 180 NM southwest; native facility AI untouched |

This tests native facility mechanics, not a terrain/scenery-fidelity guarantee. The land asset was initialized and completed launch/recovery; disabled background data and authored positioning are not a general terrain-validity proof. Do not substitute a complex briefing mission and assume identical readiness.

One-command startup, with a fresh output directory:

~~~powershell
$airOpsRun = Join-Path $env:TEMP 'naval-task003-repeat-run'
& .\Experiments\Task002\Start-Validation.ps1 -Scenario "$PWD\Experiments\Task002\AirFacilities.ini" -Side Taskforce1 -OutputDirectory $airOpsRun -AirOps
~~~

Do not use -CloseAfterReady for a lifecycle run: readiness precedes launch. Do not combine -RunProbes. The wrapper sets only child variables, removes DOORSTOP_DISABLE, temporarily enables the existing added Doorstop config, starts Sea Power.exe with no invented game arguments, checks PID/GUID readiness, and restores exact config bytes on return. Its timeout concerns startup, not sortie completion; the ready child remains running for observation.

## Expected sequence and reported results

RuntimePlugin installs AirOpsProbe on verified main thread 1. Harmony intercepts FlightDeck.createLaunchTask for attribution and JobPerformer.CompleteFixedUpdateJobs for cached sampling. Require object/collision completion flags; there is no global ECS fence claim.

- Before dispatch, native scene setup creates a prepared, permission-held ASW task on the own FFG. Native nonplayer AI requests AEW/CAP through AI.HandleCarrierFunctions independently of the disabled AIPlayer. Privileged logs preserve those actual call stacks.
- At approximately **8 mission seconds**, the probe acquires an initially unpaused disposable-test pause. For each own facility, it selects the first supported stocked type, available squadron/callsign and resource-supported non-None loadout. The observed choices are F-4E AirToAir, F-14A AirToAirLongRange and SH-2F ASW.
- It creates one prepare-only request, toggles permission twice, queues reprioritization, then immediately aborts. Counts/ammo points restore. The canceled task cannot prove actual queue reordering. It then submits one normal launch request.
- After one wall second, it verifies the simulation tick is unchanged and resumes only this disposable-test pause, requesting native **5×** compression. No player/menu interaction occurred. No general pause lease is claimed.
- Realistic timings remain unchanged: **900 / 900 / 1800** base preparation work. No skip-ready-up, registry insertion, instant recovery or forced FixedUpdate is used.
- Spawn callbacks occur at **908.315 / 908.315 / 1808.354 mission seconds**, while the aircraft are still not in flight. Native taxi/takeoff follows.
- Airborne confirmations are **1015.163 / 997.598 / 1930.086** for land/carrier/heli, requiring inFlight=true, cleared deck state and own ordinary-unit membership.
- About 15 mission seconds later, the script revalidates own control and absence of advanced aircraft evasion, then issues ordinary ObjectBase.setOrder(ReturnToBase, facility, false).
- Recovery reaches native plane Rollout or HelicopterLanding, runway/hangar/elevator stages, object removal, ammo return and a CoolDownTask at **1363.287 / 1535.662 / 2176.341**.
- The actual run stopped at approximately **2438.46 mission seconds**. Stock return after cooldown and a subsequent launch/UID were not observed. Do not infer persistent airframe identity or repaired condition from this trace.

At 5×, the three recoveries took roughly seven to eight wall minutes after startup; native compression caps and hardware may change timing. Stop after the helicopter recovery/cooldown record if reproducing this bounded test. Continuing to stock restoration is a different, longer experiment, especially the F-14's sampled ~7200 work cooldown. Invalid input/deck damage/sea-state extremes/strategic conflicts were not tested.

## Evidence and cleanup

Outputs are request.json, launch.json, ready.json, result.json, probe.log and privileged.log. The **launcher result proves startup only**. Stage evidence and lifecycle-summary.json establish the later sortie outcomes. An evidence-error.json means the run cannot be accepted.

Retained files under Docs/Evidence/Task003:

- initial-* — first startup/request run; no passing running Update snapshots.
- facilities-* — completed job-hook run, PID 31580.
- lifecycle-summary.json — parsed stage timestamps, cooldown samples, test flags.
- static-members.json — native class/member locations and inspected hashes, no proprietary source.
- setup.json, result-summary.json, final-check.json — plugin provenance, gates, integrity/cleanup.

probe.log contains own facility/unit diagnostics; its native UIDs and simulator timing are experimental data, not operational API fields. **privileged.log contains nonplayer inventory/decision truth and must be excluded from ordinary observation/model/training inputs.** Snapshot-local caches are not globally coherent. The readyCount diagnostic label means unreserved aggregate stock, not prepared aircraft.

Close the owned process and restore our prior plugin, leaving the original loader installation untouched:

~~~powershell
$airOpsLaunch = Get-Content -LiteralPath (Join-Path $airOpsRun 'launch.json') -Raw | ConvertFrom-Json
Stop-Process -Id $airOpsLaunch.pid -Force
Wait-Process -Id $airOpsLaunch.pid -Timeout 10 -ErrorAction SilentlyContinue
Copy-Item -LiteralPath $airOpsBackup -Destination $airOpsPlugin
~~~

Wait for Windows to release the mapped DLL before restoration if necessary. Preserve evidence first. Rehash vendor files using Task 002's 233-file baseline; verify enabled=false and no installation-owned test helpers remain. The executed run restored Task 002 plugin bytes; no vendor executable/managed assembly was modified. No savegame/settings file was manually edited, and no public framework/API/UI was built.

