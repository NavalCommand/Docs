# Task 009 implementation

Status: completed, with the pre-existing Debug gRPC shutdown issue retained as unresolved.

Implementation baseline: actual source checkouts captured in `../../Evidence/Task009/before-inventory.json`; no historical report was used to restore or implement code. Task 008 was not executed. The provider, gateway, canonical Protobuf messages, RPC definitions, native host implementation and C ABI exports are unchanged.

Local source commits in SeaPower.Mod:

- `fbb9680`: the separately requested shared Windows MSVC preset extraction. Developer CLion toolchain selection remains in ignored user presets.
- `f34ab00`: production managed runtime / reusable harness separation, scripts, packaging and regression tests.

No commits were pushed. Pre-existing owner changes in `.gitignore`, the README TODO and `src/CMakeLists.txt` remain outside these commits. Permanent documentation now makes the independent bootstrap and harness boundaries explicit without task-specific runtime policy.

## Before and after

| Area | Before | Implemented |
|---|---|---|
| Production C# | `managed/` with task-numbered environment configuration; harness worker loaded dependencies and reflection-attached runtime | `managed/runtime/SeaPower.Mod.csproj`; independent BepInEx `Bootstrap`, versioned `RuntimeConfiguration`, scheduling/composition `Runtime`, byte-only `RuntimeMailbox`, `TransportWorker` and existing-ABI `NativeTransport` |
| Development C# | `app/harness/` combined scenario startup, metadata loading, diagnostic probes, AirOps probes, reload and evidence | `managed/harness/SeaPower.Harness.csproj`; generic `HarnessPlugin`, `MissionLauncher`, bounded `EvidenceWriter`, reusable `scenarios/` |
| Startup | Harness plugin was required to load/enable the production composition | `dev.navalcommand.seapower.mod` bootstraps independently; `dev.navalcommand.seapower.harness` can independently start/readiness-check scenarios |
| Native targets | Host/ABI tests in src/test; app contains composition metadata | Existing locations, targets and ABI preserved; two tiny test-only invalid-ABI/export DLL fixtures added in test |
| Packaging | General package included a task-named harness DLL | Production and harness have distinct build scripts, stages and manifests; production excludes developer assemblies, fixtures, probes and platform/game references |
| Launch scripts | Separate wrappers carried historical switches, overwrote an existing helper and recovered ownership from PID receipts | One maintained ownership transaction with Production/Harness/Combined modes, explicit loader-change approval, fresh output/deployment validation, manifest hashes, child-only activation, exact loader rollback and owned-process cleanup |

The old diagnostic and AirOps probes, with their original coordinator source, are frozen under `ArchivedProbes/` for reproducibility. They are not compiled or activated. Scenario fixtures, inspected menu readiness/load behavior, token/PID receipts, generic evidence bounds and the explicit reload regression action were promoted. Historical metadata-loading and task-specific activation branches were deleted from maintained code. The source build helper and request preparation wrapper moved to `scripts/`. No experiment or executable probe is shipped.

## Runtime configuration and lifecycle

The production package ships disabled configuration schema/ABI 1. Configuration is actually consumed from beside the plugin, or from absolute `NC_MOD_CONFIG`. `NC_MOD_ENABLED=1` explicitly activates; `0` disables; other values fail validation. The loopback endpoint, fixed existing message/queue limits and disabled auto-client launch are validated. Optional JSON `nativeLibrary` and `logDirectory` resolve against the module directory; normal logs default to local application data. `NC_MOD_LOG_DIRECTORY` supplies a stable diagnostic override, without requiring a test-run directory.

The bootstrap prepares configuration, write access and metadata off-thread. It loads the installed Unity platform shims before Protobuf/provider dependencies, then composes on the verified game thread. No additional System.* assemblies are shipped. Startup errors are reported through BepInEx even when a runtime log cannot be written.

The runtime binds `SceneCreator.PlayerSideName` only when the actual native player taskforce is ready; it does not force Taskforce1 or accept an arbitrary observer-side override. The unchanged provider filters against that native taskforce. Sampling/dispatch remains behind the existing engine-thread and job-completion gates. Background work receives only byte arrays, immutable diagnostic strings and scalar lifecycle state.

Queues retain the existing command/result bounds, retryable publish backpressure and latest-view behavior. Errors retain their causes, oversize native order lengths are rejected, and the native loader validates ABI/exports with restricted Windows dependency search. Native start/stop are idempotent for the owned transport. The runtime guards overlapping hosts while off-thread shutdown completes. Teardown removes patches/events, invalidates provider state and signals the worker without joining it in Unity callbacks or unloading the native DLL. Mission reload invalidates sessions/handles and binds the next player side.

## Builds, distributions and activation

From an initialized VS 2026 x64 developer shell with the standard vcpkg setup and local dependency overrides:

```powershell
./SeaPower.Mod/scripts/Build.ps1 -GameRoot ACTUAL_INSTALL -CMake cmake -BuildCMakeSource ACTUAL_BUILD_CMAKE_CHECKOUT
./SeaPower.Mod/scripts/Build-Harness.ps1 -GameRoot ACTUAL_INSTALL
ctest --test-dir SeaPower.Mod/o/windows-msvc-release --output-on-failure
```

Production stage: `SeaPower.Mod/out/package/0.1.0`, containing SeaPower.Mod.dll, SeaPower.Client.dll, Gateway.Api.dll, Google.Protobuf.dll, SeaPower.Host.dll, disabled config, licenses and checksum manifest. Stage it together under the approved BepInEx plugin directory `NavalCommand.Runtime`. No loader binaries are redistributed. Development stage: `out/harness/0.1.0`, containing SeaPower.Harness.dll and reusable scenario fixtures; the harness plugin goes under `NavalCommand.Harness` for a deliberately requested run.

