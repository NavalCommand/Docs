# Task 012 — Engagement analytics API design proposal

**Status:** Gate D proposal for owner review; **not approved or implemented**. Based on [STATIC_FINDINGS.md](STATIC_FINDINGS.md), [RUNTIME_FINDINGS.md](RUNTIME_FINDINGS.md), the existing NavalCommand public contracts and the owner's confirmation of ordinary player-visible metrics. Gates A–C remain partially validated; this document does not upgrade their evidence status.

## 1. Decision and scope

**Recommend a single read-only `AssessEngagement` operation** for one friendly firing unit, one ammunition choice and one observer-visible track. Return a typed assessment with independent metric availability; perform at most one bounded, game-thread-safe native calculation job per request. A separate `DiscoverEngagementOptions` operation supplies selectable ammunition identifiers and the backend's metric capabilities. These are *conceptual names and shapes*, not a frozen schema.

This is **engagement analytics**, not a fire order, target designation or permission to engage. An assessment can be available when a shot is prohibited, no ammunition remains, the weapon is held, or an actual launch would be rejected. Existing order-eligibility and dispatch-time authorization remain authoritative.

**Information policy:** The Sea Power adapter may privately use native target `BaseObject` state to obtain **derived information ordinarily available to the same player/faction via the targeting interface**. It must require an observer-visible track and return no enemy `BaseObject` identity, raw true attributes or extra privileged derived values. The owner confirms that the investigated range/flight/envelope metrics are player-visible. Static inspection identified `HitChanceDisplay` `Full`, `Rough` and `Off` modes; the numerical score is a conditional presentation of the underlying effectiveness calculation. Cosmetic display settings need not limit a read-only public metric, but substantive visibility, identification and uncertainty conditions do.

The generic contract **must not depend on Sea Power preserving a particular calculation**. Per-metric `Unsupported` permits future adapters and versions to withdraw a metric without changing the protocol or inventing a replacement.

## 2. Contract ownership and transport

| Responsibility | Proposed owner | Reason |
|---|---|---|
| Abstract engagement request, result, metric availability and meanings | `Domain.Api`, within a small `Engagement.Api` interface area | Neither `Orders.Api` commands nor `World.Api` snapshots describe an on-demand pairwise analysis. No new repository required. |
| Canonical request/response Protobuf representations and codecs | `Gateway.Api` | Keep the sole C++/C# wire schema source and the existing internal C ABI payload exchange. Add messages in a dedicated canonical `engagement.proto` or equivalent. |
| External `DiscoverEngagementOptions` / `AssessEngagement` RPC declarations | `Gateway.Grpc.Api` | Service definitions import the canonical payloads; do not duplicate their definitions. |
| Generic provider forwarding, deadlines and owned results | `Gateway.Api` provider boundary plus `Gateway.Grpc` | The current `IProvider` is thread-safe I/O-side and must **never call simulator code** directly. |
| Native calculation/visibility gates | `SeaPower.Client` | Resolve observer tracks to native objects privately and evaluate on the game thread. `SeaPower.Mod` remains the composition root. |

The current `Domain.Api/Orders/Api/Request.h` carries `sessionId`, `entityId` and `basedOnVersion`; `World.Api/View.h` carries `sessionId`, `observerId`, `version` and `operationalSeconds`. The existing `World.Api/Track.h` has an opaque `id`, not a native object ID. Reuse these conventions without converting an assessment into an order or modifying the existing `Gateway.Api` `Waypoint` wire contract.

Respect `Docs/CODE_STYLE.md`: one independently useful public type per header; examples below are conceptual, not an instruction to place all types into one file.

## 3. Proposed V1 operations

### 3.1 DiscoverEngagementOptions

**Input:** current session ID; friendly unit ID. The caller/connection's observer scope is authoritative (not an arbitrary caller-chosen enemy side).

**Result:**

- The current session, observer and observation version.
- Discoverable **ammunition choices** for that friendly unit, each with an opaque, session-scoped ammunition key, friendly display name and any **verified** selection/stock facts. This is necessary because the present `WorldSnapshot.Unit` has no general ammunition catalog.
- **Generally implemented metric kinds**, optionally qualified by weapon family/platform, versus any capabilities disabled for this backend/game version. Capability support is *not* a prediction that a metric is available for every target or current weapon state.
- Backend/content version or capability revision where known; a changed session or catalog invalidates previously discovered keys.

The Sea Power UI selects an **ammunition type** through `WeaponEntry` and can aggregate multiple compatible weapon systems. V1 therefore addresses ammunition, **not one arbitrarily chosen launcher**. Do not invent mount IDs or claim launcher-specific estimates; revisit mount selection only if a backend's semantics require it.

