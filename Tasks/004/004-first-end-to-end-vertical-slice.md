# Task 004 — First end-to-end implementation: Sea Power → native gRPC gateway → external C++ client

**Status:** Ready for Codex. **Type:** Implementation with automated tests and a narrowly scoped runtime proof. **Backend under test:** Sea Power 0.8.3-261001, Unity 6000.0.67f1; verify the actual local assembly SHA-256 against Task 001–003 rather than relying on the displayed build number. **Directory convention:** all task, architecture, report and reproduction documents belong under `Docs/` (capital D). Root `AGENTS.md` applies to every module.

## Required reading and fixed architectural decisions

Read `AGENTS.md`, `Docs/ARCHITECTURE.md`, `Docs/SEAPOWER_FINDINGS.md`, `Docs/SEAPOWER_RUNTIME_FINDINGS.md`, and `Docs/SEAPOWER_AIROPS_FINDINGS.md`. Use Task 002/003 reproduction docs, harness code, opt-in direct-to-scenario launcher, fixtures and evidence *if present in the actual working checkout*. The summary reports do not themselves contain the complete runnable harness. If the harness files are unavailable, identify the precise missing files, recover **only our previously written sources** where accessible, and continue with the off-game/mock implementation rather than claiming a live test.

Keep these decisions:

