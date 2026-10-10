# Task 012 — Sea Power weapon engagement envelope and effectiveness investigation

**Status:** Proposed / not started  
**Type:** Static investigation, limited runtime validation, and API feasibility assessment. **Do not implement a production API in this task.**  
**Priority:** Investigate the existing player weapon-order hover display and its native calculations before deciding how to expose equivalent read-only information through NavalCommand.

## 1. Motivation and scope

Sea Power displays contextual information when the player is preparing a weapon/attack order and hovers a potential target track. The owner recalls at least:

- Shooting/firing range for the selected weapon against that particular target type.
- A **no-escape range** (or similarly named metric) associated with the hovered target.
- An **effectiveness** indication.

There may be other fields, colors, indicators or conditions. **These observations are starting leads, not verified specifications.** Recover the exact UI labels, source values, units, meanings and availability conditions from the installed game. In particular, do **not** assume that “effectiveness” is a calibrated hit probability, or that no-escape range is a universal property of a weapon independent of launcher, target, geometry and current conditions.

The longer-term goal is to allow external deterministic and LLM-assisted controllers to ask, for a **friendly unit / specific weapon or launcher / observable target track**, what the game can currently determine about engagement feasibility and likely effectiveness. The immediate deliverable is an investigation and a justified recommendation, **not** a selected schema or implementation.

Use the current checked-out NavalCommand repositories and the installed game version. Read the latest permanent `Docs/AGENTS.md`, `ARCHITECTURE.md`, `MODULE_STRUCTURE.md`, `CODE_STYLE.md`, `CodeStyle.xml`, `LOCAL_SOURCES.md`, relevant findings from Tasks 001–003, actual Task 006 implementation/evidence, and Task 008's investigation specification. Task 008 concerns weapon *engagement posture* (rules of engagement), which is distinct from the weapon/target envelope and effectiveness investigated here. Do not merge the tasks.

## 2. Gate A — Reconstruct the exact player hover feature

Start from the **real UI interaction**: a player-owned unit and weapon selected or an attack/weapon order pending, with the cursor hovering an observable track. Trace the UI/controller/view-model path through target selection, weapon inventory/launcher choice, hover handlers, native services/calculations, localization and final on-screen presentation.

Identify with concrete native type/member/method references:

1. The precise state or sequence required to show the display (selected own platform, weapon, magazine/launcher, current attack action, target hover, track classification, availability of fire control).
2. Every value displayed, including fields the owner did not recall; exact in-game labels, units, formatting, colored cues, explanatory tooltips, hidden/disabled values and relevant localization keys.
3. Which entity each field describes: selected weapon model, specific launcher, friendly firing platform, hovered **track**, actual target object, environment or combined engagement geometry.
4. Whether the UI shows nominal maximum range, minimum range, current distance, effective range, target-type-adjusted launch range, no-escape zone, probability/score or other distinct concepts. Map each UI label to its **actual source and computation**, not its apparent English meaning.
5. Conditions under which a field changes, is unavailable, uses a fallback, is capped, or is estimated; how selected side and weapon/target compatibility constrain it.
6. Whether the display is refreshed on mouse movement, a periodic UI tick, weapon/track state changes or cached event callbacks; the lifetime and thread requirements of involved native objects.

Record the exact installed Sea Power version, the hash of the inspected managed game assembly, tool used for inspection and the native call path. Distinguish UI code from underlying simulation computations. If no literal or method matches a remembered label, report the actual labels found instead of inventing equivalence.

**Gate A passes** when the on-screen feature and its underlying source calls are mapped end to end, including any missing or unexpected displayed information.

## 3. Gate B — Establish calculation semantics, prerequisites and side effects

Follow the actual implementation of each relevant numerical or categorical output. Investigate at least:

- **Range/envelope:** weapon-class properties versus platform-, launcher-, ammunition-, firing-mode- and target-category-specific limits; minimum/maximum boundaries; actual distance; launch conditions; target altitude/depth, speed/aspect/course, target maneuver assumptions, and environmental influences **where they actually participate in the game calculation**.
- **No-escape range:** exact definition and source; whose escape behavior or kinematics it models; whether it changes with target type or relative geometry; whether the game uses a static data-table value, simple formula, precomputed curve, or runtime model. Check if the feature is present for all weapon families.
- **Effectiveness:** establish whether the value represents a percentage probability of hit, expected damage, kill probability, ranking, compatibility score, heuristic, or other metric; inspect normalization, thresholds, rounding and any randomness. **Do not label it `hitChance` without evidence that it really is a probability of hitting.**
- **Weapon and target choice:** how the display handles multiple compatible weapons, multiple mounts/ammunition types, launcher readiness, unavailable rounds, unguided weapons, aircraft missiles, surface missiles, torpedoes, guns, and unsupported target categories. Identify what the inspected build actually implements instead of demanding full coverage for every class.
- **Information provenance:** whether calculations use player-visible track observations, classified/estimated attributes, known type catalog values or inaccessible true target properties from `BaseObject` or other native links. Test what an imprecise, stale, partially classified or lost contact does to the display.
- **Safety of read-only access:** whether nominal “getter” paths mutate cache, consume random numbers, affect native targeting state, allocate long-lived tasks, cause simulation side effects, depend on Unity/render objects, or require special game/UI thread context. Inspect reachable callees rather than declaring the top-level function pure by name.