This discovery is not an executable-order list: a discoverable ammunition key may have zero ready rounds. The assessment response describes analytical results; a future weapon-fire order will require independent eligibility checks.

### 3.2 AssessEngagement

Conceptual request:

```text
EngagementAssessmentRequest {
    sessionId
    shooterUnitId       // friendly, controlled unit
    ammunitionKey       // from DiscoverEngagementOptions
    targetTrackId       // opaque, visible in this observer's view
    optional basedOnObservationVersion
}
```

Conceptual response (names are provisional; units explicitly part of their meanings):

```text
EngagementAssessment {
    sessionId, observerId, shooterUnitId, ammunitionKey, targetTrackId
    observationVersion, sampledAtOperationalSeconds
    modelIdentifier, modelRevision

    currentHorizontalDistanceMeters  : Metric<double>
    minimumLaunchRangeMeters         : Metric<double>
    maximumEngagementRangeMeters     : Metric<double>
    kinematicRangeLimitMeters        : RangeLimitMetric
    sensorRangeLimitMeters           : RangeLimitMetric
    lineOfSightRangeLimitMeters      : RangeLimitMetric
    noEscapeRangeMeters              : Metric<double>
    estimatedTimeOfFlightSeconds     : Metric<double>
    effectivenessScore               : Metric<double>     // model score [0,1], NOT calibrated P(hit)
    effectivenessRating              : Metric<Rating>     // Poor / Moderate / Good
    impactRadius50Meters             : Metric<double>
    engagementIssues                 : Metric<list<IssueCode>>
}
```

All values are **independently optional and status-bearing**, including current distance; the response is not a pile of default-zero numeric fields. The assessment is one coherent evaluation job where possible, not a guarantee of global simulation atomicity. Use assessment-level sampling time and version only when the complete calculation truly shares them; add per-field provenance/time if the implementation composes differently timed sources.

**Version behavior:** An optional `basedOnObservationVersion` expresses the caller's desired source-view precondition. A stale session is always rejected; a stale view precondition is rejected with a stable reason when meaningful. Without a precondition, the result uses the freshest verified data and reports its own version/time. Do **not** require an impractical frame-perfect match with a rapidly changing global observation number if the game cannot provide that guarantee.

**Execution model:** A unary query on the external service may await a bounded marshalled game-thread job through the established provider mailbox. It must have a deadline, bounded concurrency/queue and a distinct timeout/busy failure. No gRPC thread may dereference Unity objects, and query work must not mutate native targeting or orders. Cap request frequency and the number of simultaneous assessments; performance measurements from the two-sample probe are not steady-state cost guarantees.

## 4. Metric availability and honest value representation

Use a **dedicated analytic status**, not `Orders.Api::Availability` (`Executable`/`Blocked` refers to orders):

- `Unknown` — protobuf/default or internal indeterminate status; **never** silently interpreted as available.
- `Available` — a defined value exists **and is present**. A legitimate zero is distinguishable from no value.
- `Unavailable` — the backend supports the concept, but this specific weapon/target/state cannot produce a meaningful or permissible value; include a stable reason.
- `Unsupported` — this backend/version does not implement or no longer exposes the metric (or elects to withhold it by capability policy). This is not a fabricated numeric fallback.

Reason-code examples to refine in implementation: `NOT_APPLICABLE`, `INSUFFICIENT_CONTACT_INFORMATION`, `TARGET_NOT_IDENTIFIED`, `WEAPON_NOT_READY`, `CALCULATION_UNDEFINED`, `NATIVE_METHOD_UNSAFE`, `BACKEND_VERSION_UNSUPPORTED`. An unknown code must still preserve a readable description if available. Do not use `NaN`, `Single.MaxValue`, `-1` or a missing protobuf scalar as an implicit status.

A `RangeLimitMetric` separates **finite limit** from **no finite model constraint** while still being `Available`. Sea Power's `Single.MaxValue` LOS result signifies no finite cap in the evaluated path; it does **not** mean physically infinite range, `Unsupported`, or an actual 3.4e38-unit distance. The finite/non-limiting distinction can be modeled as a tagged value within the metric.

An empty `engagementIssues` list means **evaluated and no issues returned**, not `Unavailable`. `noEscapeRangeMeters = 0` can be a valid evaluated result; `-1` from the native method becomes an explicit missing/not-applicable state.

Whole-query failures remain separate from the metric statuses:

| Case | Query outcome |
|---|---|
| Malformed key/arguments | `INVALID_ARGUMENT` |
| Unknown or non-controlled shooter, invisible/expired target track, or nonexistent ammunition choice | `NOT_FOUND` without disclosing foreign-unit/hidden-track identity; optional stable safe reason |
| Session mismatch or rejected observation precondition | `FAILED_PRECONDITION` with `stale_session` or `stale_observation` |
| Provider not ready, timed-out game-thread job | `UNAVAILABLE` (with bounded retry guidance if appropriate) |
| Admission/queue limit reached | `RESOURCE_EXHAUSTED` |
| Valid analysis with one unavailable metric | **Successful assessment** containing that metric's status; do not reject the entire query |

