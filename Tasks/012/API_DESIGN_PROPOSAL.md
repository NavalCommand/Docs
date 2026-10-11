# Task 012 — Engagement analytics API design proposal

**Status:** Earlier method-first Gate D sketch, **deferred**; Task 014 approves and implements the [type-first domain model](DOMAIN_MODEL_PROPOSAL.md), without approving these method surfaces. The operation names and request/response shapes in this document are **not approved or implemented**. Retained for historical context; do not implement from it before the domain concepts and equipment information boundaries are settled. Based on [STATIC_FINDINGS.md](STATIC_FINDINGS.md), [RUNTIME_FINDINGS.md](RUNTIME_FINDINGS.md), the existing NavalCommand public contracts and the owner's confirmation of ordinary player-visible engagement metrics. Gates A–C remain partially validated; this document does not upgrade their evidence status.

## 1. Decision and scope

**Recommend a single read-only `AssessEngagement` operation** for one friendly firing unit, one catalog-identified ammunition type and one observer-visible track. Return a typed assessment with independent metric availability; perform at most one bounded, game-thread-safe native calculation job per request. The platform requires **both a complete unit-class catalog and a complete weapon catalog**. A unit class may support multiple equipment/loadout configurations with nominal weapons and ammunition counts, all referencing stable catalog definitions. A separate **`GetUnitEquipment` observation query** inspects the actual known/estimated installed weapons and ammunition of a specific friendly unit **or observer-visible enemy track**. A distinct `DiscoverEngagementCapabilities` query advertises simulator-supported *analytics*, not units, weapons or current equipment. These are *conceptual names and shapes*, not a frozen schema.

This is **engagement analytics**, not a fire order, target designation or permission to engage. An assessment can be available when a shot is prohibited, no ammunition remains, the weapon is held, or an actual launch would be rejected. Existing order-eligibility and dispatch-time authorization remain authoritative.

**Information policy:** The Sea Power adapter may privately use native target `BaseObject` state to obtain **derived information ordinarily available to the same player/faction via the targeting interface**. It must require an observer-visible track and return no enemy `BaseObject` identity, raw true attributes or extra privileged derived values. The owner confirms that the investigated range/flight/envelope metrics are player-visible. Static inspection identified `HitChanceDisplay` `Full`, `Rough` and `Off` modes; the numerical score is a conditional presentation of the underlying effectiveness calculation. Cosmetic display settings need not limit a read-only public metric, but substantive visibility, identification and uncertainty conditions do.

The generic contract **must not depend on Sea Power preserving a particular calculation**. Per-metric `Unsupported` permits future adapters and versions to withdraw a metric without changing the protocol or inventing a replacement.

## 2. Contract ownership and transport

| Responsibility | Proposed owner | Reason |
|---|---|---|
| Abstract engagement request, result, metric availability and meanings | `Domain.Api`, within a small `Engagement.Api` interface area | Neither `Orders.Api` commands nor `World.Api` snapshots describe an on-demand pairwise analysis. No new repository required. |
| Complete unit-class and weapon/ammunition/launcher definitions, plus unit loadout/configuration variants | `Domain.Api` catalog domain (e.g. `Catalog.Api`) | Mostly static and content-versioned, with relational identifiers. Catalog units list their alternative stock fits without pretending that one fit is installed on every runtime instance. |
| Current equipment of a friendly unit **or an observable enemy track** | `World.Api` scoped equipment observation via `GetUnitEquipment` | Dynamic, observer-dependent, potentially incomplete and uncertain; references catalog identities when known. Not the class catalog and not analytics capability discovery. |
| Canonical catalog, inventory and engagement Protobuf representations and codecs | `Gateway.Api` | Keep the sole C++/C# wire schema source and the existing internal C ABI payload exchange. Add appropriate canonical messages without mixing static catalog with dynamic assessments. |
| External `GetUnitCatalog`, `GetWeaponCatalog`, `GetUnitEquipment`, `DiscoverEngagementCapabilities` and `AssessEngagement` RPC declarations | `Gateway.Grpc.Api` | Service definitions import the canonical payloads; do not duplicate their definitions. Keep the equipment query separate from class definitions and engagement assessment. |
| Generic provider forwarding, deadlines and owned results | `Gateway.Api` provider boundary plus `Gateway.Grpc` | The current `IProvider` is thread-safe I/O-side and must **never call simulator code** directly. |
| Native calculation/visibility gates | `SeaPower.Client` | Resolve observer tracks to native objects privately and evaluate on the game thread. `SeaPower.Mod` remains the composition root. |

