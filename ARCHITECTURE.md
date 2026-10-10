# Architecture — Naval Simulation Observation and Control

**Authority:** this file defines the stable system architecture, including long-term extension points. It is not an implementation diary. Repository and build conventions are specified in [MODULE_STRUCTURE.md](MODULE_STRUCTURE.md), developer-local sources in [LOCAL_SOURCES.md](LOCAL_SOURCES.md), and code style in [CODE_STYLE.md](CODE_STYLE.md). Runtime investigations and dated implementation evidence are preserved separately under [Tasks/](Tasks/README.md).

## 1. Purpose and scope

Build a platform that makes an externally controlled naval force accessible through stable, structured, simulator-independent APIs. First use case: let an external process, especially an LLM-driven commander, observe and command units in **Sea Power** through high-level orders while preserving useful native game automation. Other consumers include deterministic scripts, a tactical viewer, a recorder/replayer, or another compatible simulator.

Separate:

- **Backend:** authoritative source of state and execution of orders (Sea Power, mock, future backend).
- **Operational domain:** faction-visible observations and high-level orders, independent of backend implementation.
- **Privileged runtime control:** optional pause/resume/execution management; not part of the operational world.
- **Client:** generic typed access, local state cache, and incident access; no assumption about decision-making algorithm.
- **Decision runtime:** interchangeable decision policies/model providers and a separate evaluation scheduler.
- **Presentation:** independent tactical visualization and annotations.
- **Record/replay:** observations, incident records, decisions, and execution outcomes for debugging and future AI experimentation.

### Module graph and deployment

**Repositories are separate from process boundaries.** Five independent functional Git repositories form the integration spine. A sixth `Build.CMake` repository owns shared build infrastructure and is not a runtime module:

| Repository | Language | Responsibility | Direct implementation dependencies |
|---|---|---|---|
| `SeaPower.Client` | C# | Thin Sea Power-facing implementation: faction observations, native command dispatch and engine-thread safety | `Gateway.Api` generated C# message bindings as needed; abstract operational contracts, **not** `Gateway.Grpc` |
| `Gateway.Api` | language-neutral `.proto` plus generated C# / C++ bindings and narrow C++ provider interfaces | **Canonical upstream Gateway-owned contract**: message types and provider/gateway interaction semantics | Domain semantics (`World.Api`, `Orders.Api`, etc.) are authoritative; no Sea Power, hosting mechanism or gRPC service implementation |
| `Gateway.Grpc` | C++ | Generic gateway implementation: receives provider observations/commands through `Gateway.Api`, serves external clients via `Gateway.Grpc.Api` | `Gateway.Api`, `Gateway.Grpc.Api`, domain contracts; **not** `SeaPower.Client` or `SeaPower.Mod` |
| `Gateway.Grpc.Api` | `.proto` | **External gRPC service definitions**; imports and reuses canonical messages from `Gateway.Api`, without redefining their payloads | `Gateway.Api` proto definitions |
| `SeaPower.Mod` | C# composition root with narrowly scoped C++ native host/shim build | Loader-facing mod wrapper, dependency injection, configuration, C ABI glue, gateway startup/shutdown, dependency builds and distributable packaging | Composes `SeaPower.Client` and `Gateway.Grpc`; uses API repositories; implementation-to-implementation knowledge lives **only here** |

The independently launched generic **external C++ client** consumes `Gateway.Grpc.Api` and the shared messages from `Gateway.Api`. A minimal CLI can be hosted in a separate application repo or a justified existing client module; it must not become a dependency of either gateway or Sea Power provider. Existing conceptual `World.Api`, `Orders.Api`, catalog and incidents contracts retain ownership of **operational meaning**, distinct from both Gateway communication interfaces.

