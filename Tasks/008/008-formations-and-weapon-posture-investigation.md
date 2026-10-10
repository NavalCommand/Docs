# Task 008 — Sea Power Formations and Weapon-Engagement Posture Investigation

**Type:** research, reverse-engineering and bounded runtime validation. **Do not implement new production features in this task.**

## Context and objective

NavalCommandFramework is an external tactical decision/control system with five independent functional repositories and a separate `Build.CMake` repository. The in-game adapter exposes faction-restricted observations and validated high-level orders through canonical `Gateway.Api` Protobuf payloads and the generic `Orders.Api`. The native Sea Power simulation must retain low-level automation and emergency defensive behavior. Existing work implements an initial command path; subsequent work on repeatable movement and facility launch is specified separately. Do not assume those later features are complete until you inspect the actual checkout.

This investigation answers two questions left open in the architecture:

1. **Formations:** what constitutes a formation in Sea Power, whether player-controlled units and AI-controlled units use the same formation mechanisms, and what observable/control abstractions (if any) would be worthwhile for NavalCommandFramework.
2. **Weapon-engagement posture:** what the game's three weapon-control modes actually are, whether AI-controlled units possess and use them, how they affect autonomous weapon use, and whether a getter and/or setter belongs in our generic observation/order contracts.

This is not a generic feature wishlist. Base conclusions on the **installed game build and observed native behavior**, distinguishing static evidence, runtime evidence, inference and unknowns. Investigate *whether* a capability is useful before recommending it for the public API.

## Inputs and constraints

Read the current `Docs/AGENTS.md`, `Docs/ARCHITECTURE.md`, `Docs/MODULE_STRUCTURE.md`, and relevant historical reports under `Docs/Tasks/001/`, `002/`, `003/`, `004/`, and `006/`. In particular:

- Earlier static inspection found `ObjectBase.WeaponStatus` and `CoreService.GetWeaponPosture` / `SetWeaponPosture`. **Their existence does not demonstrate AI use or semantics.**
- The existing adapter conservatively excludes unsupported formation contexts; that is a current implementation boundary, not proof formations are impossible.
- Native `AIPlayer` strategic planning, lower-level unit AI, native facility mission decision logic, flight/ship formation following, and `Taskforce` grouping may be **different systems**. Do not equate them.
- Previous runs observed `AIPlayer.IsForcedToDisable = true` in a development context. Do not draw conclusions about normal active AIPlayer behavior from disabled-AI tests.
- The user's description listed “weapons free, weapons tight, weapons free” (with one label repeated). **Check the game's actual UI, enum, serialization and game semantics to establish the three canonical mode names**; do not rely on an assumed correction.

Use the existing approved, opt-in development loader and isolated test fixtures when runtime instrumentation is necessary. Do not patch vendor binaries, globally suppress AI/defense to simplify a proof, introduce a production cheat/privileged observer, disclose opponent ground truth to public observations, or commit proprietary binaries or decompiled code. Privileged diagnostics used solely in test fixtures must be segregated from production surfaces. Observe normally active AI wherever feasible; identify any reproducibility limitations. Preserve all Task 004/005/006 functionality, the code readability and documentation work of Task 007, and the established build conventions. Task 008 is **not** a code-style/refactoring task; follow the persistent code-style requirements when writing investigation helpers.

## Gate A — Map formation systems (static investigation)

Trace the real player/UI and native AI code paths for creating, joining, maintaining, ordering, splitting and dissolving formations. Differentiate, where present:

- Surface-ship formations versus aircraft formations, helicopter groups, submarine coordination and `Taskforce` membership.
- Formation leaders, followers/wingmen, relative offsets/slots, membership identities, task or mission assignments, ownership/side, status and persistence across spawn, recovery, destruction and scene reload.
- User-issued leader movement and append/replace-waypoint commands, formation-wide propagation, individual-unit override/detachment and rejoining.
- Native automatic behaviors: leader changes, station-keeping, speed/altitude/turn constraints, evasion, engagements, RTB and mission-level AI.
- Formation setup/control available in player UI versus formation choices made by AI planners and low-level controllers.

