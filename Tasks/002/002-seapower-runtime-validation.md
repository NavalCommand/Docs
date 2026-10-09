# Task 002 — Runtime feasibility, native control, and direct-to-scenario startup

**Status:** Completed on 2026-10-09; retained as historical task instructions. Results: [`SEAPOWER_RUNTIME_FINDINGS.md`](SEAPOWER_RUNTIME_FINDINGS.md). **Current assignment:** [`003-air-operations-investigation.md`](../003/003-air-operations-investigation.md). **Priority at time of execution:** runtime proof over framework implementation.

## Read first and evidence baseline

Read the repository-root `AGENTS.md`, `Docs/ARCHITECTURE.md`, `Docs/001-seapower-investigation.md` (historical), and **`Docs/SEAPOWER_FINDINGS.md`** (Task 001 results) before making changes. All documentation, task files, findings, test instructions and reports belong in **`Docs/`**, with a capital D. Do not create `Doc/`, `docs/` or `tasks/`.

Task 001 statically inspected Sea Power **0.8.3 Build 261001** (`Seapower-Scripts.dll`; precise SHA-256 is in the findings). The following are *leads supported by decompilation*, **not runtime-tested facts**:

- `Taskforce` creates faction-specific `PlottingTable`s; native `Vehicle` wrappers mix observation estimates with authoritative truth.
- `ObjectBase.setWaypointTask` / `SetWaypointTask`, `UserRootNode`, and related Native/NodeCanvas tasks implement persistent high-level orders. Native AI might override them.
- `AircraftStates.AdvancedMissileEvasion` uses typed aircraft state and temporarily owns movement; UI-issued movement can reach `Aircraft.CeaseFire` and cancel evasion.
- `GameTime.Pause`/`UnPause` exist, but pause ownership and accepting commands while paused are unresolved.
- `GameMain.PerformFixedUpdate` launches new jobs before it returns. A postfix is **not automatically a safe snapshot point**.
- `RPCServer`/`StreamJsonRpc` and scripting facilities suggest integration options but do **not** prove a usable local/remote control endpoint.
- Some environmental getters mutate Unity global RNG/cache. **Do not use them as harmless observational reads.**

Validate the installed game build/assembly before relying on these hooks. If the installation changed, document the version/hash mismatch and reassess affected findings.

## Goal and scope

Produce a **small reproducible runtime feasibility prototype** establishing whether Sea Power can be driven and observed safely through a minimal C# integration, while preserving native automation. In particular, determine and—if possible—implement a **single-command, unattended launch into a chosen combat scenario**, bypassing the main menu, mission selection and briefing. This launch facility is essential test infrastructure for future automated and Codex-assisted debugging.

This task **permits** narrow, reversible experimental code, helper scripts, a minimal development-only startup mod, and logging in an isolated test setup. It **does not authorize** a full C++ gateway, generic protobuf schemas, LLM controller, polished UI, or broad refactoring of native AI.

Do not copy proprietary game binaries/decompiled source into the repository, bypass licensing or platform checks, disable protective behavior globally, or make irreversible edits to the user's game installation. Prefer a separate test copy or another clearly reversible installation method. Never silently modify player savegames/settings. Limit experiments to local/disposable scenarios.

## Modding compatibility and file-integrity guardrail (mandatory for every milestone)

**Follow the hierarchy in `AGENTS.md`:** verify first-party Sea Power modding, mission editor/Workshop/user-content and documented scripting/startup interfaces *before* looking at third-party injection. An officially supported content mod does not imply official support for loading arbitrary C# DLLs. The task is allowed to use decompilation **for inspection only**, never to replace any shipped assembly.

If native/official extension points cannot achieve direct scenario launch or runtime observation, investigate established community code-mod loading (for example Anchor Chain/BepInEx, if compatible with this exact game build). Treat it as **community-supported, not first-party supported**. Describe all extra files, installation actions, load sequence and reversibility. **Obtain user approval before installing or activating a community loader/preloader or writing into the actual installation, even if the change only adds files.** You may prepare a minimal plugin/source and test it without modifying the installed game until approval is received.

**Hard prohibition:** never patch, overwrite, replace, reassemble, or byte-edit `Sea Power.exe`, `UnityPlayer.dll`, `Seapower-Scripts.dll`, other shipped managed DLLs, or vendor-owned binaries on disk. Only optional, reversible add-on code in approved extension/plugin locations is eligible for runtime experiments. Prefer non-destructive, in-memory instrumentation when permitted through an approved loader; do not use a custom binary patcher. Never bypass DRM/integrity validation.

The runtime report must include a **modding-method evidence table**: mechanism; official vs community/unverified classification; source link or code evidence; supported operations; installation changes; build compatibility; tested/not tested; rollback steps. If no appropriate mechanism exists under these restrictions, stop that dependent experiment and report the limitation instead of weakening this policy.