```mermaid
flowchart TB
    subgraph SP["Sea Power process — embedded host, deployment choice"]
        MOD["SeaPower.Mod — composition root"]
        GAME["Sea Power / Unity"] --> PROVIDER["SeaPower.Client — C# provider"]
        PROVIDER <-->|"Gateway.Api canonical protobuf payloads; C ABI host transport"| GATEWAY["Gateway.Grpc — native C++ library"]
        MOD -."constructs / configures / builds".-> PROVIDER
        MOD -."constructs / configures / builds".-> GATEWAY
    end
    GATEWAY <-->|"Gateway.Grpc.Api service, importing Gateway.Api messages"| CLI["External C++ / Qt / decider clients"]
    GATEWAY -."upstream abstractions + canonical messages".-> API["Gateway.Api"]
    CLI -."generated gRPC client + shared messages".-> GRPCAPI["Gateway.Grpc.Api"]
    GRPCAPI -->|"imports"| API
```

**One canonical Protobuf message schema, two ways of exchanging messages.** `Gateway.Api` defines the sole canonical `.proto` declarations for operational message *representations* (world/units, tracks, orders, status, incidents, session). Generate **C# and C++ code from the exact same proto sources**; do not create separately maintained or transformed duplicate internal/external payload schemas. `Gateway.Grpc.Api` owns the external RPC service definitions (`service`/RPC methods and truly transport-specific request envelopes only), and **imports** the canonical messages. The internal C# ↔ C++ bridge exchanges those serialized Protobuf messages through a small C ABI; **it does not run gRPC**, and internal transport/lifecycle envelopes may differ. External clients communicate using gRPC with those same payload message types. Do not assume all internal operations and external RPCs share the same call or stream topology.

**Ownership and dependency direction:** `World.Api` / `Orders.Api` / other operational domain APIs determine semantics; `Gateway.Api` owns stable shared serialization and provider-facing communications; `Gateway.Grpc.Api` owns public RPC services. Neither `SeaPower.Client` nor `Gateway.Grpc` may unilaterally redefine `Gateway.Api`; contracts are changed deliberately and compatibly. Reuse the same common messages unless there is a documented reason a particular operation needs a transport-only envelope. Generated protobuf types are **wire/adapter types**, not substitutes for domain interfaces. Do not expose Unity/game objects or hidden authoritative enemy state through the shared schema.

**In-process deployment is not a compile-time dependency.** The default mod distribution composes the two independent implementations in `SeaPower.Mod`: its C# bootstrap/DI references the C# provider and loads a native C++ host/shim linked against the generic `Gateway.Grpc` library, while a narrow C ABI carries immutable bounded serialized messages. The gateway's implementation interfaces do **not** mention P/Invoke, C#, Sea Power or DLL hosting. An optional `Gateway.Host.Standalone` composition root supplies an IPC-facing `Gateway.Api` adapter and runs the **same unmodified `Gateway.Grpc` library** as an executable. Independent mock hosting must exercise the reusable gateway without the game or the mod; production standalone IPC is a separate extension. Worker threads must never dereference Unity/ECS game objects. Actual in-process hosting is an integration responsibility of the composition root, and must be regression-tested when the hosting, toolchain or binary interface changes.

Clients normally run as separate processes, including in the developer setup. The published mod may optionally bundle and auto-launch a **separate client executable** later, while independently launched local/remote clients remain supported. Client-launch policy, pause control and scenario startup remain deployment/runtime concerns, not operational naval data. A replay reader need not run through the live gateway.

## 2. Public API domains

The platform models the following operational domains. Their boundaries do **not** require one repository or gRPC service each.

