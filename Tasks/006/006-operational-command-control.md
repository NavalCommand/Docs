# Task 006 — Repeatable movement orders and first facility launch

**Status:** New implementation task, after completed Tasks 004 and 005.  
**Priority:** P0 repeatable ordinary unit navigation; P1 first facility launch using the same orders system.  
**Baseline:** The existing six local repositories and actual checked-in Task 004/005 code are the implementation baseline. Preserve them. This task **does not** redo the gateway, the Protobuf boundary, or the build-system refactor.

## 0. Gate 0 (P0) — Apply project code style across all hand-authored code

Before further feature implementation, read [`Docs/CODE_STYLE.md`](../../CODE_STYLE.md) and the unmodified owner-supplied [`Docs/CodeStyle.xml`](../../CodeStyle.xml). The XML explicitly specifies C++ style; `CODE_STYLE.md` adds mandatory cross-language readability standards. **These apply to every repository and every hand-authored language:** C++, C#, CMake, scripts, tests and protobuf source. Do not assume formatting was intentionally preserved when code appears minified.

- Preserve the XML verbatim in the project's `Docs/` directory and make style instructions available in each affected repository's `AGENTS.md` (link/copy the relevant rules rather than divergent rewrites).
- Never generate dense one-line methods with multiple statements, cryptic argument names or unnamed domain constants. Use clear intermediate values, spacing, deliberate line breaks and meaningful domain/unit-bearing identifiers. Control-flow braces must be explicit. **A formatter pass is not a substitute for readable code.**
- For C++, follow the explicit CLion export: tabs, 180-column margin, PascalCase types/functions/namespaces, camelCase locals and parameters, `m_PascalCase` member fields, uppercase-underscore constants/macros, sorted includes, required `if`/`for`/`while` braces. Do not infer unspecified C# settings from a C++ export; follow documented language-appropriate conventions plus the universal rules.
- Audit hand-authored files touched by this task (including its fixtures, build scripts and test harnesses). Refactor nearby or otherwise encountered minified code such as a multi-operation `Distance(double la,double lo,...)` body. Preserve compatibility, do not mass-reformat generated/third-party/vendor files, and do not rename public protobuf/wire fields as a cosmetic side effect.
- Run available formatter/linter checks with project rules where configured and inspect the result manually. Record the audited scope, representative before/after changes, tests executed and exceptions. Correctness and readability **both** gate completion. A readability-only change does not count as runtime evidence for Gate A–D.

## 1. Purpose and authoritative starting facts

Task 004 established an actual Sea Power → `SeaPower.Client` → in-process `Gateway.Grpc` → independently launched C++ CLI round trip. The single implemented `set_waypoint` path creates a native task and observes movement, but conservatively rejects another waypoint when a native selected task is already active. Task 005 reports that the six-repository `Build.CMake` migration and regressions preserved that behavior. Its reported tests do **not** prove repeatable navigation or a facility launch through the gateway.

**Important user-confirmed behavior:** Sea Power's player interface supports redirecting a unit that is *already moving*: ordinary **right-click replaces the queued waypoints**, while **Shift + right-click appends a waypoint**. The existing API's idle-only restriction is therefore a limitation of our implementation, **not a fundamental game restriction**. Treat the user-observed behavior as the intended semantics; inspect the installed game's actual UI dispatch and native task code to determine exactly how it is implemented on Sea Power **0.8.3-261001**. Do not assume the generic `SetWaypointTask` call by itself fully reproduces the UI's replace/append semantics.

Task 003 separately demonstrated native launch/recovery at land airfields, carriers and helicopter ships; Task 004 only provided read-only facility observations through the new gateway. **Do not count Task 003's direct native tests as proof that a facility launch works through our gateway.**

Read the *actual current checkout* and, where present, `Docs/TASK004_IMPLEMENTATION.md`, Task 005's `MODULE_STRUCTURE.md` / evidence, `Docs/SEAPOWER_AIROPS_FINDINGS.md`, `Docs/ARCHITECTURE.md`, and `Docs/LOCAL_SOURCES.md`. Reports describe tested behavior but do not supersede code or substitute for new runtime evidence.

## 2. Non-negotiable architectural boundaries