1. **One generic `Orders.Api`**: entity-targeted orders cover deployed units and aviation facilities alike; no `AirOperations.Api` or parallel order framework.
2. **Per-entity dynamic executable-order discovery**: distinguish *order type supported by this entity* from *order executable now*. The list must come from implemented validators and observed native restrictions, not aspirational enum values. Revalidate at dispatch. An unavailable order must not mutate game state or be automatically queued for later.
3. **A spawned aircraft is an ordinary unit immediately**, even if grounded/taxiing. It appears in the selected-side world model and uses the same order discovery/submission flow. Native takeoff or missile evasion may temporarily exclude commands. Inventory before spawning is a facility observation, not a simulated unit.
4. **Mandatory repository and responsibility boundaries**: `SeaPower.Client` (C# thin provider), `Gateway.Api` (canonical upstream Gateway contract and shared Protobuf messages), `Gateway.Grpc` (generic C++ implementation), `Gateway.Grpc.Api` (gRPC services importing the shared messages), and `SeaPower.Mod` (C# mod wrapper / sole composition root for embedded deployment) are **five separate Git repositories, independently buildable/testable**. `SeaPower.Client` and `Gateway.Grpc` must not reference/link/import each other's implementations. The wrapper composes, configures, builds and packages both modules; avoid a fused Sea Power-specific gateway.
5. **One canonical Protobuf message schema; not two protocols**: own all shared `WorldSnapshot`, `OrderRequest`, `OrderResult`, eligibility, incident and session *message types* in `Gateway.Api`, generate C# and C++ bindings from the same files. `Gateway.Grpc.Api` defines only the public RPC services and genuinely gRPC-specific envelopes and **imports** those canonical messages. The upstream C# ↔ C++ bridge uses the same serialized messages through an explicitly versioned, bounded C ABI—not gRPC. No duplicate internal/external payload schema, no code-generated `Gateway.Api` clones or copy/paste field parity maintenance.
6. **Domain meaning is still owned by domain `.Api` repositories**: `World.Api`, `Orders.Api` and other domain APIs define operational semantics independent of protobuf, C#, Unity and gRPC; Gateway communication APIs define the provider contract and wire representation, not naval semantics. Cross-language generated Protobuf types belong at adapter boundaries, not as a substitute for C++ domain objects.
7. **Hosting is a composition/deployment choice**: target an **in-process gateway DLL** from `SeaPower.Mod` now. `Gateway.Grpc` itself must know nothing about DLL loading, P/Invoke, the C ABI, or the host process. `SeaPower.Mod` owns the C# bootstrap and native C++ host/shim interop. Test `Gateway.Grpc` with a mock provider and standalone-style test executable **without modifying its implementation**; a real standalone IPC host can follow later. External C++ CLI connects independently over loopback gRPC.
8. **No binary modifications**: Use approved, reversible loader path (Anchor Chain/BepInEx) without editing vendor binaries. Reuse prior approved setup; obtain authorization for any new unmanaged install/preloader changes. Never silently activate new third-party components or circumvent Steam/platform checks. Keep all new game integration opt-in and restore the normal launch.
9. **No AI/GUI in Task 004**: no LLM integration, tactical Qt UI, comprehensive order catalog, full decision scheduler, production pause lease or bundled-client autostart. External/manual client process is the development default; future packaged auto-launch remains an architectural option.

## Primary goal / definition of done

From a **fresh one-command local combat-fixture launch**:

1. `SeaPower.Mod` builds/packages the independent `SeaPower.Client` and `Gateway.Grpc` implementations and loads both in the Sea Power process: C# provider plus native C++ composition/host DLL with an actual gRPC server. Host readiness must not block the Unity/game thread. `Gateway.Grpc` contains no Sea Power/managed-host references.
2. A separately run **C++ CLI client** connects to `127.0.0.1` and reads a **selected-faction filtered** view: stable session ID, own units, small own air-facility/inventory summary where safe, and a minimal contact subset that never reveals an undetected opponent.
3. For a selected own unit, the client obtains its **implemented supported order definitions and *currently executable* set**, including an explicit reason for a verified temporary block.
4. On explicit user/test request, the client submits a **high-level waypoint**; the adapter validates authority, session, state and current applicability on the main thread; native `SetWaypointTask(WaypointData)` executes, and the client observes movement plus the strongest **truthful** request/execution evidence supported by the engine.
5. Tests demonstrate that stale-session, invalid/wrong-side, unavailable and protected-state requests are rejected without native mutation, that external threads never dereference live Unity objects, and that reconnect/reload invalidates stale handles and queued commands.
6. The **same unmodified `Gateway.Grpc` implementation** runs in a small standalone-style test host with an injected mock `Gateway.Api` provider; the same gRPC client and public contracts work **without Sea Power or the mod**. Both hosts must build from the same source revision. Cross-language C# ↔ C++ serialization conformance is tested against shared `Gateway.Api` `.proto` files.

**Mandatory gates A, B, C and E must pass for an unqualified “core end-to-end implemented” claim.** Gate D (AirOps extension) is P1 and may be Partial if its live launch path cannot be validated safely. If a gate is blocked, deliver functioning independent work and a precise, reproducible blocker; do not substitute dummy success or a standalone gateway without disclosing the deviation.

## Milestone 0 — inspect existing harness and lay out the smallest build graph (P0)

- Inspect the *actual checked-out source* from Tasks 002–003 and `Docs/` before restructuring. Preserve functioning launch and instrumentation helpers. Do not assume the task reports alone are a full repo or create a parallel unrelated mod project.
- Identify which C# plugin entry/loader is already verified, exact Windows architecture and managed-runtime compatibility, and a safe point for main-thread sampling/dispatch while running and paused. Task 002 checked narrow managed/cache slices only: do not assume a globally coherent ECS snapshot.
- Establish source/build prerequisites. For **our C++ targets**, request **C++26**, check actual compiler support and fail conspicuously if essential features aren't supported. Use CPM.cmake for internal source modules and vcpkg manifest mode/pinned versions for external gRPC, protobuf and other third-party dependencies compiled from source. Third-party code may have its own supported language standard; do not silently downgrade our contract.
- Create **five independent local Git repositories** with narrow scopes, individual build/test instructions, explicit versions and no cycles: `SeaPower.Client`, `Gateway.Api`, `Gateway.Grpc`, `Gateway.Grpc.Api`, `SeaPower.Mod`. This requirement supersedes earlier Task 004 wording permitting combined physical repos. The composition-root repository is responsible for fetching pinned module revisions (CPM.cmake for internal C++ modules; a reproducible C# equivalent/fetch step for managed code), building them and assembling the distributable mod. **Do not** treat local folder co-location as a dependency mechanism. Do not require a remote GitHub organization or publish these repos without user request.
- Build dependency graph: `Gateway.Grpc.Api` → `Gateway.Api` (proto imports); `Gateway.Grpc` → `Gateway.Api` (provider-facing C++ abstractions/shared messages) and `Gateway.Grpc.Api` (gRPC service); `SeaPower.Client` → C# `Gateway.Api` bindings and native Sea Power interfaces; `SeaPower.Mod` → both implementations and API repos; external CLI → `Gateway.Grpc.Api` and imported `Gateway.Api`. Only `SeaPower.Mod` knows both implementation types. Domain APIs (`World.Api`, `Orders.Api`, etc.) own **semantics**, but create additional physical `.Api` repositories **only when a real reusable contract is justified**, not empty placeholders.
- `Gateway.Api` must declare its communication/host-neutral provider contracts and **canonical `.proto` payloads** in one source tree. `Gateway.Grpc.Api` must import them rather than redefining mirrored payloads. Require deterministic/reproducible C# and C++ protobuf generation and meaningful versioning/backward-compatibility strategy. Keep generated protobuf models at wire/adapter boundaries; let domain interfaces stay independent.
- Put the **C# managed plugin entry point, interop/host glue, configuration, dependency injection, build orchestration and packaging** in `SeaPower.Mod`. If native host implementation code is needed, keep it as a narrow private C++ target/subdirectory built by this composition root. Do not put Sea Power imports, `DllMain` lifecycle setup, host ABI exports or C# types into `Gateway.Grpc`.
- Build `SeaPower.Client` with a fake `Gateway.Api` sink/source and `Gateway.Grpc` with a mock `Gateway.Api` provider **before** integrating them, proving neither implementation relies on the other. Follow PiSubmarine-style interfaces, one type per file, test mocks and documented ownership. Validate toolchain, managed-runtime compatibility, startup and safe engine-thread sampling as already described.
- Do not automatically create a GitHub organization, push to a remote or publish releases. Local repositories/builds and reviewable commits are fine.

**Gate A — Independent module builds and embedded integration:** All five repositories build/validate independently where applicable, with no direct `SeaPower.Client`↔`Gateway.Grpc` implementation dependency. The `SeaPower.Mod` composition root builds/packages them and loads a native host DLL **inside Sea Power** via a validated managed-to-native route (e.g. P/Invoke); the host constructs the unmodified `Gateway.Grpc` implementation. Confirm ABI version/start/stop in the actual loader context and a separate mock-host executable using the **same gateway implementation**. If the native host cannot load, report the exact failure without silently changing the deployed architecture.

## Milestone 1 — one shared Protobuf schema, the upstream interface, and the public gRPC service (P0)

**Required contract layout:**

```text
Gateway.Api/                         # independent repository
  proto/navalcommand/gateway/v1/     # canonical payload messages
    world.proto
    orders.proto
    incidents.proto                  # minimal if used by this slice
    session.proto
  public/                             # C++ provider abstractions (if needed)
  test/                               # proto C#/C++ round-trip tests
Gateway.Grpc.Api/                    # independent repository
  proto/navalcommand/gateway/v1/
    service.proto                     # imports Gateway.Api payloads
Gateway.Grpc/                        # independent repository
  public/ src/ mock/ test/             # generic C++ gateway implementation
SeaPower.Client/                     # independent C# provider library
SeaPower.Mod/                        # independent mod composition root
  ...                                # loader, DI, host/shim, builds, packaging
```

Names/files are initial examples; keep one canonical source per shared payload message. `Gateway.Grpc.Api` should express public `service` RPCs only, plus any **genuinely RPC-specific envelopes** not already represented in the shared messages. Do not auto-generate a second independent protocol or invent a field-for-field parallel protobuf hierarchy. Protobuf `package`/namespace conventions and import paths must work identically for C# and C++. Generate bindings from pinned proto revisions through each repository build.

**C# ↔ C++ upstream communication:** `SeaPower.Client` creates/consumes canonical `Gateway.Api` protobuf message bytes. `SeaPower.Mod` provides the ABI wrapper, marshaling, memory/queue ownership and startup wiring that feeds a `Gateway.Api` provider abstraction implemented on the gateway side. The `Gateway.Grpc` library consumes these via its injected provider interface, unaware of whether the host uses C ABI callbacks, local IPC, or a mock. No gRPC server, gRPC-generated service stub or HTTP stack is required inside `SeaPower.Client`.

**External gRPC:** `Gateway.Grpc` implements the service declared in `Gateway.Grpc.Api`. The RPCs carry the **same canonical messages** for shared observations and orders; transport-specific request/subscribe/control wrappers may differ. Client uses the generated C++ gRPC service and canonical message types. Preserve semantic ownership in domain `.Api` contracts, not in transport-only conveniences.

**Schema conformance gate:** automatically exercise cross-language roundtrips (C# serialize → C++ parse and reverse), unknown-field preservation where supported, enum/optional/oneof behavior and stable ID/unit encoding. A small schema update test must fail on incompatible reuse of protobuf field numbers or on accidental duplicated message definitions. Neither private bridge ABI nor the external gRPC service may bypass the canonical payload schema.


Build a **real gRPC server in C++**, not a JSON/WebSocket placeholder, and a tiny C++ CLI client. Protocol/API should be implementation-driven but typed and simulatable.

Minimum surface, using the team's naming conventions rather than these names verbatim:

| Operation | Minimal behavior |
|---|---|
| Connect / health | Protocol/schema version, connection readiness, active session/observer identity when available; no privileged simulation truth |
| Get current view | Selected-side snapshot with session identifier, timestamp and independently stamped samples where they cannot be atomic; own units, safe track estimates, a narrow own air-facility aggregate if verified |
| Discover orders | Supported/implemented high-level order kinds and argument requirements for an entity; current executable/temporarily blocked/unknown decision with optional machine-readable reason |
| Submit order | Session-bound request with unique request ID, verified entity ID, one typed waypoint payload in V1; return accepted/rejected without pretending completion |
| Query request status | Stable request ID scoped to session; distinguish invalid/rejected, accepted/queued, native task started and a verified observed outcome; allow `unknown` where the game has no receipt |

- Maintain a stable observer/session-scoped mapping of **own unit** handles for the run, invalidated on unload/reload/ownership transfer; native `UniqueID` is internal. Enemy tracks use different opaque observer IDs. Track-to-object relationships are not 1:1 protocol invariants. Never relay `Vehicle.BaseObject`, real velocity, hidden UID, global registry or `CoreService` truth DTOs into the operational view.
- Domain types express explicit units (lat/lon degrees, speed knots or normalized agreed SI conversions with units indicated, altitude/depth meters) and optional/stale values. Avoid inconsistent native Unity scaling. Include schema/protocol version and bounded message sizes. Keep contact fields within the Task 002 whitelist (`DetectedPosition`, sourced uncertainty where units verified), and **omit** uncertain measurement-age or sensor-source claims. Never query environment getters known to mutate RNG.
- The native C ABI is owned and exported by **`SeaPower.Mod`’s native host/shim**, not `Gateway.Grpc`; it must be **small, stable and boring**: explicit initialize/shutdown, publish immutable serialized observation/status bytes, and nonblocking bounded command polling/transfer. Specify allocator ownership, buffer lifetimes, UTF-8, return statuses, ABI revision, explicit cancellation/shutdown and no exceptions through C linkage. Do not call game/Unity APIs from native gRPC threads. Do not create threads or invoke managed entry points from `DllMain`.
- Main-thread adapter reads from previously verified safe caches/slices; do not run blocking gRPC, network, disk logging or inference on the game thread. When paused, `Update` remains a candidate for processing the mailbox; do not force physics updates or advance simulation merely to execute a pending command.
- Network endpoint **loopback only by default**, explicit fixed/configurable port, connection failure and port-conflict handling. Never expose an unauthenticated control service on non-loopback interfaces or publicly distribute such defaults. Configure remote access/authentication as future work. Do not auto-start an arbitrary bundled client executable.
- Reconnect semantics: a new client obtains current run/side identity and a fresh snapshot; commands from old sessions cannot execute after unload. Include bounded queues/backpressure and explicit failure statuses when full. A crash/restart of native host must not strand an unsafe C# command queue.

**Gate B — shared contracts and real client connectivity:** C# and C++ generated bindings demonstrably round-trip the *same* `Gateway.Api` messages; `Gateway.Grpc.Api` imports those definitions; the external C++ CLI connects to the **actual in-game in-process gRPC server** and retrieves a nontrivial versioned selected-side snapshot. Hermetic/mocked tests exercise the exact same gateway library, gRPC service and CLI protocol without Sea Power dependencies.

## Milestone 2 — dynamic order eligibility and verified waypoint execution (P0)

### Eligibility contract

- Do **not** merely publish a static enum of all native Sea Power `Order.Type` values. Advertise only high-level commands implemented and validated for that entity by the V1 adapter. For now this can be just `SetWaypoint` for suitable units; other order kinds may be absent rather than guessed.
- Separate **supported but temporarily unavailable**, **currently executable**, **unsupported** and **unknown**. Return a stable reason code where verified (e.g. `protected_native_action`, `grounded_launch_sequence`, `uncontrollable`, `wrong_side`, `invalid_arguments`, `stale_session`), with untrusted explanatory text optional. These reason names are examples; only emit a reason backed by actual data for that entity.
- For spawned aircraft *still on the runway/deck*, include them in own-unit observations immediately upon native spawning. Native takeoff execution may block movement orders; this is a regular unit's dynamic eligibility, **not** a separate aviation control path. If the exact grounded restriction isn't verified for the chosen waypoint entry point, report `unknown`/conservatively reject rather than assuming a positive or negative capability.
- Recompute or revalidate eligibility atomically enough **at dispatch**, after re-resolving the selected-side object/session and formation-affected units where relevant. The only approved initial mutating order is a single ordinary waypoint/append operation with explicit semantics. Guard typed `AdvancedMissileEvasion` as verified in Tasks 001–002; avoid triggering `CeaseFire`, detaching formations or overwriting protected native tasks inadvertently. Never replay a rejected order automatically.

### Lifecycle evidence

- Record `request_received` → `accepted_or_rejected` → `native_task_created` where verified → `movement_observed` independently. Do not call `ReachedWaypoint/RemovedOrFinished` success without validated reason; native task creation is not reaching destination. `unknown` is allowed when output evidence cannot certify completion.
- Retain a bounded request-status ledger across external client disconnects **within a session** so a client can reconnect and query by request ID. Detect duplicate request IDs and make retries idempotent *within a session* (return original status; never execute twice). Report expired/unknown IDs; session reset invalidates old IDs. Status mutation occurs at actual game-thread dispatch; network receipt alone is not native acceptance.
- A short-lived **internal** incident buffer may back this ledger. If exposing incidents to multiple clients now, each needs independent cursors/acks and explicit overflow gaps. Do not implement a destructive global queue that silently loses another consumer's records. A complete incident taxonomy/replay engine is *not* required for Task 004.

**Gate C — live native waypoint:** CLI issues one deliberate order; server acknowledges request appropriately; native selected-side unit changes route and movement is observed; late/stale/wrong-side/busy commands are not executed. Verify a temporary unavailable example using native aircraft advanced evasion if the reusable Task 002 fixture is available; otherwise give a deterministic hermetic availability test and label live coverage incomplete.

## Milestone 3 — bounded AirOps use of the same architecture (P1, after Gates A–C)

Prove the architecture does not accidentally treat a facility operation as a separate command system. Do this **only after** the core waypoint flow is stable. Do not implement a comprehensive AirOps module.

**Minimum required P1 observation:** expose selected-side facility identity, capability to launch applicable class(es), small aggregate stock by class/squadron, reserved/pending count or named native stage where safely verifiable. Do not invent individual hosted airframe conditions, readiness fuel/health, exact ETA or stable per-airframe identities.

**Conditional P1 command:** if current fixture and source allow it safely, implement **one** facility-targeted `Prepare/Launch` order through the *same* `Orders.Api` and validation/receipt plumbing. Use one aircraft, one verified valid class/squadron/loadout, positive bounded count, resource/operability checks. Don't directly route unvalidated arguments into `createLaunchTask`/`CoreService` (Task 003 found incomplete native validation and unresolved promises). Track **request acceptance**, pending/ready progression, **object spawn** (`PendingLaunchTask.Launched`) and (if feasible) airborne outcome as *distinct* events/status. Test on one ground or ship facility first; don't require all three fixture types again.

Immediately after spawning, expose the aircraft as an ordinary own unit in the same `World.Api`. If still grounded, present truthful current eligibility and protected native takeoff behavior through the existing `Orders.Api`; do not add an AirOps controller for it. A spawned aircraft's session-scoped ID may be new; don't assert persistent individual identity through inventory/recovery.

**External ownership risk:** Native facility AI generated CAP/AEW requests while `AIPlayer` was disabled in Task 003. For this slice prefer a **player-controlled fixture** where such facility AI doesn't issue competing sorties. Do not claim externally commanded nonplayer facility ownership or disable all AI/deck automation; preserve this as a P0 future investigation. If a small selective-arbitration test is straightforward, record it separately without changing the common gateway contracts.

**Gate D — bounded AirOps demonstration:** Own facility status arrives via the same client/world model and, where safe, the same Orders API initiates one launch with a distinguishable **spawn** event; the spawned ordinary unit is visible before or at takeoff without an artificial AirOps-to-unit authority transfer. If conditional launch cannot be made safe, provide the verified read-only facility observation plus a precise blocker and mark Gate D **Partial**, not Passed.

## Milestone 4 — tests, packaging and reproducibility (P0 throughout)

Provide:

1. **C++ unit tests** (prefer gtest/gmock) for domain/wire mapping, current-order eligibility, status transitions, duplicate request IDs, lost/reconnected client, bounded queues, session changes and mock backend. Add C#↔C++ conformance tests using the exact `Gateway.Api` `.proto` files, import/codegen tests proving `Gateway.Grpc.Api` uses those messages and no duplicate payload definitions. No Unity dependencies in these tests.
2. **C# tests or safe probe checks** for adapter-side selected-side filtering, native argument validation, protected-action rejection and game-thread-only access; when engine-only pieces cannot be unit-tested without running Sea Power, provide constrained live probes and exact logs.
3. **Automated live smoke test** using the existing one-command direct-to-scenario launcher, with readiness/port/conflict/timeout detection, actual external CLI result, and a fixture-specific unit movement check. Every test records build hash, side, command ID, timestamps, expected/actual output and evidence path. An explicitly opted-in test may issue one real waypoint; a default manual CLI invocation must be read-only.
4. **Five independently buildable Git repositories** and a `SeaPower.Mod` orchestrated build: CMake presets, CPM.cmake/vcpkg manifests for internal/external C++ dependencies, reproducible C# and C++ Protobuf generation, pinned source revisions, CLI usage, artifact locations, portable versioned host DLL/managed-mod package and clean install/rollback. Prove the gateway library can link into both mod-native and mock-standalone host targets unchanged. Do not package proprietary game files. Verify vendor executable/managed hashes unchanged and normal launch remains inactive by default.
5. **Trace failure modes:** in-process native crash can terminate Sea Power; document limitation. Verify clean stop at mission unload and process exit *as far as actual engine behavior permits*. No blocking worker join inside game callbacks; treat unloading a running gRPC DLL carefully and document any process-lifetime native host constraint.
6. **Architecture/status documents:** preserve these *accepted design requirements* in `Docs/ARCHITECTURE.md` while separately recording which runtime claims were actually demonstrated; write `Docs/TASK004_IMPLEMENTATION.md` (all five repositories, dependency diagram, contract ownership, canonical proto import graph, native composition/C ABI ownership, actual wire surface, stages, tests and gaps), `Docs/TASK004_REPRODUCTION.md` (commands/paths/cleanup), and bounded evidence under `Docs/Evidence/Task004/`. Follow existing documentation conventions and `Docs/` capitalization.

**Gate E — reproducible five-repo distribution/debugging:** From clean independent builds of `SeaPower.Client`, `Gateway.Api`, `Gateway.Grpc`, `Gateway.Grpc.Api` and `SeaPower.Mod` (plus preapproved loader setup), build/run the gateway with a mock provider and external client; then build/package through the mod composition root and run the real unattended scenario + in-process gateway + CLI waypoint with no manual menu navigation. Gateway library source must remain unchanged between hosts. Ensure all deliverables are own-authored, reproducible and safe to remove. If prerequisites are absent, clearly identify what did run and what could not.

## Deliberately deferred

- LLM inference, prompts, agents, Qt tactical viewer, scenario-independent mission selection/side switching, synchronous/hybrid/async decision scheduler, production pause ownership, full-fidelity atomic world state, source-provenanced mature track fusion/uncertainty, catalog encyclopedia import, simulation replay, arbitrary local/remote client orchestration, packaged client auto-launch, advanced combat orders, full aviation turnaround/relaunch and general native AI-control suppression.
- Full open-network gRPC authentication, other operating systems, universal framework plugin loading, persistent hidden-unit truth analysis and speculative order types.
- If Task 004 reveals a need to change a previously accepted boundary, document competing options and smallest safe experiment; don't silently redesign or publish a new public API.

## Required final report format

Document a concise **gate matrix**:

`Gate | Requirement | Built files/modules | Actual test command | Evidence | Pass/Partial/Fail/Blocked | Known limitations`

Do not describe a compiled mock as an in-game success, native spawning as airborne completion, a game-side method return as order execution, or a previous standalone test as proof of the new gateway DLL. Give exact error conditions and revised steps when blocked. All runtime claims must be specific to the game build and fixture.

## Codex kickoff instruction

> Read `AGENTS.md`, `Docs/ARCHITECTURE.md`, `Docs/SEAPOWER_FINDINGS.md`, `Docs/SEAPOWER_RUNTIME_FINDINGS.md`, and `Docs/SEAPOWER_AIROPS_FINDINGS.md`. Execute `Docs/004-first-end-to-end-vertical-slice.md` as the first **implementation** task. First establish **five separate buildable Git repositories**: `SeaPower.Client` (C#), `Gateway.Api` (provider-facing API and single canonical Protobuf message schema), `Gateway.Grpc` (simulator-neutral C++ gateway), `Gateway.Grpc.Api` (gRPC service definitions importing `Gateway.Api` messages) and `SeaPower.Mod` (C# composition root and embedded native host/shim, dependency builds, configuration and packaging). The two implementation modules must be ignorant of each other; **only the composition root wires them**. Demonstrate C#↔C++ serialization of the same Protobuf payload definitions, independent mock-provider hosting of the same gateway library, then embedded Sea Power hosting and an external C++ CLI. The initial end-to-end proof is selected-side observation, dynamic per-unit order availability and a validated native waypoint with movement evidence. A single verified facility launch through **the same `Orders.Api`** is a secondary milestone; spawned grounded aircraft are ordinary units. Reuse Tasks 002–003 direct-to-scenario tests, preserve native automation, C++26, CPM.cmake, vcpkg and reversible loader hooks. Do not modify vendor binaries or implement LLM/Qt UI, runtime scheduler or bundled-client auto-launch. Supply tested code, five-repo build instructions and gate/evidence reports under `Docs/`.
