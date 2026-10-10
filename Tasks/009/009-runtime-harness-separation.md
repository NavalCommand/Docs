# Task 009 — Production Managed Runtime and Reusable Development Harness

**Type:** Structural and behavioral-maintenance implementation task. **Status:** Completed; see [implementation report](009-implementation.md). The existing Debug gRPC shutdown issue remains unresolved.

**Prerequisites:** Tasks 004–005 established the gateway and module layout. Keep Tasks 007 (code readability) and 008 (formations/weapon posture investigation) as distinct assignments; **Task 008 has not yet run**. Complete this cleanup before using the harness for Task 008's runtime experiments.

## Objective

Refactor `NavalCommand/SeaPower.Mod` so that the shipped managed component is a small, reliable, production-quality composition/transport runtime, while the development harness is a generic, optional tool for launching Sea Power test scenarios, observing readiness/lifecycle and collecting evidence. **Remove task-specific experiment semantics from maintained runtime and harness code.** Preserve the existing gateway, native ABI, canonical Protobuf contracts, gameplay behavior and validated tests.

Reference the actual current `main` checkout, not the historical Task 004/005 reports. Known places to inspect include:

- `managed/Runtime.cs`: `NC_TASK002_SIDE`, `NC_TASK002_RUN_DIR`, `NC_TASK004_NATIVE_DLL`, `NC_TASK004_ENDPOINT`; task-labelled Harmony identifier and hard-coded defaults; intertwined worker transport, logging, Harmony scheduling and provider lifecycle.
- `managed/Bootstrap.cs`: `NC_TASK004_ENABLED` gate.
- `app/harness/RuntimePlugin.cs`: plugin identity `dev.navalcommand.task002`, `NavalCommand.Task002` namespace, numerous `NC_TASK002_*`, `NC_TASK003_AIROPS`, `NC_TASK004_*` switches; mixes loader, scenario launcher, probes, lifecycle/reload tests and receipt writing.
- `app/harness/LaunchCandidate.cs`, `DiagnosticProbe.cs`, `AirOpsProbe.cs`: determine which pieces are generic/reusable versus historical one-off probes.
- `managed/SeaPower.Mod.csproj`, `app/harness/LaunchCandidate.csproj`, `scripts/Build.ps1`, `scripts/Start-Live.ps1`, `app/harness/Start-Validation.ps1`, CMake-generated managed paths, test runners and documentation: move references and separate packaging. `Build.ps1` currently includes a task-named `Task004.LaunchCandidate.dll` in the general package.

Document a concise before/after inventory (files, package contents, startup paths and known regressions) before editing.

## 1. Directory layout

Use **`managed/runtime/` and `managed/harness/`**. The top-level `managed/` explicitly groups C# code; `app/` retains its existing CMake application/composition role, and `src/` continues to own the native C++ host.

```text
SeaPower.Mod/
├── managed/
│   ├── runtime/
│   │   ├── SeaPower.Mod.csproj
│   │   ├── Bootstrap.cs
│   │   ├── Runtime.cs
│   │   ├── NativeTransport.cs
│   │   └── ... small, purpose-specific production classes
│   └── harness/
│       ├── SeaPower.Harness.csproj
│       ├── HarnessPlugin.cs
│       ├── MissionLauncher.cs
│       ├── EvidenceWriter.cs
│       └── scenarios/          # Reusable checked-in scenario fixtures
├── src/                       # Existing native host
├── app/                       # Existing CMake application/composition wiring
├── test/
└── scripts/                   # Build, packaging and launcher scripts
```

The tree is illustrative: do not create empty classes/directories merely to match it. Keep PowerShell wrappers in `scripts/` and move C# into `managed/`. No C# production sources remain in `app/harness/`. Update all `.csproj` source/project references, native/CMake metadata, packaging, test scripts, samples and README paths atomically; no stale paths or historical DLL names.

## 2. Independent, production-quality runtime

- **Must start and operate without the harness installed or enabled.** Identify and implement a stable production bootstrap compatible with the existing approved injection/loader mechanism. The harness must never be responsible for loading, reflection-attaching or enabling the production mod.
- Use stable IDs/namespaces such as `NavalCommand.SeaPower.Mod` and a stable runtime plugin identity; no `TaskNNN`, probe/lifecycle flags, or historical experiment IDs in shipping assemblies, configuration, package filenames, or public APIs.
- Implement an explicit, validated runtime configuration/activation path. Respect the existing default-disabled posture, loopback endpoint default, bounds and opt-in safety. Read the existing versioned `config.json` or a coherent successor instead of leaving it as unused packaging data. If environment overrides are necessary, use stable, documented `NC_*` names, validate them and keep experimental overrides harness-only. Do not force side `Taskforce1`: bind to the **current legitimately selected player side** or refuse startup when it is unavailable/ambiguous.
- Keep the managed component **thin**: composition, lifecycle, thread-safe byte mailbox, main-thread sampling/dispatch, safe native ABI forwarding, startup/shutdown and operational diagnostics. Keep game-specific read/order logic in `SeaPower.Client`, the generic server in `Gateway.Grpc` and canonical schemas in `Gateway.Api`.
- Refactor the existing large Runtime for comprehensibility into narrowly responsible units as needed. No speculative framework or new gameplay APIs. Preserve bounded queues, backpressure, idempotency/session invalidation, faction visibility and native protective state guards. No Unity/game objects may cross into background workers.
- Handle missing/invalid paths, unwritable log directories, absent native DLL, ABI mismatch, disabled configuration, startup races and worker errors deterministically. The runtime must not require a test-run output directory to write normal logs. Avoid silent catch-all failure paths where they hide production defects.
- Make startup/stop/reload idempotent. Unsubscribe patches/events on teardown; ensure off-thread orderly worker/native shutdown is tested without blocking a Unity callback; preserve the documented no-unload-while-running rule. Do **not** claim graceful game-exit teardown is verified without a live normal-exit test.
- Preserve existing Mono/Unity assembly-load ordering and platform-shim compatibility where empirically required; do not introduce unmanaged binary rewriting or load new incompatible `System.*` assemblies.

