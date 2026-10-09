# Task 001 — Inspect Sea Power before implementing the API

**Status: completed (historical task specification).** See [`SEAPOWER_FINDINGS.md`](SEAPOWER_FINDINGS.md) for Codex's static findings. Original investigation performed before the repository path convention was corrected; `Docs/` is now canonical for both task specifications and documentation.

## Goal

Perform **evidence-based architectural reconnaissance** of the installed Sea Power game and its modding interfaces. Determine how to observe a selected faction's naval world, submit existing high-level orders, preserve native automation, and manage simulation time. The results will guide the first implementation of the simulator-independent API described in [`ARCHITECTURE.md`](../../ARCHITECTURE.md).

**This task is investigation and documentation, not the implementation phase.** Read repository `AGENTS.md` and `Docs/ARCHITECTURE.md` first. Do not generate speculative proto contracts, build the gRPC gateway, implement an LLM controller, or replace vanilla tactical AI during this task.

## Available information and boundaries

- The backend is Sea Power. It is expected to be a Unity/C# application, but verify versions and runtime/modding constraints before choosing tooling.
- Inspect game assemblies or source **only when accessible and permitted in the development environment**; examine relevant public mod sources if useful, but distinguish source-backed findings from assumptions and outdated third-party versions.
- Do not commit game binaries, decompiled source trees, proprietary encyclopedia content, or patched game files. Do not destroy or modify the existing game installation. Prefer read-only inspection/instrumentation, using a temporary isolated environment if an experiment is needed.
- If the game installation, assemblies or necessary tools are absent, still inspect what is available, document exactly what cannot be verified, and specify the minimum inputs needed. Do not fabricate class names or pretend to have inspected inaccessible code.

## Investigation topics

### A. Runtime integration and lifecycle

- Determine Unity version, managed runtime, assembly layout, loading/hooking/mod framework options, initialization and shutdown points.
- Identify safe game-thread access patterns for unit state and command dispatch. Document whether/when scene transitions, save/load, pause and scenario restarts invalidate references.
- Find the minimal stable hook(s) for producing immutable observations and draining a command mailbox without blocking simulation.
- Investigate whether an external C++ process can communicate with a thin C# game adapter over local IPC; list realistic transports and packaging constraints **without finalizing the choice**.

### B. Unit registry and equipment state

- Locate the full authoritative unit registry, object lifecycles and ownership/faction identifiers.
- Locate friendly unit state: position, heading, speed, depth/altitude, health/damage, damage control, sensors, emitters, ammunition, launchers and other weapon-system states.
- Find the source of class/encyclopedia data: platforms, sensors, weapons, nominal ranges, supported target types, restrictions, and whether probability-of-hit data are static or dynamic. Include custom/modded content and stable identity possibilities.
- Note retrieval cost or threading limitations where observable.

### C. Contacts, information boundaries and perspective

- Find each faction's sensor detections, tracks/contact lists, track state/classifications and ally-shared observations, if present.
- Identify what is available to player-controlled and **AI-controlled** factions. Determine whether a faction-neutral observer can be built without using omniscient raw enemy state.
- Note how contact identity is maintained, stale tracks disappear, observations change, and confidence/uncertainty are represented. V1 can assume one track per physical unit internally but the generic API cannot.
- Explicitly list potential information-leak paths: authoritative IDs, exact hidden positions, classifications inferred from inaccessible attributes, scenario/editor intelligence, non-observable events.

### D. High-level orders and native automation — HIGH PRIORITY

- Identify the actual high-level orders available to humans and AI: navigation/waypoints, speed/altitude/depth, sensors, weapon engagement and mission-level commands (if any).
- Locate the command entry points used by the UI and by AI. Identify argument types, validation, order persistence, current-order representation, start/end/failure, cancellation and reissuance.
- **Trace automatic behavior** such as missile evasion and other urgent sequences. Establish how it preempts current orders, whether orders resume, how long the unit is occupied, and exactly how a new external/player order interferes.
- Determine how to observe when a unit is autonomously busy and which commands should be blocked to avoid overriding a protected native response.
- Investigate whether native automatic controllers can be enabled/disabled selectively by unit or subsystem for future external deterministic controllers. Do not disable them in the game as part of routine reconnaissance.
- Verify the difference between commanding the player's units and commanding units assigned to the game's AI factions.