- Keep the five functional Git repositories: `Gateway.Api`, `Gateway.Grpc.Api`, `Gateway.Grpc`, `SeaPower.Client`, `SeaPower.Mod`; retain the sixth infrastructure-only `Build.CMake` repository. Do not create new speculative repositories.
- `Gateway.Api` is the **sole canonical Protobuf payload schema**; `Gateway.Grpc.Api` imports those message definitions and owns only the external gRPC service contract. Preserve existing C#↔C++ wire compatibility and RPCs where possible. The operational semantics remain generic `World.Api` / `Orders.Api` concepts, not Sea Power or Protobuf domain objects.
- The game-facing C# provider and C++ gateway do **not** depend on each other's implementation. `SeaPower.Mod` owns their composition, C ABI bridge, configuration, lifetime and package. Do not add a parallel aviation order service or `AirOperations.Api`.
- Reuse Task 005's subdirectory-defined targets, shared `Build.CMake` + `NavalCommandAddDependency`, optional ignored `Build.Local.cmake`, vcpkg, C++26/MSVC `c++latest` policy, and configure-only `windows-msvc-debug` / `windows-msvc-release` naming. No CPM, duplicated build helpers or new build architecture work unless fixing a regression.
- Native Sea Power objects are accessed only on verified game threads. Immutable bounded messages cross worker/C ABI boundaries. The game thread must not wait on gRPC, disk, inference or worker joins. Maintain loopback-only default and existing development-loader safety.
- Own units and player-authorized own facilities only; preserve selected-faction track filtering, opaque session identities, and no enemy truth leaks. Revalidate membership, order eligibility and resources **at actual game-thread dispatch**, not only when advertising the order.
- Preserve native navigation and **protective automation** (including typed `AdvancedMissileEvasion`); never implement route replacement by indiscriminately clearing all tasks, calling `CeaseFire`, disabling native AI globally, or cancelling an emergency state. No broad formation propagation without explicit proof.

## 3. Gate A (P0) — Trace the actual player movement-command path

Investigate the game's native path from the player **right-click** and **Shift + right-click** actions through UI handlers/viewmodels to task mutation. Inspect the installed assembly statically and use a small reversible runtime probe if necessary.

Record, with build/hash and relevant native callsites:

1. How the UI distinguishes replace from append, including keyboard-state handling.
2. Which task(s), waypoint list(s), user-root selection(s) and queue operations are changed; whether *replacing* means replacing all queued destinations, only a selected route segment, or some other exact native behavior.
3. How the UI handles an **already-moving isolated friendly unit**, an existing waypoint task, a non-navigation selected task, and, separately, formations where feasible.
4. How native protection or control/ownership checks are applied, and whether the UI delegates to engine automation that the external adapter must preserve.
5. Which native state, cached observations, task/waypoint members and callbacks can provide reliable evidence that the intended route was replaced or extended.

**Output:** `Docs/TASK006_UI_ORDER_PATH.md` with exact source/member references and an evidence-labelled equivalence mapping between native UI behavior and the new external orders. Do not claim a live GUI click was performed if only code paths/native equivalent behavior were tested.

**Gate A passes** when the two operations can be specified from real game behavior, not guesses based on names such as `SetWaypointTask` or `AddTask`.

## 4. Gate B (P0) — Implement reusable waypoint replacement and append

Extend the **existing** generic `Orders.Api` order path; no second dispatcher or custom route-control subsystem.

Expose two **explicit, unambiguous movement intents** through the currently implemented waypoint order family:

- **Replace route / ordinary RMB:** Redirect an already-moving unit by applying the game's ordinary right-click movement semantics. Existing queued waypoints are replaced as the player UI would replace them; do not leave stale waypoints pending or silently accumulate a second route.
- **Append waypoint / Shift+RMB:** Add a waypoint **after** the unit's existing planned route using the game's Shift+right-click semantics, without discarding previously queued destinations.

Choose the smallest compatible wire representation: e.g. an operation enum/oneof on the existing waypoint payload or distinct typed variants within the same order family. **Do not introduce a second transport protocol.** If changing an existing Protobuf message, add compatible field numbers and document the behavior of requests that omit the new field; **never silently turn an old request into a destructive replacement**. Maintain existing request IDs, duplicate-request idempotency, session epoch checks, status ledger and reconnect behavior. The unchanged legacy request path must remain well-defined.