| Domain | What it exposes | Update pattern |
|---|---|---|
| Class catalog | Unit/aircraft/vessel class descriptions; equipment; sensor and weapon definitions; limits and properties genuinely available in Sea Power | Mostly static, content-versioned |
| Order descriptions | Discoverable supported high-level order types, argument constraints, ongoing order state, availability/limitations | Definitions mostly static; unit applicability/state dynamic |
| Controlled forces | Actual observable state of controlled/friendly units: position, status, damage, sensors, weapons/ammo, active orders, and automation activities, to the extent exposed | Dynamic |
| Contacts/tracks | Tracks observable to the controlled faction and shared by allies, with last observations and uncertainty as applicable; never omniscient opposition truth | Dynamic |
| Environment | Depth, land, terrain, weather, thermal/acoustic data where supported, through bounded spatial queries | Spatial/on-demand; sometimes dynamic |
| Incidents | Retained, observable short-lived occurrences affecting the decision context | Append-only per observation/session with bounded retention |

**Do not bake in backend shortcuts as universal promises:** Sea Power may provide perfect current state of friendly units; An adapter may internally map one contact to one physical unit. The public model must not require either forever. Track IDs are opaque observer-visible identities; do not expose hidden authoritative enemy unit IDs.

### Catalog requirements

The catalog must be structured and machine-readable, useful to LLMs and deterministic algorithms, with stable schema and identifiers. Investigate which encyclopedia/unit-weapon specifications are accessible. Nominal weapon range and permitted target classes differ from actual engagement success or hit probability calculated from context: do not invent a universal probability where the engine has only dynamic calculations. Identify mod-content/version invalidation and give catalog snapshots a content revision/hash if useful.

### Orders

Prefer **native high-level order mechanisms**; do not design low-level movement micro-management by default. Keep **one `Orders.Api`** for unit-targeted and facility-targeted commands with shared submission, validation and execution reporting. Order-specific parameters and intermediate native stages remain specialized. Submission, acceptance, native task creation, physical execution and observed outcome are **distinct**; do not mistake the native flight-deck `Launched` callback (object spawned) for an airborne aircraft. Command validation must happen against current state near execution time.

**Dynamic order eligibility is a first-class observation:** for each currently visible friendly controllable unit (and appropriate facilities), expose the verified high-level order types supported by that entity and whether each is **currently executable**. Differentiate unsupported, temporarily blocked, available, and unknown states, with stable machine-readable reasons where verified. Advertise only order types whose semantics and validators are actually implemented. The adapter must revalidate at dispatch, especially during native missile evasion, taxi/takeoff, damage, unavailable resources and mission changes; a prior availability snapshot is not permission to execute later. A rejection is not a queued order and is never auto-retried. Generic clients need not understand Unity state-machine internals to use eligibility information.

### Perspective and observation

Server/API queries represent what the controlled side can legitimately observe. In particular, enemy tracks are not direct projections of full enemy unit state. Do not assume Sea Power's player UI perspective is automatically the right perspective for a controlled AI faction. Observed game versions contain taskforce-specific `PlottingTable` instances, including AI taskforces; implement observer-side filtering explicitly rather than trusting a global object registry. Native `Vehicle` contact wrappers still reach raw `BaseObject`, true velocity, unit identity and other hidden state: export only individually verified, faction-visible fields; keep opaque observer/session track IDs. The available hidden/detected-opponent experiments used separate fixtures; same-session contact lifecycle, measurement age/source and allied sharing still require verification.

### Environment queries

Avoid dumping global bathymetry or large data grids every evaluation. Favor local point queries, bounded area/tile/corridor queries, caching, and stated resolution. Only expose values the backend can actually supply. Extend the geographical schema only for backend-supported, side-effect-free queries. Some inspected `Environment` acoustic getters seed Unity's global RNG and cache values. An observation/query endpoint must not perturb simulation state; use validated safe cached data or defer those fields until a side-effect-free mechanism exists. Terrain depth also has sentinel/coverage cases; do not report sentinels as measured sea depth.

## 3. Operational interface versus runtime control

The **operational API** contains domain data and operational time, not game mechanics. It can be used with a mock backend, replay, or another data source. It must not expose scores, editor-only truth, game pause states, or speed multipliers as naval-world facts.

