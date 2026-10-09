# Task 003 — Air-facility operations: native behavior, responsibilities, and order lifecycle

**Status:** Completed — see `Docs/SEAPOWER_AIROPS_FINDINGS.md`.  **Priority:** Historical Task 003 investigation.  **Backend:** Sea Power 0.8.3-261001 (verify actual local build/hash before trusting discovered member names).

## Read first

- `AGENTS.md` — hard architecture, modding-safety and development constraints.
- `Docs/ARCHITECTURE.md` — accepted design and boundaries.
- `Docs/SEAPOWER_FINDINGS.md` — Task 001 static inspection (especially `FlightDeck`, `CoreService.LaunchAircraft`, `PendingLaunchTask.Launched`, RTB, native order queues and time semantics).
- `Docs/SEAPOWER_RUNTIME_FINDINGS.md` — Task 002 runtime results, validated loader/launch mechanism and *limitations*. Read `Docs/AUTOMATED_LAUNCH.md` and `Docs/TASK002_REPRODUCTION.md` if they are present in the working checkout; the reports alone do not reproduce the experiments without their referenced harness, fixtures and evidence.

## Goal

Determine how **Sea Power air-facility operations** actually work: for **ground airfields, aircraft carriers, and helicopter-capable ships**. Above all, identify **what the engine already does automatically** and **what a human player or native AI controller must explicitly initiate or manage**. Map verified observations and commands to the existing `World.Api`, catalog and `Orders.Api` infrastructure, without creating an `AirOperations.Api` or a separate order framework by default.

This is an **investigation with small, reversible runtime probes**, not an instruction to build the NavalCommandFramework gateway, client, full protobuf contracts, or an external AI. Produce an evidence-backed implementation plan grounded in the actual code and behavior, avoiding speculative features. Reuse Task 002's proven unattended scenario launcher to minimize manual UI interaction.

### Scope boundary — critical

The subject is aircraft **while hosted by an air facility**, including preparation, loadout, launch, recovery, storage and turnaround. **Already-deployed aircraft use the generic unit state and generic high-level orders** (navigation, engagements, RTB, sensors, etc.). Trace transitions across the facility/deployed boundary, but **do not build an AirOps system that micromanages airborne aircraft**.

Treat a `FlightDeck` as a *native Sea Power concept*, **not as a synonym for carrier operations**. Examine land airfields as first-class implementations; do not assume all facilities share the exact same native methods, queues or restrictions. Do not invent generic aircraft readiness, turnaround or servicing features absent from the game.

## Starting evidence — not yet a complete feature model

Task 001 found:

- `CoreService.LaunchAircraft(unitid, aircraftName, loadoutName, _id)` delegates to `FlightDeck.createLaunchTask`.
- `PendingLaunchTask.Launched` is part of the asynchronous launch-completion path.
- `Order.ReturnToBase` can create a flight-deck return task.
- Pause/resume and time-compression transitions can affect pending flight-deck launch preparations.

Task 002 demonstrated scoped unattended launch for local combat fixtures using the development helper and Anchor Chain/BepInEx, without changes to vendor binaries. However, it **did not validate AirOps**. Its AI-order experiment was performed with `AIPlayer.IsForcedToDisable == true`, so command durability against active strategic AI remains unknown. Snapshot coherence, pause ownership across actual player/menu paths, and selected-faction source/age semantics also remain incompletely established.

Do not treat method existence, a finished task, a successful service RPC, or UI button activation as proof of a physically completed launch/recovery. Likewise, do not infer player and AI workflows are identical merely because they reach a shared native task.

## Milestone 0 (P0) — establish runnable AirOps fixtures and discover the relevant code

1. Inspect both the game's **UI command paths** and **native AI tasking paths** for ground airfields, carriers and helicopter facilities. Identify the actual actors and lifecycle: facility `ObjectBase`, `FlightDeck`, hosted aircraft/inventory representations, loadout catalogs, launch tasks, landing/recovery tasks, refueling/rearming/repair or turnaround handlers *if present*. Inspect source-callers and task/event consumers, not just class and enum names.
2. Reuse the validated Task 002 `Start-Validation`/scenario helper and readiness checks. Find existing distributable/test scenarios containing at least **one land airbase and one carrier/shipborne flight facility**; add minimal, separate user-content fixtures only if necessary. A helicopter facility is P1 if not available in the first two fixtures. Do not alter vendor scenarios or binaries.
3. Record how each fixture chooses player/AI controlled side and facility, and verify that the chosen side actually owns or legitimately knows about it. Task 002 proved only scoped fixture/player-side direct launch; **do not claim arbitrary side switching or mission readiness is solved**.
4. Produce a native class/call-path map, including the exact game build/hash, source inspection method and confidence. If the existing loader or harness is unavailable, state the blocker and produce a static call-path map; do not claim runtime success.