Implement the actual native UI-equivalent mutation where validated. Replacing a navigational route is **not** permission to delete unrelated task types. If the current task cannot be safely changed, return a machine-readable rejection with no partial route mutation. Avoid UI-only dependencies or synthetic keypresses in the production adapter; invoke validated native semantics on the main thread.

Extend discoverability per unit to distinguish *supported* from *currently executable* operations and, when meaningful, the applicability of **replace vs append**. An already-moving unit must not be marked unavailable solely because it has a selected navigation task. A unit in a verified protected native state must not become eligible just because route replacement is implemented. Unknown restrictions remain conservative and report reasons.

Do not mark the command completed upon network receipt or task insertion. Preserve distinct `received` / game-thread accepted-or-rejected / route-mutated / movement-observed (and verified arrival only if genuinely observed) outcomes. Ensure status and observation values make route intent/results inspectable without exporting unsafe native handles.

**Gate B — mandatory live scenarios (one player-owned isolated moving surface unit unless a fixture proves another platform better):**

| Scenario | Expected evidence |
|---|---|
| Existing Task 004 first-waypoint smoke | Still passes, without changing its established request meaning |
| Submit waypoint A; after movement starts, **replace** with B | B becomes current intended route, prior queued destinations are removed as native UI semantics require; later movement/task evidence supports the change |
| Submit waypoint A, then **append** B while A is active | A stays in the route before B; native waypoint/task state confirms sequence, not merely an acknowledgement |
| Append C after an already appended B | Order remains A → B → C; no dropped/duplicated entries |
| Replace while several waypoints are queued | All and only the game-defined queued segment is replaced; no stale route remains |
| Duplicate request/reconnect while route active | Original receipt/status returned; no second native mutation |
| Invalid target, old session/handle, stale view, wrong-side unit | Rejected before mutation |
| Temporarily protected aircraft in `AdvancedMissileEvasion` | Route replace/append rejected without interrupting the native evasion state |
| Non-navigation task or unsupported formation context | Reject safely if a validated, non-disruptive equivalent is unavailable; document exact limits |

Trace the native route/task contents before and after the operation and observe displacement where meaningful. **Movement alone cannot distinguish replacement from append.** If natural waypoint completion can be observed in a bounded fixture, add that evidence; do not invent an arrival result otherwise.

## 5. Gate C (P1) — Implement one safe facility-targeted launch order

Finish the **previously partial** Task 004 AirOps gate using the **same** `Orders.Api`, service and status machinery. Implement **one** tested, player-owned facility launch request on a chosen supported fixture (land airfield, carrier or helicopter ship); allow normal native preparation and takeoff timing. Scope to a single verified aircraft/class/squadron/loadout combination first, without hardcoding this as the permanent protocol model.

Required behavior:

1. Report own-facility capability and **dynamic** eligibility accurately enough to expose the implemented launch order; preserve separation of *available unreserved inventory*, reserved/pending preparation, physically onboard, spawned/launching, airborne and cooldown counts where they can be verified. Do not invent per-hosted-airframe fuel, health, repair status or precise ETA.
2. At dispatch, validate current side ownership and handle, current session, facility operability, aircraft type and supported loadout/squadron, positive bounded count (initially one), actual unreserved stock, resource/ammunition availability, relevant native execution gates and conflicts. Validate again on the game thread adjacent to native mutation. Handle native `null`/exception/blocked outcomes without an unresolved external request masquerading as success.
3. Use native pending launch/crew/deck/taxi/takeoff automation. Do not manually spawn an aircraft or bypass preparation just to pass the test.
4. Distinguish **facility order accepted / preparation or pending / aircraft spawned / airborne**. The native `PendingLaunchTask.Launched` callback signals object spawning, not an airborne certificate. Correlate the accepted facility request with the spawned session-scoped regular unit ID where evidence permits; never assume identity continuity through future recovery.
5. **Immediately on creation of a normal own `ObjectBase`, expose the aircraft through generic `World.Api` and generic per-unit `Orders.Api` eligibility**, including when still grounded/taxiing. Its facility operation may continue running, but it needs no separate AirOps aircraft-control API. Native takeoff/protected states may temporarily block regular orders.
6. Keep player facilities isolated from unauthorised nonplayer AI actions; do not try to solve general nonplayer facility-AI ownership or disable useful native automation in this task.