Map concrete classes/methods/call paths and distinguish *public-interface candidates* from internal implementation details. If the AI uses a different mechanism, explain the actual interoperability boundary. “A `Formation` type exists” is not an adequate finding.

## Gate B — Validate formation behavior in runtime

Use the smallest reproducible representative fixtures available, preferably at least one ship group and one air group, and both player- and nonplayer-controlled sides **where independently observable in diagnostics**. Inspect whether AI spontaneously creates/assigns/reforms groups during normal operation. Do not assume it does.

Measure and record:

1. Whether the scenario already defines the group or an AI subsystem creates it during play; identify the source of formation membership.
2. Leader/follower identity and positions/offsets over time, including changes during movement and relevant maneuvering.
3. Response to a **native player** group command versus an **AI-generated** order; note whether follower commands are explicit or autonomous.
4. How ordinary movement on a member or leader interacts with the formation, including route replacement and append if those paths are available in the checkout.
5. Interaction with native emergency automation; verify no test instrumentation interferes with missile avoidance or automatic RTB.

If active strategic `AIPlayer` behavior cannot be exercised reliably, separate what is proven about lower-level AI from what remains unknown about the strategic planner. Do not claim an absence of AI formations merely because the inspected scenario, flag or fixture does not use them.

## Gate C — Map weapon-engagement posture (static investigation)

Determine the exact names, integer/enum/string values, defaults, native owner and scope of the **three** UI weapon-control modes. Do not conflate weapon posture with EMCON/sensor posture, manual attack orders, weapon targeting policy or task state.

Trace at minimum:

- UI read/change flow and `CoreService.GetWeaponPosture` / `SetWeaponPosture` down to authoritative unit state, including `ObjectBase.WeaponStatus` and any distinct AI/formation/taskforce representation.
- Whether postures apply per **unit**, formation/leader, taskforce, side, mission or multiple scopes; whether setters propagate.
- Persistence after save/reload, respawn, order replacement, reassignment and ownership changes, where applicable.
- Native AI routines that **read** the posture before detecting/selecting/engaging targets and those that **write** or override it. Distinguish strategic `AIPlayer` decisions, lower-level fire-control AI, explicit attack orders, automated point defense/CIWS and emergency self-defense.
- The actual behavioral difference between modes: permitted target categories and identification thresholds, automatic engagement versus authorization, whether explicit player/AI orders bypass the mode, and game-specific exceptions.
- Units/platforms on which each mode is meaningful (e.g. ships versus aircraft), and the behavior of units lacking weapons or with no applicable auto-engagement code.

Trace complete call sites and guards: do not infer practical behavior solely from UI labels or `CoreService` signatures.

## Gate D — Validate weapon-posture behavior in runtime

In isolated controlled fixtures and **without modifying the actual production adapter**, gather evidence for the same type of unit under the available postures, with comparable targets/tracks and conditions. Use a non-player AI unit where valid and compare it to player-controlled behavior. Tests must distinguish AI-issued attacks from spontaneous fire-control decisions and emergency point defense.

Capture posture readback, origin of settings, relevant AI decisions, target visibility/classification, engagement requests, native shots or refusals, and time/conditions. If changing the posture is necessary, use the verified normal game/UI setter in a test context and confirm readback; do not set fields directly unless explicitly labeled as a limited diagnostic. Ensure every actual engagement observed is attributable to the tested behavior rather than a preexisting attack task.

Where a scenario cannot isolate effects without unsafe manipulation or excessive uncertainty, report a scoped inconclusive result and propose a safer experiment. In particular, **no assertion that AI ignores/uses a mode without tracing the relevant decision path and observing an appropriate controlled case**.

## Gate E — Decide whether NavalCommandFramework needs these APIs

