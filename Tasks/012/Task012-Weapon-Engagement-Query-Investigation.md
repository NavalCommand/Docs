# Task 012 — Sea Power weapon engagement envelope and effectiveness investigation

**Status:** Partial — [static findings](STATIC_FINDINGS.md) and [bounded runtime diagnostics](RUNTIME_FINDINGS.md) recorded. The [type-first catalog/equipment domain model](DOMAIN_MODEL_PROPOSAL.md) is now the active design discussion; the earlier [method-first Gate D sketch](API_DESIGN_PROPOSAL.md) is deferred pending type approval. GUI parity, native side-effect attribution and implementation remain open. Task 012 is not complete.
**Type:** Static investigation, limited runtime validation, and API feasibility assessment. **Do not implement a production API in this task.**  
**Priority:** Investigate the existing player weapon-order hover display and its native calculations before deciding how to expose equivalent read-only information through NavalCommand.

## 1. Motivation and scope

Sea Power displays contextual information when the player is preparing a weapon/attack order and hovers a potential target track. The owner recalls at least:

- Shooting/firing range for the selected weapon against that particular target type.
- A **no-escape range** (or similarly named metric) associated with the hovered target.
- An **effectiveness** indication.

There may be other fields, colors, indicators or conditions. **These observations are starting leads, not verified specifications.** Recover the exact UI labels, source values, units, meanings and availability conditions from the installed game. In particular, do **not** assume that “effectiveness” is a calibrated hit probability, or that no-escape range is a universal property of a weapon independent of launcher, target, geometry and current conditions.

The longer-term goal is to allow external deterministic and LLM-assisted controllers to ask, for a **friendly unit / specific weapon or launcher / observable target track**, what the game can currently determine about engagement feasibility and likely effectiveness. The immediate deliverable is an investigation and a justified recommendation, **not** a selected schema or implementation.

### Agreed design direction — player-visible parity with backend-independent semantics

This guidance supersedes any implication elsewhere in this specification that a calculation is disqualified merely because Sea Power uses its internal true target state:

1. **Player-visible parity is acceptable.** The Sea Power adapter may internally call calculations that use a target's privileged `BaseObject` state when the **derived result** is information the native UI normally makes available to the same player/faction for the same observable contact, weapon and conditions. The API must never export the underlying hidden identity, exact target properties or other privileged inputs. Reusing a native computation is preferable to inventing a different approximation solely because its inputs are internal.
2. **Honor information eligibility, not accidental UI configuration.** Investigate and preserve substantive visibility gates (controlled faction, contact visibility, identification/classification, uncertainty and weapon/target eligibility). Distinguish these from optional presentation choices such as `HitChanceDisplay = Off/Rough/Full`: a cosmetic preference is not automatically a prohibition on querying an otherwise player-accessible metric. Establish what the game actually permits before deciding the default public policy. Do not expose values the player could never obtain through ordinary permitted UI behavior without a separate design decision.
3. **Generic analytics contracts can outlive a backend.** Recommend backend-independent engagement-assessment concepts rather than binding public domain names to Sea Power classes or guaranteeing universal support. Unsupported or withdrawn native calculations need not trigger schema removal or fabricated fallback values.
4. **Per-metric availability is first-class.** Consider `Available` (value provided), `Unavailable` (concept supported but unavailable for this weapon/track/state, with a reason), and `Unsupported` (backend/version does not offer the concept or has intentionally withdrawn it). Include enough provenance, units, sampling/observation version and reason information to interpret values honestly. Distinguish whole-query invalid/not-found/unauthorized errors from a valid assessment containing unsupported metrics. No fake zero or guessed percentage for missing information.
5. **Separate capability discovery from catalogs and observed equipment.** Backend analytics capability discovery describes the generally supported query/metric kinds, not the unit catalog, weapon catalog or current ammunition inventory. The generic API must also expose a **complete catalog of unit classes with possible normal/allowed equipment and ammunition loadout configurations**, linked to the complete catalog of weapon, ammunition and launcher definitions; these are content-versioned, not mission inventory. A separate observer-scoped equipment query must support both a friendly unit and an enemy track, with actual known/estimated/unknown installed systems and ammunition, partial coverage, provenance and time. Class-standard armament is not confirmation of a particular enemy track's fitted equipment or remaining stock. The V1 analytics contract remains a proposal until the investigation is reviewed; catalog/equipment implementation deserves a separate task.
6. **Keep analytic meanings accurate.** A qualitative effectiveness score, predicted kill chance, 50%-radius estimate and actual probability of a hit are not interchangeable. An engagement envelope does not imply that firing is currently authorized or executable.

These are architectural *evaluation criteria* for Task 012, not authorization to add APIs, new production behavior, RPCs or schemas before Gate D is reviewed. Preserve the prior static findings as evidence; verify applicability and UI-parity conditions in runtime.

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
- **Information provenance and UI parity:** whether calculations use player-visible track observations, classified/estimated attributes, known type catalog values or internal true target properties from `BaseObject` or other native links. Internal truth-derived **UI-visible results are not prohibited**; establish which output values are ordinarily available to the selected player/faction, under which contact and weapon gates, and which underlying facts must remain hidden. Test imprecise, stale, partially classified and lost contacts.
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

Use the observer-visible, faction-scoped **track identity** as the proposed public-side selector. A read-only native diagnostic may internally use the target's `BaseObject` to reproduce the **derived** values ordinarily exposed by that player's UI; this alone is not an information-leak violation. Compare actual displayed values, display suppression/gating and relevant internal inputs to determine parity. Privileged true target state must remain test-only and must never become a public API response. No scenario/global-state mutations, synthetic target creation in production, automatic weapon discharge, global AI disabling, or changes to vendor binaries. Keep fixture/deployment reversible and preserve installation integrity.