The **runtime control API** is independent, optional and privileged. A backend that permits managed execution may expose capabilities to pause/resume, inspect execution state, or regulate progression. The scheduler uses these interfaces; decision policies generally do not. Some backends might not support runtime control at all.

Use an operational timestamp plus a session/run identifier and an observation version/cursor where appropriate. Make units and timestamp conventions explicit. Snapshots returned for one decision should be internally consistent; independent client query frequencies must not imply inconsistent time bases. A narrow managed-data sample on the engine thread can pass checked fixed-job flags while global ECS/sensor coherence remains unproven. Do not describe all observations as one atomic epoch; carry per-field/sample timestamps and stale/unknown state until validated.

## 4. Time and decision scheduling

A default slow-decision scheduler invokes the decider at a configurable interval **N seconds of operational time**. The UI/cache may update much more often. Incidents do not force an LLM invocation.

- **Synchronous:** pause execution while a scheduled decision is being made; resume after its submission/result has been handled. Do not release a user-initiated pause inadvertently.
- **Hybrid:** normally run while the slow decider thinks; if the next due evaluation occurs before the previous one completes, pause until the decider catches up, then resume. Precise missed-cycle policy is not yet frozen.
- **Fully asynchronous:** never pause for slow decisions. Bound in-flight evaluations/queues and avoid submitting obsolete commands; exact missed-cycle and staleness policy remains open.

The scheduler is separate from both the decision policy and the client transport. Decisions must carry the observation version/time on which they were based, enabling stale-request checks. Do not conflate wall time with operational time or assume all backends can pause. **Do not promise exact tick-boundary stops:** observed deadlines may be crossed between frame updates. Paused-mode commands must be dispatchable without requiring `FixedUpdate`; ownership of the global native pause is not built into the game's `GameTime` API.

## 5. Native automation and control

**Central goal: reuse Sea Power's native semi-automatic order execution and urgent response behaviors.** For example, native missile evasion may interrupt a current aircraft order; another player order may override that behavior. A slow decider must be prevented from accidentally overriding protected urgent behavior and make the affected unit's activity/availability visible to the observer.

Do not prejudge how Sea Power encodes these states. Investigation must establish actual objects, order lifecycle, autonomy triggers, interruption/resumption, and whether independent native behavior can be disabled per unit/subsystem. Future fast external deterministic controllers must remain possible; avoid committing a speculative command-ownership hierarchy without evidence. Authority and instantaneous command availability should remain distinguishable concepts. On an inspected Sea Power build, advanced aircraft missile evasion is a priority state with temporary steering/speed/altitude control; a regular UI movement path can call `Aircraft.CeaseFire()` and terminate evasion, suppressing re-entry against the same missile. It restores selected controls, not necessarily the full previous state/order. The adapter requires a dispatch-time protected-state check, including relevant formation members; never assume automatic order resumption. **Durable command authority against an active native strategic planner remains unresolved**: An observed AI-side waypoint retained control only while the strategic `AIPlayer.IsForcedToDisable` flag was true. Do not generalize this to active AI planning.

## 6. Incident history (not LLM event callbacks)

Capture observable short-lived incidents that could be missed between snapshots. The slow decision policy reads accumulated history at its normal evaluation cadence. A fast native/deterministic controller may independently react to time-critical events.

The preferred semantics are **read + acknowledge**, effectively a clearable queue **per consumer**, not a single shared destructive pop. Include sequence/cursor, operational timestamp, origin/subject, and bounded retention; report dropped-history gaps. UI, recorder, and decision host must be able to consume independently. Specific incident taxonomy comes from evidence; avoid exposing hidden enemy events. Typed state transitions (`StateMachine.OnStateChanged`) and selected-side plotting table callbacks are possible capture points, but each hook needs version-specific thread/lifecycle verification; capture only observer-eligible data and finalize records at a validated safe point.

## 7. Injectable tasks, mission text, intelligence