**Gate C passes** only after one real external CLI → gRPC → gateway → C# provider → native facility launch reaches **a correlated spawned regular unit** visible to the ordinary client. If takeoff completes, also verify airborne as a distinct later stage. Provide failed/blocked resource and invalid-request tests with zero native side effects. If a necessary native gate cannot safely be validated, mark this milestone **Partial/Blocked** with precise evidence and a narrow follow-up; do not ship an unsafe shortcut.

Do not implement full AirOps turnaround, individual airframe persistence, CAP/AEW planning, automatic formation missions or generic launch cancellation here.

## 6. Gate D (P0) — Regressions, packaging, reproducibility and safety

Preserve and rerun Task 004–005 evidence-backed checks as applicable: canonical C#↔C++ Protobuf round-trip and gRPC imports; independent clean native/managed builds; mock-host/CLI and live selected-side observation; existing waypoint, invalid/duplicate/reconnect, protected aircraft, session reload; ABI/load behavior; and source/package/vendor-integrity checks. Test both newly implemented waypoint modes in the mock provider and real game. Include low-level state verification for append/replace, not just CLI output.

- Reuse the **current** six repositories and the existing `Build.CMake` local/Git resolver. Do not introduce CPM, copy build helper scripts, modify preset conventions, or publish repositories without permission.
- Keep the tested MSVC/vcpkg runtime-linkage/Unity dependency strategy; do not package proprietary game binaries. Respect opt-in loader/harness use, previous approval boundaries, backup/rollback and disabled normal-launch state.
- The native gateway remains unchanged in principle across mock executable and Sea Power DLL deployment; no game-specific branching in `Gateway.Grpc`.
- Add targeted C#/C++ unit/contract tests for route-mode parsing, current eligibility, route update safety, native-outcome correlation, schema compatibility and facility validation. Mock results must be distinguished from game evidence. Bound all queues, messages and receipts; surface errors rather than silent loss.
- Maintain an evidence-driven test matrix with **exact game build, own-side fixture, request IDs, mission timestamps, native before/after route or launch state, observed results, log paths, source revisions and hashes**. Report tests not executed or evidence missing.

### Required deliverables

- Updated, readable hand-authored code in the existing repos; separately committed changes, without pushing/publishing unless authorized.
- `Docs/CodeStyle.xml` preserved from the user-supplied export and `Docs/CODE_STYLE.md` followed; document code-style audit results and fixes in the implementation report.
- `Docs/TASK006_UI_ORDER_PATH.md` documenting actual UI right-click and Shift+right-click semantics.
- `Docs/TASK006_IMPLEMENTATION.md` with wire-contract changes, native dispatch and safety mechanisms, order eligibility and outcome semantics, launch/unit-ID correlation, and intentionally deferred work.
- `Docs/TASK006_REPRODUCTION.md` with clean build/configure, safe fixture startup, CLI commands for replace/append/launch and teardown/rollback.
- Bounded `Docs/Evidence/Task006/` including pass/fail results. Keep `Docs/` capitalization.
- A final **gate matrix**: `Gate | Goal | Actual test | Evidence | Pass/Partial/Fail/Blocked | Remaining risk`, separately reporting Gate A, Gate B, Gate C and Gate D.

### Explicit exclusions

No LLM calls, Qt GUI, full decision scheduler, production pause ownership, broad autonomous facility AI suppression, complete weapon/combat-order catalog, general formation routing, full AirOps recovery/turnaround, universal event streaming, or rewriting existing infrastructure without a demonstrated blocker.

**Completion requires passing the readability audit and documenting the scope alongside the functional gate results.**

**Completion criterion:** The external client can **replace** and **append** the planned route of an already-moving eligible own unit with UI-equivalent semantics, independently observe and verify the resulting route and execution, while protected native actions remain uninterrupted. The same generic order infrastructure can also initiate a narrowly validated launch from a player-owned air facility, recognize the spawned aircraft as an ordinary own unit, and report honest staged progress. Mark incomplete portions explicitly; never equate accepted commands with completed outcomes.