The current `Domain.Api/Orders/Api/Request.h` carries `sessionId`, `entityId` and `basedOnVersion`; `World.Api/View.h` carries `sessionId`, `observerId`, `version` and `operationalSeconds`. The existing `World.Api/Track.h` has an opaque `id`, not a native object ID. Reuse these conventions without converting an assessment into an order or modifying the existing `Gateway.Api` `Waypoint` wire contract.

Respect `Docs/CODE_STYLE.md`: one independently useful public type per header; examples below are conceptual, not an instruction to place all types into one file.

## 3. Proposed operational queries and catalogs

The **catalog and equipment surfaces are platform requirements and a separate implementation milestone**, not additional native engagement calculations to squeeze into Task 012. Their conceptual identities and relationships are described here because `AssessEngagement` depends on them. Implementing all catalogs and equipment observations is **not** part of a narrow Task 012 engagement-adapter assignment.

### 3.1 GetWeaponCatalog — complete, versioned simulator content

**Separate cross-domain API, not a Task 012-only discovery workaround.** Retrieve the **complete catalog of weapon definitions known to the current simulator/content installation**, including weapons not currently carried, armed or available to the selected faction. The catalog must be usable independently of a scenario and must not be restricted to the current player's equipment or ammunition stock. Ordinary static definitions are not a license to reveal secret current enemy loadouts or live state.

Conceptual catalog contents, subject to the backend's actual data: weapon-system/launcher classes, ammunition/munition definitions, their relationships, stable catalog keys, display names, supported target categories and nominal published properties (such as nominal/minimum ranges, guidance type or compatible systems). Distinguish a **launcher definition** from a **munition definition**; their identities and many-to-many compatibility may differ. Enumerate all source-known definitions even if some optional *attributes* cannot be populated. Do not invent unknown nominal values or mix nominal catalog ranges with target/geometry-dependent engagement envelopes.

The catalog should carry **content revision/hash and clear completeness reporting**, support bounded pages/snapshots if large, and invalidate stale keys when content changes. A backend unable to enumerate its full catalog must **report a coverage/completeness limitation**, not silently label a partial list complete. How to version source IDs across mods and updates needs an explicit implementation decision. Catalog schemas and collection mechanics belong to a dedicated catalog implementation task; this proposal does not purport to specify the entire weapon data model.

### 3.2 GetUnitCatalog — complete classes and possible configurations

This is a **complete, versioned catalog of simulator-known platform/unit classes**, including friendly, neutral and opposing classes whether or not an instance currently exists in a mission. The primary catalog entry identifies a **unit class/type**, not a live unit instance. It may contain general specifications (category, published dimensions/performance, roles and other supported class attributes), and links to supported equipment/loadout **configuration variants**. A unit class may have multiple variants; an unavailable or unknown field is not a reason to omit the class.

Each unit-class configuration may describe:

- Stable **configuration ID** scoped to its unit-class ID and content revision; human-readable configuration/loadout name and role if provided.
- **Nominal installed weapon/launcher system fit**, with quantities or slots and references to definitions from `GetWeaponCatalog`.
- **Normally loaded munition/ammunition types and nominal quantities** (including magazine stock separately from launcher-loaded rounds where the source distinguishes them), again referencing the weapon catalog.
- Constraints/compatibility and default/typical status **only when the underlying data explicitly establishes them**. A list of variants is not evidence of one universal default configuration.
- Availability/coverage metadata for missing or incompletely enumerated configurations. Do not silently infer a loadout from weapon compatibility alone.

Example, schematic only, **not** an assertion about installed F-14A configurations:

```text
UnitClass {
    classId: "aircraft/F-14A"
    configurations: [
        { configurationId: ".../air-to-air",
          standardWeaponFit: [...weaponCatalogIds...],
          nominalAmmunition: [{munitionCatalogId: "...", quantity: 4}, ...] },
        { configurationId: ".../alternative",
          standardWeaponFit: [...],
          nominalAmmunition: [...] }
    ]
    defaultConfigurationId: optional  // only if established by simulator data
}
```

**Catalog variant vs runtime instance:** A mission or editor may override nominal equipment; aircraft may be armed at launch, carry a chosen payload and expend ammunition; launch systems and magazines may be damaged or reloaded. Those changes belong to `GetUnitEquipment`, never to a mutable catalog definition. Not all scenario-specific modifications necessarily become new permanent catalog variants. Record catalog revision and per-configuration completeness. **Unit class, configuration and weapon ID mappings must be mutually consistent across catalogs.**

Sea Power investigation already identified candidate sources in Task 001 (`UnitMetadataProvider.UnitData`, `UnitIdMap`, `AmmunitionData`, launcher-compatible ammunition) and Task 003 (`FlightDeck.Loadout` definitions and flight-deck aircraft loadout choices). Task 003 explicitly found that hosted aircraft stock is aggregated by class/squadron and does **not** represent individually pre-equipped airframes. These are starting points; **complete unit and configuration enumeration has not yet been validated**.

### 3.3 GetUnitEquipment — actual observer-scoped equipment state

A separate read-only operation accepts **exactly one** subject selector: a controlled/friendly `unitId` **or** an observer-visible `trackId`; plus session and optional observation-version condition. A track ID must be resolved under the current observer's plotting table, not by unrestricted native object lookup. Unknown/expired/foreign subjects fail without leaking information.

Conceptual query/result:

```text
EquipmentQuery {
    sessionId
    oneof subject { friendlyUnitId | observedTrackId }
    optional basedOnObservationVersion
}
EquipmentObservation {
    subject, observerId, observationVersion, sampledAtOperationalSeconds
    catalogRevision
    identifiedClassId                  : Knowledge<UnitClassId>
    identifiedConfigurationId          : Knowledge<ConfigurationId>
    installedSystems                   : ObservedCollection<InstalledWeaponSystem>
    ammunition                         : ObservedCollection<AmmunitionStock>
    // Each item may reference a weapon/munition catalog ID, observed quantity,
    // magazine/launcher location and readiness if legitimately known.
}
Knowledge<T> {
    status: Confirmed | Estimated | Unknown    // distinct from backend Unsupported
    optional value
    optional confidence, observedAt, source
}
ObservedCollection<T> {
    coverage: Complete | Partial | Unknown
    entries: list<Knowledge<T>>
}
```

Friendly-side data should provide all **actually verifiable** equipped launchers, mounted weapon systems, loaded rounds, magazine contents, remaining ammunition and readiness/health, with status where a backend does not observe some details. Avoid double counting launcher-loaded and magazine-stored rounds. `InstalledWeaponSystem` and `AmmunitionStock` are explanatory placeholders, not an approved wire schema.

For an enemy track, weapon identities, quantities, current configuration and even class may be **unknown, estimated, partially observed or stale**. An identified unit class may permit an analyst to look up **possible standard configurations in `GetUnitCatalog`**, but **this must not be reclassified as confirmed equipment or current ammunition**. If an observation is inferred from an identified class/configuration, label it explicitly as a catalog-based **hypothesis** with provenance; ideally return such hypotheses separately rather than in the list of physically observed installed systems. An empty list with `Complete` means confirmed none; an empty list with `Unknown` means the API lacks information, **not that the target is unarmed**.

`Unsupported` describes a backend's inability to inspect a *kind of equipment attribute*; `Unknown` describes absent current observation; `Estimated` describes an inference and `Confirmed` a substantiated observation. These axes must not be collapsed into one enum. Preserve observation time and sources; do not fabricate confidence numbers or unverified sensor abilities. A track equipment query **does not trigger sensing, improve identification or reveal hidden loadouts** merely because Sea Power internally knows the exact values.