For each field, document its precise value type, range/units, data dependencies, trust/provenance, native callsite, version/asset assumptions, uncertainty and supported/unsupported cases. Distinguish *possible to compute* from *currently executable as a firing order*. A target may be inside a theoretical envelope but not fireable because of ROE, ammunition, launcher alignment, identification, sensor/tracking or native protection.

**Gate B passes** when the metrics can be interpreted without conflating UI heuristics, physical limits, native engagement permission and actual probabilities.

## 4. Gate C — Bounded runtime reproduction and validation

Reproduce the player feature in the installed game using the existing approved, opt-in development loader/harness and **one or a few small own-side scenarios**. Do not require live firing or modification of protected gameplay. Prefer observing normal player hover and native display values before using a diagnostic probe.

At minimum, where the game fixtures permit, compare:

1. One player-controlled friendly firing platform with a valid selected weapon and a visibly identified, compatible target track; capture the full hover display and a time-aligned read of the values feeding it.
2. The same weapon/target situation at meaningfully different observed distances to distinguish fixed weapon range from geometry-dependent metrics and determine when firing eligibility changes.
3. A different target category, classification or identifiable weapon selection, to establish which values are genuinely target-dependent and which are static. Prefer a second representative weapon family (e.g., missile vs torpedo) when conveniently reproducible; otherwise state the limitation.
4. A case with unavailable ammunition, noncompatible target or incomplete/stale track information, identifying whether the UI hides/replaces values and how the native calculation behaves.

Record actual shown numbers **and** relevant calculation inputs/outputs, units, sampled mission time, object/weapon class, observed track state, and outcomes. Confirm that changes align with traced code. Do not substitute a test of displacement, nominal catalog specifications, or an arbitrary numerical example for validation of the UI feature.

If direct mouse/GUI operation cannot be automated reliably, manually document it or use an isolated diagnostic to invoke the same verified native **read-only** calculation under equivalent conditions. Label the two test methods honestly: static call-path inspection or native-equivalent invocation does **not** prove an actual GUI hover occurred.

Use the player-observable track as the public-side input model. A privileged test fixture may inspect true target state to diagnose possible information leakage, but it must remain isolated and never be repurposed to expose opponent ground truth through the production provider. No scenario/global-state mutations, synthetic target creation in production, automatic weapon discharge, global AI disabling, or changes to vendor binaries. Keep fixture/deployment reversible and preserve installation integrity.

**Gate C passes** on bounded, attributable runtime evidence. If a relevant UI or target scenario cannot be exercised safely, report a **Partial/Blocked** result with the exact cause rather than fabricating data.

## 5. Gate D — API feasibility and design recommendation (no implementation)

Based on the observed native semantics, answer these questions separately and explicitly:

### 5.1 Is a read-only engagement-envelope query justified?

Could an external controller request an engagement assessment for a **friendly session-scoped unit + specific available weapon/launcher/munition + observer-visible track** without needing a mouse, current UI selection or issuing an attack order? Can the game calculate this on demand using authoritative but *authorized* inputs, or does the UI depend on transient state/privileged enemy truth? Consider side-scoped views, opaque track IDs, selected-faction constraints and stale observation versions.

Separate **mostly static weapon catalog properties** (e.g., nominal range and compatible target classes) from **dynamic weapon–track engagement estimates** (e.g., current distance, geometry-dependent range, no-escape conditions, current effectiveness). Explain whether a cached catalog, a bounded explicit query, a mixed approach or **no safe API yet** is warranted. Do not automatically put all metrics in `World.Api` snapshots, and do not automatically create a new domain repository or a second transport.

### 5.2 What can each field honestly promise?

Produce a proposed **conceptual field table**, not code or `.proto` changes. For each candidate report:

- Public name and plain-English meaning, anchored to the game evidence.
- Unit, range, optionality/unsupported state, and whether it is static, dynamic, or approximate.
- Dependence on current weapon/launcher state, target track classification/uncertainty, relative geometry, weather/environment or native caches when proven.
- Provenance, timestamp/content version and consistency/staleness limits.
- Whether the field is a range bound, permission to fire, relative heuristic or actual probability, and **which distinctions must remain explicit**.

If the game never computes a justified `hitProbability`, recommend omitting that concept rather than deriving a misleading percentage. If the UI obtains exact enemy attributes from native truth, explain how to avoid revealing them; do not recommend exposing the original value as if it were a legitimate track observation.

### 5.3 How expensive and safe would it be?

Assess main-thread requirements, query frequency and complexity, side effects/RNG/cache mutation, predictable per-query cost, bounded simultaneous queries, stale target/weapon handles, asset/config versioning and backend portability. Recommend the minimum safe mechanism to reuse game computations if possible; identify fields that require a new approximation or would have to be withheld.

### 5.4 What comes next?

Provide **one recommended minimal V1 API concept** with alternatives and trade-offs, or recommend deferring if safe semantics cannot be demonstrated. Sketch only the requested identifiers and response shape in prose/table/pseudocode as needed. Do not add RPCs, Protobuf schema fields, C++ domain headers, managed provider methods or new generic client calls in Task 012. API design will be approved **after** reviewing this investigation.

**Gate D passes** when the findings support a concrete implement/defer decision for each meaningful metric and an honest minimum-scope query design.

## 6. Project constraints and exclusions

- **Investigation only.** Do not implement engagement assessment, new weapon orders, autonomous firing or production observations in this task.
- Preserve architecture from Tasks 006–011: domain contracts in `Domain.Api`, canonical wire messages in `Gateway.Api`, service contract in `Gateway.Grpc.Api`, simulator-independent gateway and game-facing logic in `SeaPower.Client`, runtime/harness composition in `SeaPower.Mod`.
- Respect `Docs/CODE_STYLE.md` and unmodified owner `CodeStyle.xml` in any diagnostic source. Do not perform unrelated header, naming, build or formatting TODO cleanup.
- Keep read-only queries observational: never change native order/task selection, launch/firing state, targeting allocations, simulation RNG or caches invisibly. If the UI computation itself has side effects, identify them and avoid using it as a supposedly pure production getter.
- Never expose classified enemy object identity or truth data from the opponent's real `BaseObject`. Use separate test-only privileged diagnostics where necessary and mark them clearly.
- Do not require or execute real attacks to prove a UI estimate, and do not disable native protective behavior. Do not alter proprietary game files, ship decompiled source or commit proprietary binaries.
- Avoid unnecessary exhaustive class inventories, mass scenario automation, framework restructuring, Task 008 formation/posture work, unrelated Debug teardown fixes or LLM integration.
- If this investigation reveals a *side-effect-free* safe API is impossible or ambiguous, reporting **not currently feasible** is a successful outcome.

## 7. Deliverables and acceptance

Write outputs into the existing `NavalCommand/Docs` repository:

- `Docs/Tasks/012/WEAPON_ENGAGEMENT_FINDINGS.md`: concise executive summary, exact UI display inventory, native call graph/source references, calculation table, distinctions between range/no-escape/effectiveness/hit probability, information-leak and side-effect audit, runtime observations, recommended API approach and unresolved questions.
- `Docs/Tasks/012/REPRODUCTION.md`: short, repeatable inspection/fixture steps, installed game version and assembly hash, selection/hover procedure, diagnostic opt-in, expected checks and clean rollback.
- `Docs/Evidence/Task012/`: **small, focused** screenshots/value traces/logs or source-reference tables sufficient to substantiate actual results. Do not produce giant build archives or make evidence collection the main task. Exclude proprietary binaries, decompiled game sources and any confidential local environment files.
- Update `Docs/Tasks/README.md` with the new Task 012 entry and its accurate status. Do not rewrite permanent architecture documents until the user approves a concrete API decision.

Finish with a gate table:

| Gate | Goal | Evidence | Pass/Partial/Blocked | Gaps |
|---|---|---|---|---|
| A | Native hover/UI path | | | |
| B | Calculations and truthful meanings | | | |
| C | Runtime validation | | | |
| D | API recommendation only | | | |

**Completion criterion:** We can explain exactly what Sea Power displays during weapon targeting hover, what the game actually computes and from which data, which information is legitimately available to an external side-scoped controller, and whether a read-only engagement-envelope/effectiveness query can be implemented without guessing, leaking hidden truth or perturbing the simulation.

Commit documentation and any strictly necessary isolated test fixtures in the appropriate repositories. **Do not push** without explicit authorization. Do not implement the proposed API before review.