Conclude with **two explicit, evidence-based recommendations**, not a mandatory implementation:

### Formations

Decide separately whether to provide:

- A formation/group relationship in `World.Api` (leader, own membership, observable role, lifecycle; only if meaningful and reliably obtainable).
- Dynamic supported/currently-executable **formation-level orders** in the existing `Orders.Api` (create, join, leave, leader/group route, formation type/slot), or whether ordinary per-unit orders with safe native propagation are sufficient initially.
- Formation context on current generic order eligibility to prevent accidental detachment or overrides.

Evaluate need, native feasibility, authority/ownership, stable identity, AI parity, transport exposure and leak risks. Consider a minimal first representation rather than prematurely mirroring native object graphs. No new separate `Formations.Api` without compelling evidence and explicit approval.

### Weapon posture

Make **independent yes/no/defer decisions for a getter and for a setter**, for player and AI sides as appropriate:

| Surface | Questions to answer |
| --- | --- |
| Getter | Does reading native posture materially help the external controller understand/predict automatic engagements or avoid issuing contradictory orders? Is the value observable, current and attributable to a unit/formation? |
| Setter | Can an external order meaningfully change native automatic behavior for a controllable unit, and can validity, support, propagation, precedence and dispatch-time rechecks be implemented safely? |

If recommended, sketch the **minimal optional wire fields or order types** in the canonical `Gateway.Api` messages / `Orders.Api`, enum mapping, applicability, provenance, rejection reasons and statuses. State clearly whether this is mission doctrine, unit posture or both. Do not create a parallel schema or bypass the normal generic order discovery/validation. Do not implement any public RPC, getter/setter or formation control as part of Task 008.

Explain how the recommended API would behave when a human can set a posture but the AI's own controller may supersede it; do **not** claim external order authority over active native AI without evidence.

## Required deliverables

Place outputs **only in the Git-tracked `Docs/` repository**:

- `Docs/Tasks/008/FORMATIONS_AND_WEAPON_POSTURE_FINDINGS.md`: concise executive recommendation followed by source-backed call paths, tested scenarios, per-platform/player-vs-AI matrices, what each posture does, relevance of getter/setter, unresolved questions and suggested next implementation milestones.
- `Docs/Tasks/008/REPRODUCTION.md`: exact game/version identifiers, fixture names, setup, opt-in instrumentation, commands, expected observations, cleanup/restore and limitations.
- `Docs/Evidence/Task008/`: small, sufficient test outputs, diagnostic traces, hashes and an evidence index; distinguish static/source inspection from actual runtime outcomes. Exclude proprietary binaries and sensitive local paths.

Keep task reports under `Docs/Tasks/008/` and test artifacts under `Docs/Evidence/Task008/`; do not append progress reports to long-lived `Docs/AGENTS.md`, `ARCHITECTURE.md`, or `MODULE_STRUCTURE.md`. Only propose narrowly scoped permanent-document amendments **if the investigation establishes a durable design decision**; otherwise leave those documents unchanged.

## Acceptance criteria

- [ ] Native formation mechanisms and UI/AI call paths mapped, including leader/follower control, formation movement and ownership.
- [ ] Runtime evidence for at least one representative formation when the installed build permits; clear distinction between scenario-authored formations and AI-created formations.
- [ ] All three weapon-posture names/values/semantics verified from the installed build.
- [ ] Actual AI reads/writes/use of posture traced, with controlled player-versus-AI runtime comparison or a documented, reproducible blocker.
- [ ] Interaction with manual engagements, native point defense and protective autonomy assessed without disabling safety behavior.
- [ ] Explicit formation-observation/order recommendation and separate weapon-posture getter/setter decisions (implement, defer, or omit) with reasons and scope.
- [ ] No speculative production contracts or premature mutating API implemented; no vendor or current functional behavior modified.
- [ ] Evidence, repro steps and limits recorded under the correct `Docs/` subdirectories.