## Milestone 0 (P0) — unattended direct-to-combat-scenario launch

**Do this first.** Answer the user's question **by actually trying** where the Codex execution environment permits: *Can Sea Power be launched from the command line directly into a specified playable combat scenario, with a selected taskforce/side, without manually clicking through the menus?*

Investigation and experiment order:

1. Establish whether the Codex shell/runner has access to Windows GUI/game execution, Steam/platform prerequisites, a usable desktop/GPU and the installed game. **Distinguish environment limitations from game limitations.** Record the actual executable path and command invocation used (the path from Task 001 may differ).
2. Inspect relevant startup/initialization/mission-loading pathways (`GameInitializer`, `GameMain`, `MissionManager`, `SceneCreator`, mission content loading, native scripting/RPC and any *documented/verified* CLI flags). Investigate any existing first-party/development fast-start or startup-mission mechanism. **Do not invent Unity or Sea Power command-line switches.**
3. Try the least invasive repeatable mechanism first (verified native startup option, supported API, config or an existing mod capability). Test fresh process startup from a shell script/command, **not just reloading from a running session**. Select a deterministic local test mission, with a controllable unit and a defined start side.
4. A successful startup means **playable mission scene and relevant units are initialized and observable**, not merely that `Sea Power.exe` is running or the mission is visible in a menu. Measure and log the phases: process launched → adapter ready → mission loaded → selected taskforce found → units ready.
5. If no robust direct-start path exists, assess the smallest **opt-in development helper mod** that initiates loading at the correct lifecycle point and bypasses/auto-dismisses UI navigation and briefing. If load/hook compatibility allows, implement a *minimal* helper and test it. If testing is blocked, produce an actionable design and label success **unverified**.
6. Design a convenient shell-facing launcher (e.g. a PowerShell wrapper for Windows; exact path/name up to you) accepting an **explicit scenario identifier/path and, where the engine permits, selected side**. Provide stdout/stderr or log-file diagnostics and a machine-checkable ready/success/failure signal with a bounded timeout. If needed, support closing/restarting the game in a controlled way for repeated tests; do not kill unrelated existing player sessions.
7. Ensure ordinary gameplay is unchanged unless development fast-start was explicitly requested. Do not automatically enter a scenario whenever Sea Power starts. Handle invalid mission paths, multiple/competing game instances, absent prerequisites and loading failure clearly. Test two consecutive clean launches where possible.

**Important:** GUI click automation may be documented as a temporary comparison/fallback, but **does not satisfy** the direct-start requirement by itself. Prefer a native loading hook over hard-coded screen coordinates or menu timing. A helper mod is development infrastructure, separate from the operational naval API, and should be small enough to reuse later in integration tests.

Document the outcome in `Docs/AUTOMATED_LAUNCH.md`: exact tested invocations, prerequisites, validation signals, observed startup timings (if measured), limitations, lifecycle hooks, and whether native startup or a helper is required. Include an explicit result: **works / blocked by environment / requires helper / implemented helper but unverified / not supported**, with supporting evidence.

## Milestone 1 (P0) — safe minimal runtime loading and lifecycle

Choose and validate a safe integration strategy for the installed version: first test documented first-party scripting/mod interfaces and native RPC possibilities; only then evaluate a community loader/hook if needed, subject to the user-approval and binary-integrity guardrail above. Instrument a minimal C# logger that reports:

- game build/assembly identity, mod load/unload, thread ID and startup mechanism;
- mission/session initialization, taskforce selection, scene unload/reload and reference invalidation;
- candidate main-thread callback/safe points while running and while paused;
- whether fixed/ECS jobs are complete for the *actual fields* read at those points.

Use **immutable plain-data copies** of any observed game state. Never read native Unity/ECS/`ObjectBase`/`Vehicle` instances off the authorized thread; do not block the game thread on logging, IPC or other I/O. The adapter needs a command mailbox and a paused-mode path that does not require fixed ticks to advance, but a full IPC transport is not needed yet (a local scripted diagnostic command interface suffices).

If no proven coherent snapshot boundary exists, document per-field timestamps/age or narrow the first data slice rather than asserting atomic coherence.

## Milestone 2 (P0) — correct faction-specific observation

In a disposable test mission, observe the selected taskforce's own units and contact table, first with one undetected opponent and then after a controlled detection. Test **player and AI taskforces** if runtime access permits. At minimum:

- show that hidden enemy entities do not appear in observable contact output;
- log only whitelisted estimate fields from the selected-side `PlottingTable` (nullable estimate, age/source/uncertainty where available);
- verify that raw enemy UID, `BaseObject`, real position, velocity, name, destruction truth and global registry are **not exported** through the observational output;
- check lost/dormant tracks or mark the experiment untested;
- document observer/source semantics, including unknown allied sharing. Do not replace a missing estimate with the authoritative enemy position.

For debugging, any omniscient truth inspection must be in a separately named **privileged test-only diagnostic channel**, never mixed with the prospective public observation shape or model training trace.

## Milestone 3 (P0) — external authority over an AI unit (feasibility-critical)

Start with a benign **player-controlled** unit to verify waypoint insertion/validation and observe native execution. Then exercise **one AI-controlled** unit in a disposable mission:

1. Issue one verified native high-level waypoint through the minimal test harness; avoid full-route replacement and engagement initially.
2. Observe command acceptance, task insertion, actual movement, route continuation and removal/completion distinctly over multiple decision intervals.
3. Record whether native strategic/taskforce AI overwrites, inserts ahead of, cancels, or resumes the order; identify the exact responsible state machine/behavior tree/API when possible.
4. Investigate the **smallest** selective method for suppressing conflicting *strategic/mission* decisions while retaining native navigation, sensors, weapons automation and emergency defensive behavior.
5. Do **not** indiscriminately disable native AI, `HasControl`, all behavior trees, defense subsystems or global AI switches as an expedient solution. If narrow control ownership remains unsolved, present the attempted alternatives, exact blocker and minimal next experiment; do not claim success.

**Core success criterion:** an AI unit can execute and retain an external high-level order long enough to demonstrate externally driven behavior **without losing native emergency defense**. If only player-unit control is proven, clearly label the goal **partially achieved**.

## Milestone 4 (P0) — protect native urgent automation

For at least one aircraft with a reproducible incoming-missile condition:

- capture `AdvancedMissileEvasion` entry, current typed state, state-change time, impact on speed/altitude/steering and exit;
- compare pre/during/post current route/waypoint and whether native execution actually resumes; do not equate partial control restoration with full order resumption;
- separately exercise a deliberately interfering movement/cancel command **in a disposable run** to verify the reported `Aircraft.CeaseFire` cancellation path;
- implement a minimal dispatch-time **busy/protected-state guard** that rejects high-level commands which would interfere during evasion; recheck immediately on the authorized game thread, including possible formation propagation;
- demonstrate that a blocked command does not cancel the defense, and logs a clear rejection reason; do not automatically replay stale rejected commands after the threat passes;
- record entry/exit as short-lived *observer-safe* incidents if practical; prove at least that an event cannot disappear merely because the next slow observation happens after evasion.

Extend surface/submarine defensive cases only if inexpensive. Otherwise record them as follow-up tasks; do not incorrectly claim the aircraft guard protects every unit family.

## Milestone 5 (P0) — pause ownership and dispatch during pause

Probe `GameTime.Pause()`/`UnPause()` and actual player/menu time-control paths. Check the distinction among mission time, mission-session time, fixed time/tick and wall time. Test:

- a normal adapter-initiated pause/resume from an unpaused mission;
- an existing player/menu pause **before** the adapter requests a pause;
- a player/menu pause intent **during** an adapter-owned pause (including repeat/same-value pause requests, where observable);
- a new mission/unload while the adapter believes it owns a pause;
- acquiring a stable observation and **dispatching a benign native command while paused**, without unintentionally calling `FixedUpdate`, stepping the world or forcing launch preparations.

The helper/runtime controller must release only its still-valid pause ownership; never resume a pause now owned by the player. If ownership cannot be established with the native API, do not offer a deceptively safe pause release; document the required interception/state mechanism instead.

No requirement to implement all three production decision schedulers in this task. Report whether synchronous, hybrid and full-async modes remain feasible and whether time deadlines can only be detected approximately.

## Milestone 6 (P1) — lifecycle, incident readback and practical operation

Where runtime progress permits, exercise mission restart/unload, ensure obsolete references and queued commands are invalidated, and check the observer-specific incident capture design. A minimal bounded in-memory incident buffer with **two independent read cursors** is useful, but a full generic event service is not required. If not implemented, report evidence and proposed follow-up tests.

Capture useful operational logs including selected side, opaque observer IDs, accepted/rejected commands, native busy state and timestamps. No logs may expose enemy authoritative truth in an ordinary observer stream. Explicitly label privileged diagnostics.

## Scope boundaries and test discipline