### E. Simulation time and execution control

- Find authoritative operational-time source(s), update loop and time scaling/compression behavior.
- Find pausing and resuming APIs, user/manual pause ownership, and ways to prevent the external scheduler from accidentally overriding a player's pause.
- Identify safe application of paused-mode decisions: when snapshots are valid and commands can be accepted while paused.
- Assess feasibility of **synchronous**, **hybrid**, and **fully asynchronous** slow-decider operation; investigate whether a decision scheduler can pause at an exact operational-time boundary or only approximately.
- Do not decide the hybrid missed-cycle policy until actual game timing behavior is known.

### F. Environment, incidents and mission briefing

- Identify bathymetry, land/obstacle, weather, thermal layers, acoustics and other environmental query sources; note what is actually modeled and what may only be cosmetic.
- Identify short-lived observable incidents worth accumulating (e.g., launch warning, detected missile, temporary native evasion, system failures, order interruptions). Determine suitable interception/logging points that respect faction visibility.
- Confirm the limitations of AI-side mission/briefing data. We plan to inject externally authored tasks, briefings and intelligence; note any useful native information but do not assume player briefing text is available to AI factions.

## Required deliverables

Create **`Docs/SEAPOWER_FINDINGS.md`** with:

1. **Environment inspected:** game build/version (if known), assemblies, tooling, relevant public mod versions, and any inaccessible resources.
2. **Executive feasibility verdict:** can a side-specific state reader, high-level order writer and safe pause controller be built? Mark each capability verified, likely, unknown, or blocked.
3. **Evidence inventory:** table with `Capability | Assembly/type/member or hook | Behavior | Evidence/inspection method | Confidence | Constraints`. For negative findings, say what was checked and why absence remains uncertain.
4. **Native automation deep dive:** at least one concrete behavior path (preferably missile evasion), including trigger, execution state, collision with manually issued orders, and whether previous orders resume. If unverifiable, report the exact gap and how to test it.
5. **Perspective/security model:** player versus AI faction visibility; leakage hazards and proposed minimal filtration boundary.
6. **Order catalog draft from actual code:** discovered order families and arguments, **not** a fabricated universal schema. Classify each as verified/inferred/unknown.
7. **Runtime and concurrency:** game-thread access, snapshot safe points, command dispatch, pause/resume and IPC options.
8. **Proposed thin C# adapter outline:** entry points, snapshot collection, native automation protection, and local IPC responsibilities, without implementing them yet.
9. **Recommended V1 vertical slice:** smallest end-to-end demonstrator that preserves native automation (for example, observe one side, list one contact, issue an ordinary high-level movement order, recognize a protected busy state).
10. **Open questions and investigation experiments:** order by risk and expected information gain.

If the investigation reveals architecture changes, add a section titled **`Proposed changes to ARCHITECTURE.md`** rather than silently rewriting settled decisions. Include trade-offs.

## Definition of done

- Every materially important factual claim identifies inspectable evidence or is explicitly marked as hypothesis.
- The report explains what Sea Power actually does for native high-level orders and urgent automation—or precisely states why this was not verifiable.
- No prototype is required, and no full API or AI implementation should be introduced.
- Output can be reviewed independently by someone who knows C++ architecture but has not yet explored Sea Power's code.

## First Codex instruction

> Read `AGENTS.md`, `Docs/ARCHITECTURE.md`, and this task file. Inspect available Sea Power assemblies and relevant modding sources read-only. Produce `Docs/SEAPOWER_FINDINGS.md` following the required deliverables. Prioritize native urgent automation, high-level orders, faction-specific contacts, threading, and pause control. Do not implement the interface or speculate about unavailable game behavior; clearly document missing access and unresolved questions.