**Player-visible parity:** The owner approved exposing derived outputs that the normal player can access. That does not automatically authorize **all raw enemy weapons, ammo counts or actual fitted loadouts** from `BaseObject`. Every enemy equipment field needs its own player-visibility/identification evidence. If Sea Power's UI exposes an equipment detail normally, it may be returned; otherwise use a supported unknown/withheld status.

### 3.4 DiscoverEngagementCapabilities — backend capability metadata

**Input:** current session (or backend metadata context); observer scope if capabilities vary with side. This is **not** a per-unit ammunition discovery query.

**Result:**

- Backend/simulator build identity and analytics capability revision.
- Metric kinds and model families the adapter generally implements (e.g. no-escape range only for supported missile/air-target cases), with supported/unsupported status or qualified applicability.
- Declared output semantics and model version, where known; not a list of currently equipped weapons, actual stock, or currently valid firing orders.

This discovery describes **what analytic operations the simulator supports in principle**. The separate `AssessEngagement` response describes whether each metric can be computed **right now** for a specific shooter/ammunition/target. A simulator might support the weapon catalog but no engagement analysis, or support some analyses for only certain weapon families. A future game update may withdraw a metric without changing catalog identifiers or the generic API.

### 3.5 AssessEngagement

Conceptual request:

```text
EngagementAssessmentRequest {
    sessionId
    shooterUnitId       // friendly, controlled unit
    ammunitionCatalogId // identifies a munition in GetWeaponCatalog; GetUnitEquipment can establish actual shooter inventory
    targetTrackId       // opaque, visible in this observer's view
    optional basedOnObservationVersion
}
```

Conceptual response (names are provisional; units explicitly part of their meanings):