## 5. Recommended metric definitions

Use **metres and seconds** in canonical wire/domain interfaces, consistent with existing `World.Api` and `Gateway.Api` SI units. Convert to Nmi/feet only in a client UI. Public values need clear numerical ranges and finite-value validation.

| Candidate V1 field | Meaning and Sea Power mapping | Availability and qualifications |
|---|---|---|
| `currentHorizontalDistanceMeters` | Current horizontal shooter-to-target separation used for kinematic/no-escape comparisons; native transforms. | Player-derived result, **not** a disclosure of raw opponent position. Do not silently substitute slant distance. |
| `minimumLaunchRangeMeters` | Selected ammunition's nominal minimum boundary, `_launchRangesInUnity.x`. | May be static by ammunition, but reported with current weapon assessment. Zero is meaningful if confirmed. |
| `maximumEngagementRangeMeters` | Current combined bound, `GetMaxEngagementRanges`: minimum of adjusted kinematic, sensor and terrain limits. | **Not** permission to fire; compare against distance and actual order eligibility separately. |
| `kinematicRangeLimitMeters` | `GetKinematicRange` / `MaxRangePrecise`, current geometry-dependent limit. | Dynamics include true target velocity; private native use allowed by player-visible parity. |
| `sensorRangeLimitMeters` | Native `GetSensorRange` / presentation adjustments. | Respect unidentified-contact adjustments; don't export internal raw values that the UI withholds. |
| `lineOfSightRangeLimitMeters` | Native `GetTerrainRange`. | Can be `Available + NotLimiting`; do not surface the `Single.MaxValue` sentinel. |
| `noEscapeRangeMeters` | `NoEscapeRangePrecise` with source, target and valid maximum cap. | Only established for missile-versus-air cases; other combinations `Unavailable(NOT_APPLICABLE)` or `Unsupported` if the backend never implements it. Supported zero remains zero. UI-equivalent cap/iterations must be verified. |
| `estimatedTimeOfFlightSeconds` | Native engagement-range/time-of-flight calculation. | Dynamic estimate, not a guaranteed shot or weapon-hit time. The UI may suppress this on uncertain contacts. |
| `effectivenessScore` | Normalized native `CalculateHitChance` or specialized gun/CIWS prediction; reported **as a backend-model score**, not `hitProbability`. | When supported and ordinarily accessible; identification gates remain substantive. Different models/weapon families may have fundamentally different meanings. Supply model ID/version; **do not compare scores across models as universal probabilities**. |
| `effectivenessRating` | Native `Rough` label: Poor / Moderate / Good. | For the inspected Sea Power method: score <0.37 Poor, [0.37,0.75) Moderate, >=0.75 Good. Other backends choose and declare their own model/rating mapping; numeric score and rating may have independent statuses. |
| `impactRadius50Meters` | `CalculateExpectedDeviation` / UI `50% Radius`, converting native feet. | Return only when model's radius meaning is verified and finite. The recorded AIM-7M probe produced NaN: `Unavailable(CALCULATION_UNDEFINED)`, not zero. |
| `engagementIssues` | Typed, documented subset of `ObjectBase.EngagementIssue` (lookdown, obscured vision, jamming, target altitude/speed/depth, etc.). | Only issues ordinarily disclosed for this side/contact; map backend-specific issues to stable generic codes where semantics are verified. Unmapped issues need an explicit unknown/extension mechanism rather than falsely equating them. |

**Not included in V1:** fire orders or explicit fire authorization; raw target properties; independent radar-horizon annotations and their misleading fixed “20 ft target” text; wake-homing torpedo preview geometry, safety-offset “Hit Chance” categories or ad hoc predictive curves; unspecified catalog revision schemes; bulk all-target assessment endpoints. These may be separately added after evidence and a specific use case.

The list above is a **proposed surface**, not a promise that all eleven quantitative values are immediately implementable. A V1 backend may return `Unsupported` per metric, and the implementation should prioritize the current distance, minimum/maximum range, no-escape range, TOF, effectiveness, and engagement issues before less common outputs.

## 6. Sea Power implementation map and unresolved gates