Sea Power is single-player-focused: player-facing mission briefings should **not** be assumed available to AI-controlled factions. The decision context supports externally provided textual briefings, tasking, intelligence and constraints, initially from user-authored files if convenient. In the future, these may come from other services or from verified backend data.

Such inputs should be **sourced and distinguishable** from sensor observations. Do not insert user intelligence into the objective track state as if it were an observed measurement. Do not require a specific LLM prompt format in general-purpose context interfaces.

## 8. Client and decision-engine separation

The C++ generic client can expose a coherent observable world cache, catalog access, incident reader, and order submission. It must be suitable for deterministic programs as well as LLM engines. The tactical UI is a separately composable read-only observer. It may overlay explicit *decision annotations* (intent, hypotheses, brief human-readable rationale), but these annotations are not verified simulation facts and must not mutate the world model.

A decision policy is modular independently of the model runtime: single LLM, LLM assisted by deterministic algorithms, hierarchical multi-LLM, or fully deterministic policies. Model connectors may use local services or remote agents, with structured-output or tool-driven interaction. No runtime, provider, or topology is privileged in the generic client.

Represent repeatable, explicit high-level orders rather than blind desired-state reconciliation. Native player behavior distinguishes replacing a route from appending waypoints; the adapter must preserve this distinction and respect protected automatic actions. The external decider should generally issue mission/waypoint/engagement-level orders, not micromanage every simulation tick.

## 9. Transport and build decisions