**Gate A:** A reproducible unattended AirOps test fixture (preferably both land and ship) loads and yields verified facility identifiers, or the exact missing requirement is recorded.

## Milestone 1 (P0) — investigate facility state and inventories

For each facility type, trace and, where practical, observe:

- Distinguish aircraft *physically stored/hosted*, queued for preparation, queued for launch, on deck/runway, actively launching, airborne, recovering, servicing, and unavailable — **only where native states actually exist**. If the game has a less detailed model, state it precisely.
- How aircraft types and individual airframes are represented while hosted: counts versus individual objects, instantiated/despawned `ObjectBase`s, ID/name reuse, grouped squadrons/wings, loadout-specific variants, and transitions after launch/recovery.
- Observable readiness, remaining supplies, ammo/loadout stock, aircraft condition, fuel, service duration, queues, capacity, runway/deck availability, damage and restrictions. Distinguish implemented mechanics from content metadata or merely UI labels.
- Side visibility and permissions: information available to a friendly facility commander versus data from global registries/editor/scenario truth. A controlled facility may expose perfect own state in V1; **never export hidden enemy airfield inventory through the operational interface**.
- Identify which state comes from metadata (class/loadout definitions) and which is dynamic (ready aircraft, queue, preparation progress, failures). Never manufacture availability from the encyclopedia.

For every field, document authoritative producer, native representation, unit/meaning, freshness (especially during pause), side entitlement and any unknown/sentinel values.

**Gate B:** Evidence-backed state/inventory schema **sketch** for at least one ground airfield and one ship facility; all unsupported/untested fields explicitly marked. Do not yet lock the public schema.

## Milestone 2 (P0) — WHO DOES WHAT: player, AI, native automation

This is **the central deliverable**. For each relevant operation, trace the entry point and every major automatic follow-up. Investigate at minimum:

- Choosing a hosted aircraft/flight, aircraft type or loadout (and what is configurable vs fixed by scenario).
- Preparation/arming/refueling/repairing, movement to launch location, scheduling/queuing and launching.
- Cancelling, delaying, redirecting, or reprioritizing pending launches.
- Recovery/landing, facility selection, post-recovery storage/turnaround/relaunch.
- Ordinary RTB order initiated on a deployed aircraft **versus** facility-side recovery automation.
- Native AI's decision to initiate launches, choose loadouts, create patrols/strike groups, recover aircraft and manage readiness, **only where observed or proven by code**.

Create an explicit **responsibility matrix**, one row per operation and relevant facility type, with the columns:

`Operation | Facility types | Human player's explicit action | Native AI's explicit decision/action | Automatic native follow-up | Entry point/caller | Preconditions/resources | Observable progress/completion | Interruption/cancellation | Evidence/confidence`

Separate **human UI interaction** from **underlying command semantics**: a player's single click may schedule an extensive autonomous sequence. Separate **strategic AI planning** (whether/when to launch) from **tactical/unit automation** (how to execute the launch). Trace the real native AI callsites and configuration flags; never assume `Taskforce.IsAIControlled` alone proves an active planner.

For each operation classify the expected external decider's responsibility:

1. **Explicit high-level order required** (e.g., select/loadout/launch request, only if verified).
2. **Automatic once another order is issued** (should not be reimplemented externally).
3. **Fully automatic without an external decision** (observe only).
4. **Manual-only/UI-side mechanic** requiring further adapter investigation.
5. **Unknown, unsupported or unverified**.

Also state whether player and AI paths converge on the same backend execution code. Do not treat a no-op enum case or UI affordance as executable API support.

**Gate C:** A complete, evidence-backed *manual-vs-automatic responsibility map* for the operations found in the installed build, distinguishing player, active native AI and engine automation.

## Milestone 3 (P0) — observe and exercise the smallest complete launch lifecycle

Use a **single aircraft or smallest native launch group** and a harmless test scenario. Prefer doing one launch each from a ground airfield and a ship facility; if the second is blocked, document why.

