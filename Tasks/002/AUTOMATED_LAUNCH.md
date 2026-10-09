# Task 002: automated launch

2026-10-09. **Result: works through the approved development helper**, for supplied fixtures and their native player side. Two consecutive fresh launches passed in **25.7439586 s** and **26.0266696 s**; refined readiness passed in **25.2640737 s**. The final normal run passed in **26.4920651 s**. No menu/mission/briefing click automation was used.

The user approved Anchor Chain/BepInEx installation and process cleanup and explained that Steam connectivity is intentionally disabled. Its export exception remains but did not prevent playable missions. No platform binary/check was changed. This supersedes the initial approval-blocked checkpoint.

## Working command

~~~powershell
& C:\Software\GitHub\NavalCommand\Experiments\Task002\Start-Validation.ps1 -Scenario C:\Software\GitHub\NavalCommand\Experiments\Task002\MinimalCombat.ini -Side Taskforce1 -OutputDirectory "$env:TEMP\naval-task002-new-run" -CloseAfterReady
~~~

Use a **fresh** output directory outside the installation. The wrapper starts C:\Games\Sea_Power_8_3\Sea Power.exe with that root as working directory, without invented game arguments. Child-only NC_TASK002_* variables convey ENABLED, RUN_DIR, SCENARIO, SIDE, TOKEN and optional diagnostic switches. The parent environment is unchanged. DOORSTOP_DISABLE is removed: setting it to the string FALSE disabled injection in an intermediate failed run.

The installed loader's own doorstop_config.ini is **disabled by default**. The wrapper temporarily enables it and restores the exact previous bytes in finally. [Restoration evidence](../../Evidence/Task002/doorstop-restore-check.json) records matching hashes. This edits only the added loader configuration. Existing Sea Power sessions are refused; concurrent launches are unsupported. CloseAfterReady closes only this child; omit it to leave a playable experiment open.

Invalid scenario, side mismatch, changed gameplay hash, stale/installation-contained output, early exit or evidence error fails clearly. Shell timeout defaults to 180 seconds, configurable 1–300; timeout reports its own PID and leaves it for diagnosis. The bootstrap also has a 180-second deadline.

## Readiness and native lifecycle

Readiness requires scene 2, SceneCreator.IsLoadingDone, GameMain._initialized, matching PlayerSideName, finished LoadScreen/loading camera disabled, no briefing pause, unpaused mission, a controllable own unit with HasControl and valid cached geographic position, and mission time advancing at least one second. Worker ready.json must match the wrapper's PID/GUID token; result.json records measured duration. A process or initialization flag alone does not pass.

Outputs: request.json, launch.json, probe.log, privileged.log, ready.json, result.json. Queue overflow explicitly produces evidence-error.json and refuses readiness; none occurred. A deliberately failed log open produced a writer-failed marker and no readiness. Exclude privileged.log from ordinary observer/model traces.

RuntimePlugin is an opt-in BepInEx.BaseUnityPlugin. It waits for SeaPower.Globals.mainThread equality, then installs LaunchCandidate on thread 1. The candidate waits for initial scene-1 loading to finish, preserving native warnings/checks. It sets Globals.currentMissionFilePath and calls SeaPower.MissionManager.DoLoad once, matching SeapowerUI.ViewModels.ScenarioMenuViewModel.PlayCommand.

DoLoad preloads INIs, attaches Session.SceneManager_missionLoaded, loads scene 2, and waits for SceneCreator.WaitForLoadingDone plus TerrainManager.WaitForDemData/WaitForTerrainChunks. Session calls GameInitializer.init. LoadScreen.ExecuteLoadScreen completion is checked beyond initialization flags. Our helper never forces FixedUpdate or releases pause.

BackgroundSceneController.Start selects SessionMode.Game/menu. Session.OnStart uses StandaloneMissionFilePath/[Mission] Mission only in Standalone mode. No executable direct-scenario option was verified in inspected code/manual; native/other-assembly possibilities were not exhaustively excluded.

Side is **validated**, not swapped: it must equal INI PlayerTaskforce. Fixtures contain no briefing. General briefing scenarios may time out; MissionBriefViewModel.PlayCommand/CancelAction.UnPause was not generalized here. Arbitrary side selection remains unverified.

## Community loading and integrity

Game: **0.8.3-261001**, Unity **6000.0.67f1**. Seapower-Scripts.dll SHA-256: **0FE6AAF2A6D9982E7E2EB41FD6035CB97C149773202C90821D71C00A9FDEB01A**.

The approved live-install trial used the [V1.1.0 preloader archive](https://github.com/SeaPower-Modders/AnchorChain/releases/tag/V1.1.0), containing BepInEx **5.4.23.2**, Doorstop and AnchorChain.Preloader **1.0.0**. Archive hash: **6FE07835A56E0E401AF580CFB9167CD460973F066C162F7E69DFC256B1F9A90D**. [Archive manifest](../../Evidence/Task002/loader-archive-manifest.json) lists every entry.

AnchorChain.dll was source-built from [pinned Main.cs](https://github.com/SeaPower-Modders/AnchorChain/blob/28d225be917533fcc25e19cf8f25ac44010f8abd/AnchorChain/Main.cs), source hash **5DC88976D56D7A47ADD211DFA9C0436148FAA71B712FF4532AD9BE13101BFD0C**. Correct tested location: Sea Power_Data/StreamingAssets/user/Task002AnchorChain/AnchorChain.dll. BepInEx/plugins alone did not satisfy FileManager.Directories search. Logs confirm AC V1.1.0 loaded. Our own probe directly loads from BepInEx/plugins/LaunchCandidate.dll; custom AC attributed-plugin callbacks are not certified.

BepInEx reports UnityLogWriter internal-call mismatch/Unable to start Unity log writer. Loading and our independent plain-data worker work. This is **incomplete compatibility**, not a clean certification. No library replacement suppresses it. Builds retain two MSB3277 reference-conflict warnings.

All **233** checked vendor files remain unchanged. No proprietary DLL/decompiled source is in deliverables. [Installed additions](../../Evidence/Task002/installed-files.json) lists root loader files, BepInEx tree/cache/logs, own DLL and dedicated user-content DLL; AC also created an empty ACConfigs directory. No pre-existing loader file was overwritten. Loader remains installed but disabled for ordinary starts; experiment processes are closed.

Rollback after closing experiments: retain evidence, remove only manifested additions after checking resolved paths, then compare vendor hashes. Preserve existing user content/profile/saves/original binaries. Default deactivation is not full uninstall.

See [runtime findings](SEAPOWER_RUNTIME_FINDINGS.md) and [reproduction guide](TASK002_REPRODUCTION.md). Source/scripts/own fixtures are under Experiments/Task002. Source hashes provide a checkpoint; this workspace has no Git repository. No API/gateway/client/production framework was implemented.