- **`SeaPower.Client` (C#)** is an independently buildable game-facing provider library; its API dependency is `Gateway.Api` rather than a particular C++ gateway or hosting mechanism. Never make the game provider depend on `Gateway.Grpc`.
- **`Gateway.Grpc` (C++26)** is independently buildable/testable and simulator-agnostic. It depends on `Gateway.Api` provider abstractions and `Gateway.Grpc.Api` public service definitions. It knows neither the C# provider nor embedded/standalone host.
- **`SeaPower.Mod` (composition root)** owns BepInEx/Anchor Chain integration, C#/C++ binding, C ABI wrapper/shim, configuration, dependency injection, version negotiation, lifecycle and reproducible build/package orchestration. Its embedded native DLL contains/links gateway code via **composition**, not via a gateway-to-mod dependency.
- **`Gateway.Api` and `Gateway.Grpc.Api` are separate repositories**, but **not two duplicated Protobuf payload protocols**: write common `.proto` messages once in `Gateway.Api` and import them from gRPC services in `Gateway.Grpc.Api`. Generate both C# and C++ bindings from one pinned schema source; validate cross-language round trips. Domain interfaces retain source-of-truth semantics, and generated Protobuf classes remain wire models.
- **Standalone gateway mode** uses a different composition root/upstream IPC adapter while keeping `Gateway.Grpc` unmodified. The generic gateway must remain testable with an injected mock provider without the game. A production standalone IPC host is a separate composition concern.
- **Preferred deployment** is in-process gRPC DLL, served over loopback by default. A separately launched C++ CLI client connects over gRPC. Future packaged client auto-launch is optional, not a framework dependency; only explicitly configured executables may be launched, with owned-process lifecycle safeguards.
- **C++26** where toolchain support permits; central **Build.CMake / FetchContent** for internal repository dependencies and **vcpkg manifest/pinned source builds** for external packages. Root CMake files only initialize and compose. Target definitions follow src/mock/test/app/benchmark boundaries. Configure-only presets use platform-compiler-buildtype names, Ninja and o/${presetName}. Windows target/host dependencies use x64-windows-static with the matching static MSVC runtime (/MT Release, /MTd Debug). CMake/Build.CMake resolves canonical source repositories and orchestrates managed builds; ordinary SDK-style projects/MSBuild own C# codegen, NuGet and ProjectReference compilation. Generated repository-local IDE context and all build outputs follow [MANAGED_BUILD.md](MANAGED_BUILD.md). Ignored top-level Build.Local.cmake registrations provide direct/transitive local checkout substitution before declaration; Build.CMake bootstrap may use the standard FetchContent source override. No committed workspace substitutions or duplicated CMake policy. Separate builds and mocks remain mandatory; repositories stay local until explicitly authorized for publication.
- Favor pull queries and independent query cadences; optional streaming may follow. Cache static catalogs, poll changing observations as needed, and allow on-demand environment queries as verified. A narrow thread-safe internal byte/queue transport is not itself a second RPC protocol.

## 10. Recording, replay and testing

Record what the controlled side **actually observed**, including operational timestamps, catalog revisions, relevant incidents, issued orders/results, and optional decider diagnostics. Avoid leaking authoritative hidden truth into training/evaluation traces meant to approximate operational perception.

A trace reader should satisfy the same observation-facing contracts as the live client where practical. Replaying an observation trace is distinct from deterministically rerunning combat. A small mock backend should support contract tests without requiring Sea Power to run. Test wrong-side information leaks, native-automation command rejection, stale decisions, per-consumer incident consumption and synchronization behavior.

## Sea Power integration: officially supported first, no binary edits

Use officially documented Sea Power mechanisms (scenario/user content, Workshop, editor, and verified scripting/startup extension points) wherever suitable. Official support for user content does **not** demonstrate that arbitrary C# integration is officially supported. For code hooks beyond such interfaces, evaluate community-standard loaders such as [Anchor Chain](https://github.com/SeaPower-Modders/AnchorChain) / BepInEx as *unofficial* candidates, subject to explicit compatibility verification and user approval before deployment.

The Sea Power integration must never patch, overwrite, replace, or reassemble the game's original installed executables or managed assemblies. Static decompilation for investigation is permitted; original binaries must remain intact. Reversible, loader-managed in-memory hooks can be considered only after supported interfaces prove insufficient and the user approves installing the necessary third-party loader. Keep its footprint opt-in, test-only until validated, and document rollback. If binary modification would be necessary, report the limitation instead of implementing it. Follow the project safety rules in `AGENTS.md`.

## 11. Repeatable direct-to-scenario startup (development infrastructure)

**Essential for automated/Codex-assisted debugging:** a developer must be able to launch Sea Power into a chosen, reproducible combat scenario with a single command and without manually traversing the main menu, scenario chooser, or briefing UI. Prefer a verified native launch argument, startup API, or scripting mechanism. If unavailable, build a small **opt-in development-only startup helper mod** after validating the loader and lifecycle. A helper must select an explicit local test scenario/side, avoid overriding normal launches by default, expose readiness/error signals and logs, and reset cleanly on shutdown/relaunch. Do not infer that Steam/Unity accepts a mission-path argument without testing it; GUI click automation does not meet the primary requirement. Keep this feature outside the simulator-independent operational API. Keep this separate from the operational protocol and verify each development launcher against its target game version.

## Managed composition versus development tooling

`SeaPower.Mod` owns the managed composition/runtime of the Sea Power adapter and generic gateway. Its **production** managed source is grouped under `SeaPower.Mod/managed/runtime/`; its separately built **development harness** lives under `SeaPower.Mod/managed/harness/`. The native C++ host remains under `src/`, managed CMake composition under `managed/`, actual native applications under `app/` where applicable, and thin PowerShell wrappers/packaging/test entry points under `scripts/`. These are source-ownership boundaries, not new functional Git repositories or public API domains.

- **Production runtime:** a thin, maintained C# integration component that loads/configures the native bridge, composes the provider and gateway, samples observations and dispatches orders on verified permitted game threads, and transfers immutable bounded messages to and from I/O workers. Keep game-specific observation/order implementation in `SeaPower.Client`, generic gRPC networking in `Gateway.Grpc`, and canonical message definitions in `Gateway.Api`. The runtime is not an inference client and must not implement tactical policy.
- **Independent bootstrap:** approved startup/loading and activation work without the development harness. Use stable identifiers and a validated versioned runtime configuration, respecting the project's disabled-by-default and loopback-safe defaults; select the actual permitted player side, not a hard-coded test-side alias. The game thread must never wait on worker joins, disk I/O, gRPC or inference; cleanup must be idempotent and honest about unverified game-exit semantics.
- **Development harness:** optional separately packaged C# tooling for deterministic scenario startup, readiness and error receipts, diagnostics, selected development lifecycle tests and reversible launcher/loader state. It may observe/exercise the independently enabled production runtime through its normal external transport; it must never load, reflection-attach or enable the production plugin. The production runtime may not depend on it. The harness must be usable on its own with the mod disabled.
- **Experiments:** time-bounded, explicitly opt-in task fixtures that may use the stable harness but do not extend its core merely for a single probe. Keep disposable experiment source and run-specific evidence in the numbered task archive when reproducibility is needed. Promote common capabilities into the harness only with a stable design, test coverage and separate packaging.
- **Distribution boundary:** normal deployment packages contain production runtime and native gateway artifacts but no test launcher, task-labelled plugin, scenario fixture, experiment-dependent startup condition or evidence writer. A developer test distribution is separate, explicit, reversible and inactive by default.

Development infrastructure has no authority to change the operational protocol, expose privileged enemy truth, disable native protection globally, or mutate proprietary installed game binaries. The architectural lifecycle and engineering quality gates are further specified in [DEVELOPMENT.md](DEVELOPMENT.md).

## 12. Air-facility operations

Sea Power's `FlightDeck` is common to **ground airfields, carriers and helicopter-capable ships**. Live investigation has exercised launch and recovery at ground airfields, carriers and helicopter-capable ships; behavior remains game-version-specific. Keep this as a **facility feature**, not a second `AirOperations.Api`: observable inventory, reservations, preparation/launch/recovery queues and stock state belong in `World.Api`; capability/loadout descriptions in the catalog; all high-level facility requests in the **same `Orders.Api`**; observable transitions in incident/history facilities. The exact class/module split is incremental.

Distinguish **(a) higher-level strategic decisions, (b) independent native facility AI deciding CAP/AEW sorties, and (c) automatic flight-deck execution**. Native facility AI remained active while `AIPlayer.IsForcedToDisable=true`; disabling the strategic AI does not imply that native aviation tasking is under exclusive external ownership. External nonplayer command arbitration is therefore still open. Do not globally disable native flight-deck automation to gain control.

**Aircraft are aggregated before spawning**, by class/squadron and available inventory. A launch request reserves stock/resources, starts pending preparation, and eventually spawns an ordinary `ObjectBase`. **As soon as that spawned aircraft exists as a normal friendly game unit it enters the generic `World.Api` / `Orders.Api` flow, even if still grounded or taxiing.** Its dynamic order eligibility must correctly reflect ongoing native takeoff automation. AirOps continues to report the facility operation lifecycle but does not own a separate control interface for deployed/spawned aircraft. RTB is a regular deployed-unit order; recovery uses native facility automation. `Launched` marks object spawning; airborne status and physical recovery/turnaround are separate observable stages. Cooldown exists, but do not assume aggregate-stock restoration or a persistent airframe identity without runtime verification. Do not assume a persistent per-airframe `ObjectBase` or UID through multiple sorties.

**Inventory vocabulary matters:** unreserved aggregate stock, prepared/pending reservations, native onboard physical count, spawned/launching objects, airborne units and cooldown are different concepts. Individual hosted-airframe fuel/damage/readiness state is not established by the existing native source mapping; do not invent it. Backend validation in `createLaunchTask` is incomplete; authorize and validate on dispatch. Treat any unverified aircraft readiness data as unknown rather than inferred.