1. Before launch, capture hosted-inventory/readiness, selected loadout, pending tasks and facility state.
2. Issue a command through the same validated **native high-level method** the game normally uses, not direct low-level motion/teleportation/spawning.
3. Observe accepted request, preparation or queue insertion, launch start, physical appearance/airborne transition and the resulting **ordinary deployed-unit identity/state**. Mark which states and callbacks reliably prove each transition.
4. Trace errors: invalid/unavailable aircraft, unsupported loadout, occupied deck/runway, capacity/resource restriction, duplicate/queued request and cancellation where supported. No fabricated generic error results.
5. Test side-controlled facility permissions and whether native AI can overwrite/compete with the requested launch. Because Task 002 had strategic AI forcibly disabled, do **not** claim active strategic-AI conflict resolution without creating a verified active-planner condition.
6. Record whether pause affects preparation and whether a launch request can be safely accepted while paused. Task 002 found time-resume side effects; never force `FixedUpdate` just to make the experiment pass.

Keep hooks/loggers bounded, side-filtered, and restricted to tested safe-thread boundaries. Avoid copying live Unity/ECS objects to worker threads. Recheck native state at command dispatch, including authority and protected/occupied states. Distinguish queued/accepted/executing/completed/failed; if the engine cannot disambiguate a stage, label it unknown.

**Gate D:** At least one *verified live* launch with attributable input, stage timestamps and observed airborne unit; land and ship runs distinguished if both are available. If unavailable, deliver call-path evidence and a precise blocker.

## Milestone 4 (P1) — recovery, turnaround, identity and native AI

Investigate a recoverable sortie if the native state and test setup allow it:

- Initiate RTB using the generic deployed-aircraft orders; observe what the aircraft controller does automatically and when facility recovery begins.
- Observe queue/approach/landing/despawn or storage transitions, service/rearming/refueling and subsequent availability. Verify whether the process is modeled, instant or absent.
- Map aircraft identity across hosted → airborne → recovered: native IDs, type/loadout, loss/damage, squadron/unit relationships and possible object replacement. Public IDs must not promise persistence unless supported.
- Compare land/ship/helicopter recovery differences, deck capacity/damage restrictions, and pause/time-compression behavior where feasible.
- Probe native AI control decisions *only where the strategic planner is verified active*. Determine minimal possible external ownership of facility-level orders without breaking native launch/recovery automation or unit emergency behaviors.

**Gate E:** A trace of recovery and (if modeled) turnaround, or a clearly scoped explanation of why live validation was not possible and which native methods support further work.

## Milestone 5 (P1) — mapping to the current APIs, not creating new ones

Do **not** add `AirOperations.Api` by default. Keep one generic `Orders.Api` for **all high-level orders** with shared submission/validation/receipt and appropriate native lifecycle; specialize parameters and handlers as needed. Work out how facility-targeted orders coexist with unit-targeted orders, including hosted-aircraft selection. Use existing logical domains:

- **World.Api** — friendly facilities and hosted aircraft/inventory/queues/preparation/status, including dynamic facility-side operations.
- **Catalog.Api / catalog portion of the world model** — aircraft/loadout/facility capability definitions, only if verified.
- **Orders.Api** — preparation/loadout/launch/cancel orders that really exist, their applicability and observed lifecycle; deployed-aircraft RTB/navigation/engagement remain ordinary orders.
- **Events.Api / incident history** — observable milestones (e.g., preparation/launch/recovery) that occur between periodic decisions; independent per-consumer read cursors and retention gaps are future implementation requirements, not an already proven service.

These are conceptual responsibilities; do not insist on exactly one Git repository per row, create separate air-domain protocols or predefine `AirOperations` class hierarchies. A common order lifecycle must allow asynchronous, multi-stage operations and status `unknown` when the game lacks proof.

Document how external tasking or intelligence about aircraft readiness remains separate from verified simulation observation. Native AI's mission tasking is not automatically an external context source.

**Gate F:** A proposed minimal list of facility observation fields and verified *high-level* order families with source members, plus a list of schema questions to defer. **No production implementation required.**

## Optional adjacent follow-up (P2, only if efficient)

If useful, perform *one* narrowly defined Task 002 follow-up directly related to AirOps: actual player/menu pause-intent interception during preparation, clean read boundaries for flight-deck caches, or a verified active-strategic-AI facility decision test. Keep unrelated unfinished Task 002 issues (generic track fusion, multiple-subscriber event service, C++ gateway hosting, UI, client launcher, all-three-mode scheduler) **out of Task 003**; record them for later tasks.