- All changes must be reversible. Keep any loader/bootstrapper and experimental game hooks narrowly scoped and documented. **Never modify original shipped game binaries or assemblies on disk.** The user must approve installation/activation of an unofficial loader/preloader before any live-game changes.
- If Codex cannot interact with the desktop, still inspect relevant startup and loading code, build available tooling, and document **what was actually executed** versus a proposed plan. Never present static inspection or compiling a DLL as evidence that a game runtime test passed.
- Favor a limited number of small, evidence-rich experiments over writing a large suite of speculative components. Tests must have clear input conditions, expected observable results, observed results and pass/fail/blocked status.
- Do not assume a native API may be called from a worker just because it is reachable from a managed type.
- Preserve native emergency behavior and selected-faction visibility throughout. Do not leak truth via privileged diagnostic outputs to clients or model-facing code.
- The project's production C++ code targets C++26, with internal repositories built through CPM.cmake and third-party dependencies through source-based vcpkg. **Task 002 is a targeted C#/test harness** and does not need a production C++ build system or general IPC.

## Required deliverables

1. **`Docs/SEAPOWER_RUNTIME_FINDINGS.md`** with a test/evidence matrix: `Question | Build/hash | Setup & exact steps | Observed result | Evidence (log/file/screenshot reference) | Confidence | Remaining risk`. For each milestone, mark **passed, failed, partially tested, blocked by environment, or not tested**, and distinguish static inspection from live-game tests.
2. **`Docs/AUTOMATED_LAUNCH.md`** describing verified (not guessed) startup method, exact repeatable invocation, scenario/side selection, readiness/error checks, and helper-mod design/implementation if needed. Treat unattended direct-to-scenario startup as its own explicit acceptance gate.
3. Minimal reversible source code and/or scripts for the validated runtime loader, developer scenario launcher and observation/order probe, placed in a clearly separated `Experiments/` or `Tools/` area as justified by the chosen mechanism. Do not commit compiled mod DLLs or proprietary game assets.
4. A **reproduction guide** showing prerequisites, setup/cleanup, how to run each experiment, and where to find outputs. Prefer automated assertions and a machine-readable test result summary if feasible.
5. A short **architecture delta** in the runtime findings: what Task 001 assumptions are now confirmed/contradicted and what should change in `Docs/ARCHITECTURE.md`. Avoid silently modifying settled public contracts. Proposed future work should be ranked by risk.

## Acceptance gates

Treat each gate independently—do not claim overall success just because a process starts or a mod compiles:

- **Gate A — Automated launch:** One command starts a chosen combat scenario, without main-menu/mission/briefing navigation, and signals *mission playable + selected side/units ready*. If impossible to execute in the current environment, mark **blocked** and provide tested alternatives plus the smallest helper-mod plan/source feasible.
- **Gate B — Runtime hook safety:** A C# probe loads/unloads, reports consistent lifecycle/thread details, and does not perform out-of-thread Unity/ECS reads or block simulation on I/O.
- **Gate C — Observation security:** One hidden opposing unit is not leaked before detection and the selected-side contact is emitted without authoritative ID/position/velocity.
- **Gate D — AI command durability:** An actual AI-controlled unit follows an external waypoint for a meaningful observation period without native AI immediately replacing it; native emergency behavior remains enabled. Report failure plainly if this cannot be established.
- **Gate E — Native defense:** A protected command is rejected during the aircraft missile-evasion state without cancelling evasion.
- **Gate F — Pausing:** The adapter does not unpause a player-owned pause, and a benign order can be dispatched during pause without advancing simulation time unexpectedly.

Do not block submission of the findings when one gate cannot be run; record the precise blocker, supporting diagnostics and next smallest experiment. No fabricated measurements, invented options or unexecuted success claims.

## Suggested execution order and limits

Work in order: **A → B → C → D → E → F**, with Milestone 6 only if capacity permits. Gate A and B can share the same minimal startup/loader experiment. **After Gate A, record the exact result and commit or checkpoint the small prototype before moving to later gates; if direct start is blocked, do not spend the entire assignment building unrelated modules.** **Report an actionable partial result even if the game cannot be launched from Codex's execution environment.** Avoid building general infrastructure or repeated broad codebase searches once a targeted experiment will answer the question.

## Initial Codex instruction

> Read `AGENTS.md`, `Docs/ARCHITECTURE.md`, and `Docs/SEAPOWER_FINDINGS.md`, then execute `Docs/002-seapower-runtime-validation.md`. First verify **officially supported modding/startup approaches**, preserving all original game binaries, then try to establish **unattended, direct-to-combat-scenario launch from the available shell**. If native startup cannot do this, assess and, where feasible, implement a minimal opt-in developer helper mod that does. Validate the game-thread integration, selected-faction observations, AI-unit command durability, protection of native missile evasion, and safe pause ownership using reversible experiments. Produce `Docs/AUTOMATED_LAUNCH.md` and `Docs/SEAPOWER_RUNTIME_FINDINGS.md` with actual evidence; do not start the full framework.