| Native facility | Verified by existing evidence | Required before production enablement |
|---|---|---|
| `ObjectBase.GetMaxEngagementRanges` | Static path mapped; two real game-thread samples using F-14A/AIM-7M/MiG-23MLD yielded current, minimum/maximum, kinematic/sensor/LOS and TOF numbers. | Resolve actual `AttackingState` ammunition instance and display adjustments, uncertainty gates and frame/thread-safe access; no UI hover parity demonstrated by the runtime probe. |
| `AmmunitionParameters.NoEscapeRangePrecise` | Two runtime values, properly converted from raw Unity units in the report; supported missile/air behavior mapped statically. | Match UI's exact search cap/iteration arguments; corrected diagnostic source was not rebuilt/replayed. |
| `AmmunitionParameters.CalculateHitChance` | Two runtime `0.6568` scores; static `Full/Rough/Off` presentation and missile/gun/CIWS divergence mapped. | Match any UI gun/CIWS override for selected weapon family; confirm model identity and avoid universal probabilistic semantics. |
| `AmmunitionParameters.CalculateExpectedDeviation` | Runtime NaN on the AIM-7M case; display gate mapped statically. | Verify numerical meaning and categories where applicable, sanitize nonfinite values. |
| Native `EngagementIssue` list | No issues in the two probe samples; static UI issue set identified. | Verify observer-visible/suppressed issue list on other contacts before mapping to public codes. |

**Blocking safety question:** Both probe blocks changed a fingerprint of selected `WeaponSystemHardpoint` fields without per-field/per-call attribution or an idle baseline. This is **not proof that native queries mutate weapon state**, but it prevents claiming they are pure. An implementation task must isolate the changed fields and check whether querying itself changes reservation/targeting/weapon status. If it does, use an isolated read-only equivalent or return `Unsupported`/`Unavailable` for affected metrics. Native simulation-global RNG checks were unchanged in the small sample, but transitive purity is unverified.

**Other operational prerequisites:** resolve side-scoped track ID to `BaseObject` only within the adapter and only while track eligibility holds; avoid leaking identities in logs or errors; ensure the game-thread job and provider-owned response cannot outlive the target/weapon; distinguish track loss and stale query; bound main-thread cost; do not use GUI-selection state or mutate cursor/attack order. Never deploy the Task 012 diagnostic DLL as a production dependency. The previous report says `BepInEx/plugins/Task012EngagementProbe.dll` remains in the local game installation; remove it and verify teardown before any further development run.

### Acceptance checks for a later implementation

1. Valid/invalid/foreign unit and hidden/expired track handling; no privileged fields escape the adapter.
2. One exact player-equivalent missile case and a contact with identification/uncertainty gates; distinct cosmetic `HitChanceDisplay` options must not accidentally override the documented public capability policy.
3. One bounded result with per-metric `Available`/`Unavailable`/`Unsupported`, including genuine zero no-escape, not-applicable, NaN/deviation, no-finite-LOS-limit and backend-withdrawn-score cases.
4. Same-session stale-version handling, cancellation/timeout, queue saturation and native-object lifetime; no UI-thread freeze.
5. Before/after idle-baseline state audit around each native call; no ammo/task changes, global RNG consumption or unintended targeting/reservation effects.
6. Cross-language Protobuf conformance, generic mock-provider tests, and existing world/order/regression tests. No modification to proprietary assemblies or deployment without opt-in.

## 7. Alternatives and approval questions

| Alternative | Assessment |
|---|---|
| Separate RPC per metric | **Reject for V1.** Repeats weapon/track resolution and expensive native inputs, multiplies thread crossings and risks internally inconsistent samples. |
| Add metrics to each `WorldSnapshot.Track` | **Reject.** Potentially quadratic per-weapon/per-track computation, forces evaluations even when not needed and conflates observations with a hypothetical shot. |
| Sea Power-specific “hover values” endpoint | **Reject.** Locks the external contract to localization/UI presentation and breaks as other backends or game versions evolve. |
| Track-estimate-only recomputation | **Not required.** The owner chose player-visible parity; replacing the native model would be less faithful. Could be a fallback *only if explicitly labeled as a different model*. |
| One query with typed, status-bearing metrics and separate discovery | **Recommend.** Honest partial support and a stable generic surface with bounded calculation cost. |

**Owner decisions requested:**

1. Approve one `AssessEngagement` plus one `DiscoverEngagementOptions` operation, with `Engagement.Api` contracts physically in `Domain.Api`?
2. Approve SI units in the API and status-bearing metrics, including explicit “no finite limit” representation?
3. Include the **numeric `effectivenessScore` as a backend-specific model score** in V1 (with model metadata), or expose only the qualitative rating until its interpretation is further validated?
4. Should uncertain/identified-contact gating mirror the native UI exactly by default, while allowing the owner-approved exception for purely cosmetic `HitChanceDisplay` modes?

**Gate D result:** **Design proposed, pending owner review**. Gates A–C remain partial, and the missing UI parity/side-effect attribution are implementation gates; no code/schema/RPC has been changed or approved by this document.