```text
EngagementAssessment {
    sessionId, observerId, shooterUnitId, ammunitionCatalogId, targetTrackId
    observationVersion, sampledAtOperationalSeconds
    catalogRevision, modelIdentifier, modelRevision

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
| Unknown or non-controlled shooter, invisible/expired target track, or nonexistent catalog ammunition ID | `NOT_FOUND` without disclosing foreign-unit/hidden-track identity; optional stable safe reason |
| Known catalog ammunition not associated with the friendly shooter | Distinguish unsupported shooter/ammunition combination from a missing catalog ID; reject with a stable non-leaking reason, or support a clearly labeled hypothetical-only assessment if explicitly designed. Never imply that catalog presence proves installed ammunition. |
| Session mismatch or rejected observation precondition | `FAILED_PRECONDITION` with `stale_session` or `stale_observation` |
| Provider not ready, timed-out game-thread job | `UNAVAILABLE` (with bounded retry guidance if appropriate) |
| Admission/queue limit reached | `RESOURCE_EXHAUSTED` |
| Valid analysis with one unavailable metric | **Successful assessment** containing that metric's status; do not reject the entire query |

## 5. Recommended metric definitions

Use **metres and seconds** in canonical wire/domain interfaces, consistent with existing `World.Api` and `Gateway.Api` SI units. Convert to Nmi/feet only in a client UI. Public values need clear numerical ranges and finite-value validation.

| Candidate V1 field | Meaning and Sea Power mapping | Availability and qualifications |
|---|---|---|
| `currentHorizontalDistanceMeters` | Current horizontal shooter-to-target separation used for kinematic/no-escape comparisons; native transforms. | Player-derived result, **not** a disclosure of raw opponent position. Do not silently substitute slant distance. |
| `minimumLaunchRangeMeters` | Selected ammunition's nominal minimum boundary, `_launchRangesInUnity.x`. | Nominal/static ammunition data can also appear in the complete catalog; repeating the validated value in an assessment is convenient but must not contradict the catalog. Zero is meaningful if confirmed. |
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

**Not included in V1:** fire orders or explicit fire authorization; raw target properties; independent radar-horizon annotations and their misleading fixed “20 ft target” text; wake-homing torpedo preview geometry, safety-offset “Hit Chance” categories or ad hoc predictive curves; bulk all-target assessment endpoints. The **full weapon catalog is included in the platform's operational API plan**, but its detailed definition and implementation are a separate cross-domain task, not part of the engagement-calculation probe. These may be separately added after evidence and a specific use case.

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

**Other operational prerequisites:** provide stable complete unit-class/configuration and weapon catalog IDs/revisions, plus separate observer-authorized equipment observations for friendly units and enemy tracks; map the selected ammunition to the shooter's current installed fit without confusing class loadouts with runtime inventory; resolve side-scoped track ID to `BaseObject` only within the adapter and only while track eligibility holds; avoid leaking identities in logs or errors; ensure the game-thread job and provider-owned response cannot outlive the target/weapon; distinguish track loss and stale query; bound main-thread cost; do not use GUI-selection state or mutate cursor/attack order. Never deploy the Task 012 diagnostic DLL as a production dependency. The previous report says `BepInEx/plugins/Task012EngagementProbe.dll` remains in the local game installation; remove it and verify teardown before any further development run.

### Acceptance checks for a later implementation

1. Valid/invalid/foreign unit and hidden/expired track handling; no privileged fields escape the adapter.
2. One exact player-equivalent missile case and a contact with identification/uncertainty gates; distinct cosmetic `HitChanceDisplay` options must not accidentally override the documented public capability policy.
3. One bounded result with per-metric `Available`/`Unavailable`/`Unsupported`, including genuine zero no-escape, not-applicable, NaN/deviation, no-finite-LOS-limit and backend-withdrawn-score cases.
4. Same-session stale-version handling, cancellation/timeout, queue saturation and native-object lifetime; no UI-thread freeze.
5. Before/after idle-baseline state audit around each native call; no ammo/task changes, global RNG consumption or unintended targeting/reservation effects.
6. Cross-language Protobuf conformance, generic mock-provider tests, and existing world/order/regression tests. Validate catalog ID/revision mapping, multiple loadout variants for one unit class, and weapons/classes not present in the current friendly force; test friendly actual stock separately from catalog nominal fit and enemy track unknown/estimated/partial coverage. Capability discovery must **not** enumerate ammunition or equipment. No modification to proprietary assemblies or deployment without opt-in.

## 7. Alternatives and approval questions

| Alternative | Assessment |
|---|---|
| Separate RPC per metric | **Reject for V1.** Repeats weapon/track resolution and expensive native inputs, multiplies thread crossings and risks internally inconsistent samples. |
| Add metrics to each `WorldSnapshot.Track` | **Reject.** Potentially quadratic per-weapon/per-track computation, forces evaluations even when not needed and conflates observations with a hypothetical shot. |
| Sea Power-specific “hover values” endpoint | **Reject.** Locks the external contract to localization/UI presentation and breaks as other backends or game versions evolve. |
| Track-estimate-only recomputation | **Not required.** The owner chose player-visible parity; replacing the native model would be less faithful. Could be a fallback *only if explicitly labeled as a different model*. |
| Use an analytics discovery endpoint as either catalog | **Reject.** Current ammunition choices and analytic capabilities are not the complete unit/weapon catalogs and do not describe loadout alternatives. |
| Infer a track's exact current weapons and ammunition from an identified unit class | **Reject.** A nominal class/loadout listing is not evidence of which equipment the observed unit actually carries or how many rounds remain. |
| One query with typed, status-bearing metrics; complete catalog; independent capability discovery | **Recommend.** Distinct operational responsibilities, honest partial analytics support and bounded calculation cost. |

**Owner decisions requested:**

1. Confirm the distinct public surfaces **`GetUnitCatalog`, `GetWeaponCatalog`, `GetUnitEquipment`, `DiscoverEngagementCapabilities`, `AssessEngagement`**, with the catalogs and equipment query scoped to a separate foundational implementation task? The unit catalog must retain multiple normal/allowed fits per class; enemy equipment responses must represent uncertainty.
2. Approve SI units in the API and status-bearing metrics, including explicit “no finite limit” representation?
3. Include the **numeric `effectivenessScore` as a backend-specific model score** in V1 (with model metadata), or expose only the qualitative rating until its interpretation is further validated?
4. Should uncertain/identified-contact gating mirror the native UI exactly by default, while allowing the owner-approved exception for purely cosmetic `HitChanceDisplay` modes?

**Gate D result:** **Design proposed, pending owner review**. Gates A–C remain partial, and the missing UI parity/side-effect attribution are implementation gates; no code/schema/RPC has been changed or approved by this document.