Harness configuration uses only `NC_HARNESS_ENABLED`, `NC_HARNESS_SCENARIO`, `NC_HARNESS_SIDE`, `NC_HARNESS_RUN_DIRECTORY`, `NC_HARNESS_TOKEN` and optional `NC_HARNESS_RELOAD`. It never loads or enables the runtime and never issues operational commands. Readiness retains token/PID `ready.json`; logs transition to `harness.log`. Historical probe/interference switches have no compatibility aliases. `Start-Validation.ps1` remains a harness-only entry point but now always cleans up its owned run.

```powershell
./SeaPower.Mod/scripts/Start-Live.ps1 -Mode Harness -GameRoot ACTUAL_INSTALL -OutputDirectory NEW_EVIDENCE -ApproveLoaderChanges
./SeaPower.Mod/scripts/Start-Live.ps1 -Mode Production -GameRoot ACTUAL_INSTALL -OutputDirectory NEW_EVIDENCE -Cli ACTUAL_CLI -Smoke -ApproveLoaderChanges
./SeaPower.Mod/scripts/Start-Live.ps1 -Mode Combined -GameRoot ACTUAL_INSTALL -OutputDirectory NEW_EVIDENCE -Cli ACTUAL_CLI -ReloadTest -ApproveLoaderChanges
```

Production mode never chooses a mission: use the game menu. Smoke/blocked modes explicitly authorize the existing regression mutations; ordinary view queries are read-only. `-NormalExit` requests normal window-close, with forced fallback clearly recorded as a failed normal-exit attempt. Existing game sessions/deployments are refused; no PID from a prior receipt is treated as process ownership. The launcher checks fresh loader-log timestamps so old errors cannot satisfy a new run's failure/readiness conditions.

## Verification

Evidence is under `../../Evidence/Task009/`, indexed by its README. All builds and tests below were actually executed.

| Acceptance area | Evidence / outcome |
|---|---|
| Clean managed runtime | Clean plus successful production build; six existing game-reference conflict warnings, zero errors |
| Separate clean harness | Clean independent build and separate staging; two game-reference conflict warnings, zero errors; unused probe-only project references removed |
| Clean native composition | Ninja clean-first build, 55 build steps, VS 2026 MSVC, `/std:c++latest`, static `/MT`, existing x64-windows-static source-built vcpkg prefix |
| Native/managed regression | Release host ABI and managed-runtime-harness CTest both pass; 27 focused managed checks include disabled configuration, invalid schema/ABI/endpoint/bounds, missing paths, actual wrong-ABI/missing-export DLLs, backpressure, byte-copy dispatch, shutdown and unwritable evidence/log paths |
| Independent unchanged modules | Existing Release gateway, provider and codec CTest pass; existing C# → C++ → C# conformance and canonical schema-lock tests pass |
| Independent mock hosting | Existing external CLI smoke pass, 111 m mock displacement |
| Harness only | Two live harness-only readiness runs, including final reduced-reference build with production mod and legacy helper absent; token/PID receipts and exact rollback pass |
| Combined | Explicitly enabled independent plugins pass live waypoint smoke: native task creation, invalid command rejection, idempotent result and 5.05963 m observed movement |
| Production without harness | Legacy helper suspended, new harness absent, scenario chosen through normal game UI; faction-restricted GetView, four invalid commands rejected, waypoint native task creation and 5.23559 m movement pass |
| Default-disabled posture | Production-only plugin logs disabled; no native gateway readiness artifact/worker was started |
| Protected native action | Existing aircraft-threat fixture rejects waypoint with protected_native_action; native automation guard preserved |
| Reload/reconnect | Existing CLI observes new session/view, rejects prior session and prior entity handle; RELOAD_PASS |
| Startup/log failures | Live missing-native and unwritable-log runs report explicit bootstrap errors while game process remains alive until owned cleanup; no silent readiness pass |
| Normal exit | Production-only normal window-close completed without forced fallback; runtime log records worker-stop status=0. This verifies that Release run, not every toolchain/exit path |
| Packaging/source isolation | Final production manifest/inventory excludes harness, scenarios, test DLLs, probes, task-named files and System.* references; maintained source search has no Task002/003/004 IDs, NC_TASK variables or old harness/project paths |
| Safety/cleanup | 231 installed managed vendor DLL hashes and executable/UnityPlayer hashes unchanged; original Doorstop/helper hashes restored; no new deployments, temporary UI fixture or game process remains |

These outcomes preserve the previous Release smoke/protected-action/reload contract checks. Task-specific diagnostic/AirOps experiments were archived rather than used as new maintained features. Task 008 remains unstarted.

### Existing Debug issue and limits

The Task 007 Debug gRPC process-exit/cleanup access violation remains **unresolved**. This task changes no gateway code, shutdown ABI or dependency versions, adds no exit suppression and skips no failing assertions. Current existing Debug host and gateway CTest reruns both passed; the recorded failure was not reproduced with these local artifacts. That observation is not a root-cause explanation or a fix. The historic evidence remains intact; broad Debug live deployment/shutdown guarantees are not claimed.

The game prints the already present BepInEx UnityLogWriter internal-call warnings during loader startup; live readiness and Release behavior above still passed. Managed builds retain their existing transitive game-assembly version warnings. No extra platform DLLs were introduced to conceal them. Full version/toolchain/loader portability is outside this maintenance task.

The original legacy helper was already installed before this task. Acceptance fixtures temporarily suspended it and restored its exact bytes; maintained launchers neither depend on it nor rewrite it. Normal Doorstop remains disabled. All newly staged runtime/harness files were removed. Forced cleanup receipts are kept distinct from the genuine production-only normal-exit receipt.