## Safety and test discipline

- No changes to original Sea Power/Unity binaries or vendor-managed assemblies. Static decompilation is for understanding only. Use first-party content/editor capabilities where available; Anchor Chain/BepInEx remains an **unofficial, loader-managed, reversible** code-mod path. Existing Task 002 approval does not authorize arbitrary new installer changes. Document new changes, compatibility checks, exact rollback and request approval for new unmanaged installation/preloader changes.
- Use a disposable/test user-content scenario, not proprietary game source redistribution. Keep original vendor file-hash checks comparable to Task 002; no platform/Steam bypass, no hidden binary patching. Restore normal-launch defaults; opt into automation explicitly.
- Build on Task 002's functioning direct-to-scenario helper and tests instead of introducing a new GUI click automation, loader, general process controller, or command-line format without need.
- Use selected-faction observation whitelists. Truth-only diagnostics must remain isolated and clearly privileged; never copy hidden opponent inventory/positions into ordinary observations or training traces.
- All game access stays on validated game/Unity thread(s) with appropriate job completion; leave unsupported fields out. Do not run network, disk, blocking waits or model inference on the game thread.
- Distinguish **static code inspection**, **runtime traces**, **inferred native semantics**, **untested hypotheses** and **unsupported**. Do not imply complete global ECS snapshot safety from Task 002's narrow managed-data proof.
- Avoid unnecessarily large fixtures or repeated batch launches; use focused test cases and retained logs. Do not write the general framework or LLM controller in this task.

## Deliverables (under `Docs/`, capital D)

1. **`Docs/SEAPOWER_AIROPS_FINDINGS.md`** — deep dive, class/method call-path inventory, player-vs-AI-vs-automation responsibility matrix, facility-type differences, state/readiness model, supported native high-level order candidates, uncertainties and explicit risk ranking.
2. **`Docs/AIROPS_REPRODUCTION.md`** — exact build and launcher invocation, setup/cleanup, user-content fixture descriptions, switches, tested sides, log paths, expected/reported outcomes and how to repeat without menu navigation. Reuse links to Task 002 documents where applicable.
3. **`Docs/Evidence/Task003/`** — bounded probe logs, machine-readable run results and test conditions; identify which evidence establishes live behavior. Avoid proprietary assets/decompiled-source distribution.
4. **Small reversible diagnostic code/fixtures only if needed.** Modify existing Task 002 harness rather than building a new service. Document all changes and original-game integrity checks.
5. **Architecture delta section** — observations/query/command/lifecycle consequences and explicit proposals for `Docs/ARCHITECTURE.md`; do not silently freeze a separate AirOps domain or full command schema.
6. **Test matrix:** `Question | Game build/hash | Facility/scenario/side | Invocation / native methods | Expected | Observed | Evidence | Status/confidence | Remaining gap`. Mark each gate Passed, Partial, Failed, Blocked or Not tested separately.

## Completion criteria

Task 003 is complete when the **player / native-AI / native-automation responsibility split** has been mapped with direct evidence; at least one representative land facility and one carrier/ship facility have been investigated; one full launch has been exercised live or a precise test blocker has been demonstrated; and findings distinguish facility-side work from generic deployed-aircraft orders. A missing recovery cycle or helicopter fixture does not invalidate an otherwise complete investigation—record those as bounded follow-ups. No production API, gateway, UI or LLM is expected.

## Initial Codex instruction

> Read `AGENTS.md`, `Docs/ARCHITECTURE.md`, `Docs/SEAPOWER_FINDINGS.md`, and `Docs/SEAPOWER_RUNTIME_FINDINGS.md`. Execute `Docs/003-air-operations-investigation.md`. Reuse the proven Task 002 direct-to-scenario test helper. Investigate **ground airfields, carriers and helicopter-capable facilities** as implementations of Sea Power's native flight-deck system. Your principal deliverable is an evidence-backed matrix showing **exactly what a human player and native AI must order manually, versus what Sea Power executes automatically** through preparation, loadout selection, launch, recovery and turnaround. Run small reversible experiments where possible. Keep all facility-side commands within the existing generic `Orders.Api` concept, with deployed-aircraft control remaining generic unit orders. Do not modify vendor binaries or implement the full framework. Produce `Docs/SEAPOWER_AIROPS_FINDINGS.md`, `Docs/AIROPS_REPRODUCTION.md` and reproducible evidence.