## 3. Generic development harness, not accumulated experiments

- Provide a stable **development-only** harness, disabled unless explicitly opted into a run. It may offer scenario selection, chosen native player side, approved menu-skipping mission launch, readiness check, evidence capture, timeout/cleanup and deliberately requested lifecycle/reload tests.
- The reusable core must expose **generic actions and configuration**, not `Task002`, `Task003`, `Task004`, `AirOpsTestMode` or an ever-growing collection of task-specific switches. Do not turn this into a generic code-execution engine or an unnecessary plugin ecosystem: a small cohesive harness API/configuration is preferable. Keep externally observable readiness/error receipts stable or provide a documented compatibility transition for automation.
- Isolate historical one-off probes (`DiagnosticProbe`, `AirOpsProbe`, etc.) from the reusable harness. Remove obsolete experiment logic from maintained sources; archive source/configuration with original task evidence under `Docs/Tasks/` or `Docs/Evidence/` where needed for reproducibility. For specialized future probes, use separate opt-in experiment fixtures or test modules that consume harness primitives rather than modifying its core.
- Harness must be able to start/readiness-check a scenario **without the mod**, and also test the independently bootstrapped production mod when explicitly requested. It must not silently enable production control or issue commands.
- Keep strict developer-run safety: never touch pre-existing game sessions, only terminate processes launched/owned by this run, require explicit permission for loader configuration changes, restore exactly the originally present own helper/loader settings even on failure, validate checksums and paths, and leave normal game launching inactive when cleanup ends.
- Preserve required smoke, blocked native automation, reload and mock regression fixtures via small configuration/test drivers rather than task-number branches in the harness core. Do not silently discard existing regression coverage.

## 4. Separate builds and distributions

- Build production managed runtime and optional harness as **distinct projects and artifacts**. Normal runtime build/package **must exclude all harness assemblies, scenario files, probes and task-evidence writers**. Provide a separate development-harness build/stage/run path.
- Ensure production and harness can be tested independently and together, with explicit opt-in and no dependency from runtime to harness. Document their exact loader assembly names, locations, activation and outputs; no historical `Task004.LaunchCandidate.dll` aliases in new packaging.
- Keep C++ target placement and the shared `Build.CMake` architecture as currently established; no build-system redesign. Preserve vcpkg `x64-windows-static` (`/MT`, `/MTd`) and the configured compiler/toolchain choices. If a verified dependency/runtime issue necessitates deviation, report and resolve it explicitly rather than silently reverting to `static-md`.
- Do not package proprietary game assemblies, additional platform `System.*` DLLs or vendored binaries; preserve license and package-manifest checks.

## 5. Verification and required evidence

Demonstrate at least:

1. Clean managed runtime build and native/module regression tests with corrected paths; clean separate harness build.
2. Production package manifest contains **no harness or task-named artifacts**; static source search finds no `Task002/003/004` IDs, `NC_TASK*` variables, or old harness paths in maintained production code and scripts. Exceptions only in intentionally archived historical evidence.
3. Start Sea Power with **no harness** and explicitly enable the production plugin: get faction-restricted `GetView` from external CLI, valid waypoint dispatch, invalid command rejection, reconnect/session invalidation as supported. Demonstrate runtime remains inactive when disabled.
4. Run **harness-only** scenario startup, readiness/evidence and exact cleanup/restore with mod disabled. Demonstrate a separate run with both harness and production runtime explicitly enabled, without the harness being a prerequisite for runtime startup.
5. Re-run applicable mock, live waypoint, protected-native-action and reload tests; compare behavior to the previously recorded results. Exercise startup failures and run-directory/log failures without crashing game or masking them.
6. Verify no game vendor hashes changed and no game/loader helper was left installed, enabled or running after tests. Clearly separate forced-kill test cleanup from genuine graceful normal-exit evidence.

Create a concise implementation report at `Docs/Tasks/009/009-implementation.md` and focused receipts/source inventories under `Docs/Evidence/Task009/`. Keep `Docs/AGENTS.md`, `ARCHITECTURE.md`, `MODULE_STRUCTURE.md` and other permanent documents **task-agnostic**; update them only if permanent design contracts change. Project docs live in their own `Docs` Git repository, not an unsynchronized workspace parent. Follow `Docs/CODE_STYLE.md` and `Docs/CodeStyle.xml` for all hand-authored edits.

## Out of scope

Do not implement the formations/weapon-posture investigation of Task 008, new AirOps behavior, LLM control, additional order types, new native interfaces, or a general launcher rewrite unrelated to safe harness separation. Do not discard working Task 004–006 functionality merely to simplify the refactor. Document unverified aspects honestly.