**Gate C passes** on bounded, attributable runtime evidence. If a relevant UI or target scenario cannot be exercised safely, report a **Partial/Blocked** result with the exact cause rather than fabricating data.

## 5. Gate D — API feasibility and design recommendation (no implementation)

Based on the observed native semantics, answer these questions separately and explicitly:

### 5.1 Is a read-only engagement-envelope query justified?

Could an external controller request an engagement assessment for a **friendly session-scoped unit + specific available weapon/launcher/munition + observer-visible track** without needing a mouse, current UI selection or issuing an attack order? Can the game calculate **the same player-accessible derived results** on demand, even if the native calculation privately reads true target state? Identify transient UI dependencies, hidden output differences, substantive eligibility gates, side-scoped views, opaque track IDs, selected-faction constraints and stale observation versions.

Expose a **complete, mostly static weapon catalog as a separate platform requirement**: it covers all weapon/munition and relevant launcher definitions known to the simulator, not merely choices currently carried by friendly units. Distinguish catalog properties (e.g., nominal range and compatible target classes), **dynamic friendly-unit inventory** (installed systems and ammunition counts), **backend engagement-analytics capability discovery**, and **dynamic weapon–track assessment** (current distance, geometry-dependent range, no-escape conditions, current effectiveness). The catalog is content-versioned, and optional attributes may be unavailable without silently dropping catalog entries. Explain where a bounded assessment query is warranted. Do not put all metrics in `World.Api` snapshots or introduce another repository/transport merely for these distinctions.

### 5.2 What can each field honestly promise?

Produce a proposed **conceptual field table**, not code or `.proto` changes. For each candidate report:

- Public name and plain-English meaning, anchored to the game evidence.
- Unit, range, optionality/unsupported state, and whether it is static, dynamic, or approximate.
- Dependence on current weapon/launcher state, target track classification/uncertainty, relative geometry, weather/environment or native caches when proven.
- Provenance, timestamp/content version and consistency/staleness limits.
- Whether the field is a range bound, permission to fire, relative heuristic or actual probability, and **which distinctions must remain explicit**.

If the game never computes a justified `hitProbability`, recommend omitting that concept rather than deriving a misleading percentage. If the UI uses exact enemy attributes internally, it is acceptable to reproduce **the derived player-visible metric** while withholding raw truth. Explain the precise UI gate and whether the value can be obtained from the normal UI, rather than requiring calculations to use estimated track properties only.

For each proposed analytic field, describe **Available / Unavailable / Unsupported** semantics, including whether the status is constant for the backend/version or depends on the current weapon and track. Consider independent capability discovery, stable reason codes, observability/provenance, and graceful degradation if a future Sea Power version removes a previously available calculation. Do not conflate a player's cosmetic display setting with backend capability.

### 5.3 How expensive and safe would it be?

Assess main-thread requirements, query frequency and complexity, side effects/RNG/cache mutation, predictable per-query cost, bounded simultaneous queries, stale target/weapon handles, asset/config versioning and backend portability. Recommend the minimum safe mechanism to reuse game computations if possible; identify fields that require a new approximation or would have to be withheld.

### 5.4 What comes next?

Provide **one recommended minimal backend-independent V1 analytics-query concept** with alternatives and trade-offs, or recommend deferring if meaningful parity and read-only safety cannot be demonstrated. Distinguish (a) query-wide validity/authorization and (b) per-metric Available / Unavailable / Unsupported, and consider separate discoverable capability support. Show how future backends or Sea Power versions can withhold individual metrics without contract changes. Sketch only identifiers and response shape in prose/table/pseudocode as needed. Do not add RPCs, Protobuf schema fields, C++ domain headers, managed provider methods or new generic client calls in Task 012. API design will be approved **after** reviewing this investigation.

**Gate D passes** when the findings support a concrete implement/defer decision for each meaningful metric and an honest minimum-scope query design.

## 6. Project constraints and exclusions

- **Investigation only.** Do not implement engagement assessment, new weapon orders, autonomous firing or production observations in this task.
- Preserve architecture from Tasks 006–011: domain contracts in `Domain.Api`, canonical wire messages in `Gateway.Api`, service contract in `Gateway.Grpc.Api`, simulator-independent gateway and game-facing logic in `SeaPower.Client`, runtime/harness composition in `SeaPower.Mod`.
- Respect `Docs/CODE_STYLE.md` and unmodified owner `CodeStyle.xml` in any diagnostic source. Do not perform unrelated header, naming, build or formatting TODO cleanup.
- Keep read-only queries observational: never change native order/task selection, launch/firing state, targeting allocations, simulation RNG or caches invisibly. If the UI computation itself has side effects, identify them and avoid using it as a supposedly pure production getter.
- Never expose classified enemy object identity or raw privileged truth data from the opponent's real `BaseObject`. **This does not forbid derived estimates already ordinarily shown to the same player by the native UI**, even when they are computed using that object internally. Validate and preserve substantive native display/eligibility restrictions; use separate test-only privileged diagnostics where necessary and mark them clearly.
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

**Completion criterion:** We can explain exactly what Sea Power displays during weapon targeting hover, what it computes and from which data, which derived outputs are ordinarily player-accessible, and whether a read-only, backend-independent engagement-assessment query can provide individually available/unavailable/unsupported metrics without revealing raw hidden truth, fabricating values or perturbing the simulation.

Commit documentation and any strictly necessary isolated test fixtures in the appropriate repositories. **Do not push** without explicit authorization. Do not implement the proposed API before review.
