# Classification integration review fixes

## Scope and policy

Task 024 addresses the existing findings on Symbology.Nato.Api PR #1,
SeaPower.Client PR #1 and TacticalDisplay PR #1. Work remains on
`feature/classification`; no replacement PR, main merge, game deployment or game
launch is part of this execution.

The owner's clarification is authoritative: absent `PrimaryPlatformRole` is a
valid final decision. No primary is inferred from capabilities, equipment, loadout
or hull subcategory. The reviewed ledger and generated INI are unchanged.
The original fixes left Domain.Api unchanged. The heading-semantics follow-up
below adds contract documentation only; wire properties and observer identities
remain unchanged.

## A. Submarine SIDCs — confirmed defect

Source: NATO APP-06, Edition E Version 1, Annex A Tables A-41–A-43 and A-50,
and common modifier illustrations in Table 1-3. The
[NATO-authored standard hosted by StudyLib](https://studylib.net/doc/28067209/app-06-ede-v1-e)
was inspected independently of the review allegation.

A-41 assigns `110100` to a general military submarine, with surfaced,
snorkelling and bottomed variants; it provides no attack/ballistic/cruise hull
subtypes. A-42's local sector-1 assignments are `00`, `02`, `13`–`19`.
Local `08`–`10` are unassigned. A-43 describes propulsion; none is inferred here.

All four domain submarine subcategories use `35/110100` without a mission when
primary is absent. Exact type labels remain intact. With an explicit primary,
common A-50 modifiers apply: ASW `151`, surface warfare `155`, strike `106`;
a ballistic-missile hull with explicit Strike/LandAttack uses `108` (BM).
This preserves a supported distinction without assigning a mission to a hull
with no primary. No cruise-specific common modifier is claimed. Common modifiers
carry their hundreds digit in SIDC position 21; common `108` is not local `08`.

Parser validation checks subsurface local assignment and applicability:
auxiliary/confidence require a submarine, anti-torpedo requires a torpedo, and
nonzero local propulsion codes require a submarine and assignment `01`–`13`.
Common extensions remain distinct. This is focused validation, not a complete
validator for every entity/common-modifier combination in APP-06.
The renderer removes invented A/B/G local meanings and composes common BM
through the existing modifier path.

Regression cases cover generic/attack/ballistic/cruise submarines, explicit and
absent primaries, exact identified and partially classified observations, full
local modifier ranges, and applicable common modifiers. Partial classification
never imports the catalog's primary mission.

## B. Contact kinematics — confirmed source mismatch

Authoritative reads are not categorically forbidden. The approved policy remains
parity with information available to a human player.

### Inspection method and version

Read-only ILSpy 11.1 inspection of installed
`C:/Games/Sea_Power_8_3/Sea Power_Data/Managed/Seapower-Scripts.dll`.
SHA256: `0FE6AAF2A6D9982E7E2EB41FD6035CB97C149773202C90821D71C00A9FDEB01A`.
Evidence was inspected in ignored `Docs/o/task024-inspection/`; proprietary
assemblies/decompiled source are neither edited nor committed. Folder naming
alone is not a verified product version. Findings apply to this assembly hash.

### Statically verified data flows

| Native path | Actual presentation source and gates | Adapter consequence |
|---|---|---|
| `SeaPower.Vehicle.CreateViewModel()` | Object view for player-controllable objects; otherwise constructs `SeapowerUI.ViewModels.ContactBaseViewModel(Vehicle)` | Faction observations follow contact semantics; own units remain authoritative and are excluded from that faction's observations |
| `ContactBaseViewModel(Vehicle)` direction | Always constructs `ObjectHeadingViewModel` from `atan2(Vehicle.UnityVelocityVector.x, z)`; no identification/class/dormancy check | Use displayed vector direction, not underlying hull heading |
| Same constructor's speed | Full 3D vector magnitude times `67.200066` metres per Unity unit and `1.94384` knots per m/s, for Vessel, Submarine, Biologic, Torpedo, Aircraft, Helicopter, Missile, ASROC, RBU and Bomb | Convert the same magnitude directly to SI; do not read `_velocityInKnots` |
| `Vehicle.UpdateFromECS()` | Copies object velocity, adds TMA adjustment when enabled and present, then raises `Updated` | Observer vector already includes native presentation's truth-backed behavior; preserve it, including zero |
| `MapUnitViewModel` position subscription / `VelocityPoint` | Same horizontal vector forms a normalized map direction tick | Nonzero horizontal vector supports course; zero supplies no physical course |
| Contact vertical switch / `ObjectAltitudeViewModel` | Aircraft, Helicopter, Missile, ASROC, RBU, Bomb bind `ObjectBase.Altitude` | Use this player-bound reactive value, feet converted to metres |
| Contact vertical switch / `ObjectDepthViewModel` | Submarine and Biologic display negative altitude as depth; Torpedo clamps above-water depth to zero | Preserve signed elevation; reproduce torpedo clamp |
| Contact vertical switch / `ObjectDepthBelowKeelViewModel` | Vessel shows terrain depth minus keel distance, not object elevation | Elevation Unknown; never re-label seabed clearance as elevation |
| `ObjectBase.OnFixedUpdate()` | Assigns reactive altitude from `getHeightInFeet()` | Authorized player-bound authoritative source, distinct from arbitrary object-height access |
| `Vehicle.PositionEstimate` | Fresh detected position or observer extrapolation; can be absent | Horizontal coordinates remain observer estimates; no truth fallback |
| `MapUnitViewModel.UpdateContactInfoLine3()` | Optional airborne map-label altitude uses `Location._height`; selected panel has its own altitude binding | Export uses information available in panel; not every native map-label altitude has that source |

No class/identification test surrounds contact kinematic bindings. Unknown,
partial and exact contacts therefore use the same mapping. Names, affiliation
and classification keep their independent gates. A missing native object
prevents constructing the contact panel; missing references produce no exported
kinematics. Invalid/nonfinite inputs remain Unknown.

Dormancy is transferred to `MapUnitViewModel.Dormant`; contact-panel subscriptions
have no dormancy gate. The adapter retains the current observer vector and
displayed vertical value without refreshing movement from truth. A retained
vector can be stale; this task adds no per-field freshness property.

### Corrections and retained behavior

`CaptureDisplayedKinematics` selects only panel sources and calls the pure
production mapping helper `DisplayedContactKinematics`. Underlying heading,
true scalar speed and true-velocity fallback are removed.

Zero is not missing: speed can be known zero; native displayed direction is north
(`atan2(0,0)`), while physical course remains Unknown. Pure vertical motion also
has no horizontal course. Missing observer latitude leaves geographic direction
and course Unknown: a grid angle cannot be accurately converted without latitude.
Scalar speed and permitted elevation may remain available without localization.
`NativeKinematicsMapping`'s latitude correction is preserved. Original UI numbers
show grid bearings; compare projected bearings, not numeric equality at oblique
headings away from the equator.

### Confidence and limits

**Verified statically:** classes, constructor branches, reactive bindings, vector
source, TMA adjustment, zero behavior and absence of class/dormancy gates.
Managed tests execute our mapping and existing provider scope/thread/session
behavior; they do not execute proprietary UI code.

**Owner-reported player-visible behavior:** earlier mission feedback establishes
that contact direction, speed and elevation are available before identity/type.

**Not live validated here:** Noesis view selection in every mission, TMA updates,
stale/missing-reference timing, player settings and every family's presentation.
Source inspection does not establish that these live cases passed. No new
player-information-policy decision is introduced.

### Heading semantics follow-up — 2026-10-17

Addresses [the remaining heading semantics comment](https://github.com/NavalCommand/SeaPower.Client/pull/1#issuecomment-6234009662).
The owner explicitly requires observation heading and speed to retain their
player-visible values and availability, including unidentified, partial, exact
and dormant contacts. Leaving heading Unknown merely because the source is
movement-derived would violate that requirement.

| Field | Meaning |
|---|---|
| Authoritative unit `Heading` | Actual platform/body orientation; unchanged |
| Observation `Heading` | Source-reported track heading; Sea Power derives its displayed heading from `Vehicle.UnityVelocityVector`, then the adapter converts the grid direction to a geographic bearing |
| Observation `CourseOverGround` | Reported horizontal movement direction from that same vector when nonzero |

The two observation fields are not independent measurements. Their equality is
not evidence of bow/nose orientation. `Exact::Known` describes availability of
the reported value; it does not independently verify physical orientation.
This distinction is now explicit in the generic Domain.Api observation header
and README, project architecture, adapter comments and SeaPower.Client README.
No simulator dependency, property, protobuf change or breaking migration is added.
Authoritative unit heading documentation and mapping remain unchanged.

The production changes are comments only. Source review reconfirmed that
`CaptureObservation` calls `CaptureDisplayedKinematics` before independent
classification resolution; neither that call nor the mapping has an
identification/dormancy gate. The native mapping still reads
`track.UnityVelocityVector`; it does not read native body heading, true scalar
speed or a replacement truth velocity. Zero-vector reported heading remains
known north, speed known zero and course Unknown. Missing object/latitude and
nonfinite vector behavior are preserved. Geographic correction and speed-leader
inputs are unchanged. No new native inspection or live-game result is claimed.

Regression additions execute the production pure mapping and protobuf round trip
for unknown, partial and exact observation classifications with active/retained
dormant-vector fixtures. They assert presence as well as values, known zero
heading/speed, vertical-only movement, differing authoritative body heading and
speed, unavailable speed display and NaN/infinite components on every axis.
Existing missing-object/localization and multi-latitude/oblique-bearing regressions
remain. The helper deliberately accepts neither classification/dormancy nor
authoritative heading/speed; fixture metadata is not a simulated native UI gate.
These tests protect mapping/serialization behavior; absence of native gates and
truth substitution is established by the source review above, not a live UI test.

Executed: `dotnet run --project SeaPower.Client/test/managed/ProviderTests.csproj --configuration Release`
and the same command with `--configuration Debug` — both passed on 2026-10-17.
This includes existing ownership, observer classification,
expiry, session and thread-boundary regressions. Changes to native contract headers
are comments only; native suites and runtime packaging were not rerun for this
follow-up. No dependency pins change because executable code and contracts are
unchanged. No deployment, game launch, main merge or review resolution is performed.

Published on the existing `feature/classification` branches:
Domain.Api `63109941b1982cffcdeaef9a9cd497b7a2eedfa4` (documentation only),
SeaPower.Client `e70e3fc570073901bd1f18ea9c13ec0e9f0b292c`
(comments, README and regressions).

## C. Map omission — confirmed defect

Strict `DomainConverter` errors remain, including known Other, invalid enum
values, unsupported primary-role pairs and mismatched definitions. Previously
TacticalDisplay skipped these objects; unsupported renderer measurements were
also discarded.

Internal production `SymbolPresentation` recovers at the presentation boundary
with standard `SymbolSet::Unknown`, entity/modifiers zero. No false dimension,
submarine identity, mission, propulsion or type is assigned. It preserves name,
authorized exact type label, affiliation/context/destruction, position, uncertainty
and kinematic amplifiers. Truly unknown classification remains an ordinary
unknown symbol without a failure diagnostic.

Known but unrepresentable classification, invalid data, mismatched definitions
and renderer limitations have explicit diagnostics in selected-object details.
The strict converter still returns its original error. Recovery does not correct
the domain object or silently accept a bad modifier.

`Measure` retries unsupported renderer details once with generic identification.
The same injected renderer draws it; the same `SymbolLayout` owns measurements,
sticky/adaptive placement, hover, clicks, identity, geographic dots and connectors.
No public rendering interface or parallel interaction subsystem was added.
An injected renderer must support the standard Unknown symbol. Missing font/UI
contexts and invalid presentation contexts are not classification failures.

Position policy is unchanged: units require position; observations require both
observer latitude and longitude. Unlocalized/partial observations stay in the
list without invented coordinates. Diagnostics appear for measured map objects;
original domain details remain accessible.

## Validation

Executed with the existing Windows/MSVC/Ninja/CMake infrastructure after the
final dependency pins were committed. Local source overrides remain ignored.

| Build/check | Result |
|---|---|
| Symbology.Nato.Api Debug | 32/32 tests passed |
| Symbology.Nato.ImGui Debug | 20/20 tests passed |
| TacticalDisplay Debug | 26/26 tests passed |
| TacticalDisplay Release | 26/26 tests passed |
| SeaPower.Client managed provider | Passed, including displayed-kinematics and geographic regressions |
| SeaPower.Mod managed runtime | Passed |
| Complete CommandCenter Debug application build and integration | Build succeeded; 2/2 tests passed |
| SeaPower.Mod Release runtime and harness packages | Rebuilt successfully; 30/30 manifest SHA256 entries matched |
| Primary-role inventory regeneration | 1002 entries reproduced byte-for-byte; missing/duplicate/stale review rows rejected |
| C++ formatting and whitespace | Changed handwritten C++ formatted with repository clang-format; git diff checks passed |

Domain.Api and Gateway.Api were not modified; their suites were not rerun.
Build.CMake was not modified. SeaPower.Mod and CommandCenter changes are
necessary dependency pins only. Runtime packages exclude proprietary game and
loader references, test projects, scenarios and harness assemblies by the existing
packaging checks. No live acceptance case above is claimed as passed.

Reproduction uses the existing configure directories:
`Symbology.Nato.Api/o/windows-msvc-debug`,
`Symbology.Nato.ImGui/o/windows-msvc-debug`,
`TacticalDisplay/o/windows-msvc-debug`, `TacticalDisplay/o/styling-release`,
`CommandCenter/o/wdb` and `SeaPower.Mod/o/windows-msvc-release`.
Run `cmake --build <directory> --parallel 8` and
`ctest --test-dir <directory> --output-on-failure --no-tests=error` for native suites.
Managed commands are `dotnet run --project SeaPower.Client/test/managed/ProviderTests.csproj --configuration Release`
and `dotnet run --project SeaPower.Mod/test/managed/RuntimeTests.csproj --configuration Release`.
The package targets are `NavalCommand.SeaPower.Mod.PackageRuntime` and
`NavalCommand.SeaPower.Mod.PackageHarness`.
Local execution logs are the existing `sticky-roles-build.log` and
`sticky-roles-tests.log` within those build directories,
`SeaPower.Client/o/review-managed-tests.log`, and
`SeaPower.Mod/o/sticky-roles-package-build.log`. Their filenames are inherited
build-helper output names, not evidence from a prior task execution.

## Modified repository revisions

| Repository | Commit |
|---|---|
| Symbology.Nato.Api | `326f5750ea20379df78fc978db30a9e8868b92b5` |
| SeaPower.Client | `f8188e45354add9484a1de4f931f167cf6579e24` |
| Symbology.Nato.ImGui | `9445e0cfc8cfea11c3f7491ca8c3f152bc55a8a9` |
| TacticalDisplay | `43808b144d2ba49774b2378fbfd6bb460c283d7a` |
| SeaPower.Mod | `bb0215c27a3c1b8b9b04de78954a8cfeceb8611a` |
| CommandCenter | `7bb09a3838824d130f975e5f416bd191a3420c20` |

Docs is identified by the Git commit containing this report; its final SHA is also
reported in the completion message. All commits target existing feature branches.



## Targeted manual acceptance — not executed

1. Start MinimalCombat/MajorCombat through the owner's normal launcher. Compare
   an unknown contact, then partial and exact identification in the original
   Tactical Display and CommandCenter faction view. Confirm available speed and
   altitude/depth, independently gated class/identity, and projected direction
   at an oblique course and nonzero latitude.
2. Change a TMA solution: direction/speed should follow the game panel/map tick,
   not target hull orientation. Stop a contact: known zero speed, no physical
   speed leader/course; native direction may read north.
3. Lose sensors and compare dormant contacts. Confirm no substitution of live
   true velocity. Check air altitude, submarine depth (including surfaced Kilo),
   torpedo depth and vessel depth below keel. The last must not become elevation.
   Missing-reference/position cases not safely reproducible remain unexecuted;
   do not modify game binaries or fabricate native tracks.
4. Use an unrepresentable object/provider fixture: verify Unknown symbol,
   affiliation, permitted labels and diagnostic. Hover/click for details; exercise
   label modes/richness/styles, scale, both decluttering modes and displaced dots.
5. Confirm multipurpose submarines have no mission with absent primary, Udaloy
   keeps ASW and explicitly strike-classified ballistic submarines show common BM.
   Partial observations must not gain catalog missions before exact identification.

## Source lifecycle

Promoted: focused SIDC validation, corrected conversion/rendering, panel mapping
and presentation recovery. Regression fixtures remain in owning test trees.
Disposable read-only inspection/build helpers stay ignored under `o/`.
No proprietary source, diagnostic plugin, runtime switch or harness dependency
is shipped. Production/harness staging remains separate and activation opt-in.
Historical reports have not been rewritten.
