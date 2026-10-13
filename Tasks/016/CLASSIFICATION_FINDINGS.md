# Sea Power classification model investigation

**Date:** 2026-10-13

**Scope:** Investigation-only. No production code, API, schema, tests, or proprietary game files were changed; this report is being added to the Docs archive at the user's request.

**Verdict:** **Requires structural changes** before NavalCommand can represent the relevant Sea Power taxonomy and source roles/loadouts reliably.

## Executive assessment

The model separates physical classification, configuration, operational behavior, and observer knowledge, which is a sound foundation. Its current values and data sources are not sufficient.

1. Important native families lack a sourceable subcategory: generic surface vessels, missiles, torpedoes, several deployed weapon families, and biological entities. Conversely, cruise/ballistic missile and heavy/light torpedo splits are not native type distinctions.
2. Sea Power stores platform roles on type metadata (AI.Role → _unitRoles), not on Loadout. It uses these roles for flight-deck eligibility and AI selection. Loadout::Roles therefore cannot be populated faithfully. A type-level normalized role list is justified by this evidence.
3. A selected loadout is configuration identity. Remaining ammo, damage, system operation, and spawned weapons change separately. Native loadout names are scoped to a type/content revision.
4. Observer contacts contain legitimate estimates and a pointer to the true object. Vehicle.Class is generally based on sensor-classification bits, but the wrapper uses the true object INI name for weapons or forced identification. The current NavalCommand adapter copies populated track.Class without an Identified gate. This is a verified high-risk path; actual faction-visible weapon UI behavior needs runtime confirmation.
5. APP-6 is a symbology model, not the same hierarchy. Standard identity, status, dimension, function and modifiers are separate. No APP-6 numeric identifiers are proposed.

Keep stable physical family, type-level platform purpose, weapon traits, selected configuration, live inventory, current mission, and observer knowledge as separate facts.

## Baseline and inspected build

- Domain.Api branch feature/classification, HEAD e2bc69e095e0a4110c6af983890cef9cf82d5b78 (requested baseline).
- Docs was clean on main. Read Docs/AGENTS.md, ARCHITECTURE.md, MODULE_STRUCTURE.md, CODE_STYLE.md, MANAGED_BUILD.md, LOCAL_SOURCES.md, and DEVELOPMENT.md.
- Baseline Windows configure/build succeeded with the Visual Studio developer environment and bundled CMake 4.3.1; PATH CMake 3.28.1 was too old. Build completed 96 steps; ctest --test-dir o/windows-msvc-debug --output-on-failure passed **61/61**. CMake warned that the manually specified CMAKE_TOOLCHAIN_FILE was unused; it did not block the build. Outputs are in ignored Domain.Api/o/.
- Sea Power installed at C:\Games\Sea_Power_8_3; changelog reports 0.8.3 Build 261001 Public Release (2026-10-01), Unity 6000.0.67f1.
- Seapower-Scripts.dll version 0.0.0.0, size 8,647,680 bytes, SHA-256 0FE6AAF2A6D9982E7E2EB41FD6035CB97C149773202C90821D71C00A9FDEB01A. Assets.dll SHA-256 01387C3F2FAB6A125138C079364137004BD4244CCEB34BDBE605A43083DFE213. Scripts hash matches Tasks 001–003, 012 and 013.
- Static review reused local ILSpy 11.1.0.9782 output from Task 001 after hash verification and inspected installed original INIs. No decompiled source was copied. Sea Power was not launched.
- Original INI file counts: aircraft 227, vessels 510, land_units 376, biologic 6, ammunition 444. These are file counts, not counts of playable or trackable units.
- Prior evidence: Docs/Tasks/001/SEAPOWER_FINDINGS.md; 003/SEAPOWER_AIROPS_FINDINGS.md; 012/STATIC_FINDINGS.md and RUNTIME_FINDINGS.md; 013/STATIC_FINDINGS.md, RUNTIME_FINDINGS.md, DOMAIN_MAPPING.md; 015/REPORT.md.

## Sea Power entity inventory

ObjectBase.isUnit() includes Vessel, Submarine, Aircraft, Helicopter, LandUnit, and Biologic. WeaponBase families are separate. ObjectBase.ObjectType is a runtime discriminator, not the whole semantic taxonomy: it has no CruiseMissile, BallisticMissile, Mine, or Sonobuoy value. Decoy can be a type override, not a CLR subclass.

| Concept | Native identity/evidence | Current Domain fit | Assessment |
|---|---|---|---|
| Fixed-wing aircraft | Aircraft, ObjectType.Aircraft | Aircraft / FixedWingAircraft | Supported when mapped from native family. |
| Helicopter | Helicopter, ObjectType.Helicopter | Aircraft / RotaryWingAircraft | Supported. |
| Tilt-rotor | No dedicated family/role; VTOL fields exist | None reliable | VTOL does not prove tilt-rotor; defer exact subtype. |
| Surface combatants, small craft, auxiliaries | Vessel; AI.Role includes Carrier, CVS, CVL, FAC, ASuW variants, AAW, ASW, NGFS, RAS, Transport, Landing | SurfaceVessel, but no generic subcategory | Add generic surface-vessel fallback. Do not infer cruiser/destroyer/frigate/corvette/patrol from names; roles are not hull classes. |
| Merchant/civilian vessels | Vessel; roles Merchant, Passenger, SmallCivilian | SurfaceVessel / MerchantVessel | Partial, only with explicit role; not exhaustive hull classes. |
| Submarines | Submarine; roles SS, SSN, SSG, SSGN, SSB, SSBN | Submarine plus subtype candidates | Some subtypes map from explicit roles; keep generic fallback for broad/mixed cases. |
| Guided missiles | One Missile family; ammunition Type=Missile; target/guidance/launch/loft fields | Missile, but no generic subcategory | Add generic Missile. No reliable cruise/ballistic native type distinction. |
| Torpedoes | Torpedo; MOSS remaps to Torpedo subtype | Torpedo, but no generic subcategory | Add generic Torpedo. No heavy/light enum or universal threshold. |
| MOSS acoustic decoy | Ammunition MOSS → Torpedo + subtype MOSS | Current SubsurfaceDevice candidate | Physically a propelled torpedo; decoy behavior is separate. |
| ASROC/RBU | Separate ASROC and RBU WeaponBase families; MLRS remaps to RBU subtype | No current match | Missing. Add only if deployed objects/contact tracks are in scope. |
| Bomb, depth charge, fuel tank, paratrooper | Bomb object with ammunition subtypes | Sonobuoy is current candidate | Not every ammo definition is a unit type. Distinguish deployed contacts from loadout resources. |
| Rockets/projectiles | UnguidedRocket → AerialRocket; separate Projectile | No current match | Missing physical families; agent/contact relevance needs validation. |
| Naval mine | Vessel + AI.Role=SeaMine + [SeaMine]; Vessel.ExplodeMine | Mine / NavalMine | Semantic mapping is sound but requires role/config recognition. IsTrackableObject excludes SeaMine. |
| Noisemaker | Noisemaker : WeaponBase; ObjectType.Noisemaker | SubsurfaceDevice / Noisemaker | Physical type is explicit; fixed dimension is questionable. Trackability excludes it. |
| Other decoys | UnitRoles.Deco, optional ObjectType.Decoy override, AI.DisguisedAs; examples include vessel-family raft/ship decoys | Role::Decoy only | No single physical family. Physical and perceived identity can differ. Deco objects are excluded from ordinary trackability. |
| Sonobuoy | Ammunition Sonobuoy remaps to Bomb + subtype; Bomb creates sonar at _sonarDepth and deploys on water impact | SurfaceDevice / Sonobuoy | Appropriate for buoy body; sensor component is below surface. It is a spawned object with sensing capability. Faction-visible contact still needs runtime verification. |
| Other sensors | SensorSystem components and catalog definitions | Equipment capability | No general standalone sensor-object family found. |
| Land equipment/installations | LandUnit; LandUnitSubType: AirBase, Installation, MobileUnit, AAA, SAM, MissileSite, Radar, Port, Bridge, OilRig | LandEquipment / LandInstallation | Partial; current enum misses generic and several native functions. Civilian status is inferred from a civ_ filename prefix, not a typed subtype. |
| Biological entities | Biologic, included in isUnit(), six original configs | No current category | Missing. Agent control vs. environmental/contact-only needs runtime confirmation. |
| Chaff, wake, AI marker | ChaffCloud, WakeBubble, AI_Marker | No current match | Likely transient effect/internal marker; do not make unit types without contact evidence. |
| Space object | No Sea Power family found | SpaceObject | Simulator-independent reservation only. |

### Trackability is not existence

ObjectBase.IsTrackableObject admits some controlled WeaponBase objects but excludes Noisemaker, ObjectType.Decoy, UnitRoles.Deco, and UnitRoles.SeaMine. TerrainManager uses it when adding objects to world chunks. Existence as a simulated object therefore does not prove ordinary faction contact visibility; this exclusion also does not prove that every other weapon is always detectable.

Sonobuoys and MOSS show why one broad device bucket is misleading: the buoy is a Bomb subtype with attached sensing; MOSS is a torpedo subtype. Model physical family and effect separately.

## Current Category/SubCategory audit

GetCategory(SubCategory) is the only category mapping and GetDimension(Category) fixes one dimension per category. This avoids redundant fields, but each authoritative type needs a sourceable subcategory or generic fallback.

| Current Category | Finding |
|---|---|
| Aircraft | Good broad family; fixed/rotary sourceable, tilt-rotor unsupported. |
| Missile | Good broad family; Air fits an airborne missile instance, not launch domain or target. |
| SurfaceVessel | Good family; generic subcategory is missing, forcing false precision. |
| Submarine | Good family; needs generic fallback and validated role mapping. |
| Torpedo | Good physical family; heavy/light split unsupported. |
| Mine | Good semantic family, but native is Vessel + SeaMine and excluded from ordinary trackability. |
| LandEquipment | Good parent; needs broad fallback and MissileSite/Port/Bridge/OilRig cases. |
| LandInstallation | Good parent; civilian/military split is not typed. |
| LandUnit | No current SubCategory maps to it: unreachable/redundant. Remove or define purpose. |
| SpaceObject | No Sea Power mapping. |
| SurfaceDevice | Sonobuoy body fits; sensor element is separately subsurface. |
| SubsurfaceDevice | Overloaded: MOSS is a torpedo; Noisemaker position can vary by vertical state. |

| Current SubCategory group | Native support/confidence | Recommendation |
|---|---|---|
| FixedWingAircraft; RotaryWingAircraft | Aircraft/Helicopter families, high | Keep. |
| TiltRotorAircraft | No dedicated family/role, low | Defer/remove unless curated source exists. |
| CruiseMissile; BallisticMissile | Single Missile family; _ballisticLoft is trajectory input, high confidence mismatch | Generic Missile; flight profile in weapon metadata. |
| AircraftCarrier | Carrier/CVS/CVL roles, medium-high | Keep with explicit crosswalk. |
| Cruiser, Destroyer, Frigate, Corvette, PatrolVessel | No native hull enum, high | Generic SurfaceVessel unless stable source metadata exists. |
| AmphibiousVessel; AuxiliaryVessel | No direct subtype; Landing/Transport/RAS are not hull classes, medium-high | Generic vessel plus role/capability. |
| MerchantVessel | Merchant/Passenger/SmallCivilian roles, medium-high | Keep only with explicit role mapping. |
| AttackSubmarine | SSN explicit; SS broad, medium-high | Keep for validated SSN; generic fallback. |
| BallisticMissileSubmarine; CruiseMissileSubmarine | SSBN/SSB and SSGN/SSG, medium | Keep only after semantic validation; generic fallback. |
| HeavyweightTorpedo; LightweightTorpedo | No native classes, mass only, high | Generic Torpedo; no arbitrary cutoff. |
| NavalMine | SeaMine role/config, high | Keep. |
| Noisemaker | Explicit native class, high | Keep identity; reconsider fixed dimension. |
| SelfPropelledAcousticDecoy | No separate family; MOSS is Torpedo, high | Torpedo plus decoy/deception property. |
| Sonobuoy | Explicit subtype/deployment, high | Keep as surface buoy; model sensor separately. |
| GroundVehicle | MobileUnit, medium | Broad fallback only. |
| SurfaceToAirMissileLauncher | SAM/MissileSite, medium-high | Use when subtype is explicit. |
| AntiAircraftGun; Radar; Airfield | AAA/Gun; Radar; AirBase/Airfield, high | Keep when sourced explicitly. |
| MilitaryInstallation; CivilianInstallation | Installation; civilian flag inferred from civ_ name, low-medium | Generic LandInstallation unless stable typed metadata exists. |

## Roles and loadouts

### Native roles vs. current missions

SimpleObjectParameters.InitializeSimpleParameters parses AI.Role into ObjectBaseParameters.UnitRoles; deployed parameters copy that list. Native Loadout has no Roles member. FlightDeckAircraftSupportPoint.CheckSupport and AI selection use type roles such as Fighter, Bomber, AEW, EW, ASW, Recon, and MPA. VehicleTypeOnBoard separately stores loadout alternatives.

FlightDeckTask.MissionType values (CAP, Intercept, BombStrike, MissileAttack, SEAD, Jamming, AEW, Recon, MPA, SearchAndDestroyASW, SAR) are assigned operations, not design roles. Task 003 confirms hosted inventory is aggregated by type/squadron and selected fit is passed at launch.

Native UnitRoles include None, Deco, SeaMine, SmallCivilian, Merchant, Passenger, Transport, Landing, Minesweep, RAS, Spy, FAC, ASuW, ASuW_VLR, ASuW_Gun, NGFS, AAW, ASW, Carrier, CVS, CVL, SS, SSN, SSG, SSGN, SSB, SSBN, Airliner, Fighter, Bomber, HeavyBomber, Recon, AEW, EW, MPA, ESM, SEAD, Attack, Targeting, SAR, Airfield, Port, Gun, SAM, ASM, Radar, Target, and LifeRaft.

### Domain role crosswalk

Mappings are candidates for curation, not exact synonyms.

| Domain Role | Closest native value(s) | Assessment |
|---|---|---|
| AirSuperiority | Fighter | Approximate; broader fighter role. |
| AirDefence | AAW | Good candidate, validate use. |
| Strike | Bomber, HeavyBomber, Attack, SEAD | No single equivalent. |
| LandAttack | NGFS, SEAD, Attack / weapon target data | No direct platform role; NGFS is narrower. |
| SurfaceWarfare | ASuW variants, FAC, NGFS | Capability family, not hull type. |
| AntiSubmarineWarfare | ASW; MPA specialization | ASW explicit. |
| MineWarfare | Minesweep | Mine-laying role not found. |
| Reconnaissance | Recon; contextual Spy/ESM | Recon closest; others differ. |
| MaritimePatrol | MPA | Strong candidate. |
| AirborneEarlyWarning | AEW | Strong candidate. |
| ElectronicWarfare | EW | Strong; ESM does not automatically imply active EW. |
| Transport | Transport, Landing | Candidate; Landing semantics need validation. |
| AerialRefuelling | None | Do not map RAS: replenishment at sea. |
| Surveillance | Recon, MPA, ESM/sensors | No unique platform role. |
| Decoy | Deco | Behavior/trackability marker, not a general design role. Prefer deception capability/identity. |

A type may have multiple native roles. The current GetRoles(type) union over Loadout::Roles has no native source. Move normalized roles to Units::Api::Type; do not duplicate them onto each loadout or infer them from weapons. Keep current missions/orders separate.

### Loadout identity and lifecycle

| Question | Finding |
|---|---|
| Default on every type? | No universal guarantee. Flight deck may create Default for aircraft with no alternatives; distinguish generated fallback from source-authored fit. |
| Fixed vessel/sub as one loadout? | Yes, normalized when native default/variant exists. Some multi-loadout vessels/submarines are synthetic @loadout rows; normalize under parent type. |
| Aircraft loadout change? | Selected at preparation/launch; full recovery/rearm/variant lifecycle not proven. |
| Selected variant retained? | Yes, statically: ObjectBaseParameters._loadoutVariant is populated on spawned aircraft from selected name. Resolve within parent type/current content. |
| Explicit ID or reconstructed? | Name is explicit when populated, but not a global ID. Never reconstruct selected configuration from remaining ammo. |
| Name uniqueness? | Keyed within type (case-insensitive); not global. |
| Different names, same equipment? | Not ruled out; exhaustive scan not performed. |
| Equipment changes separately? | Yes: firing/reload/rearm, damage, and system state alter inventory/capabilities independently. |
| Spawned weapons linked to loadout ID? | No persistent selected-loadout association found. |
| Stable IDs across content updates? | No: native integers are cache indices; keys are folder/INI plus type-local loadout name. Scope IDs to content revision. |

Units::Api::Instance::CurrentLoadout can represent an authoritative selected configuration when _loadoutVariant resolves to a type-scoped ID. It must not mean remaining stores. Do not turn missing variant data into a known empty loadout. Observer Exact<LoadoutId> remains unknown unless legitimate faction mechanics reveal the selected fit; platform class or one observed weapon does not prove the full configuration.

## Observer classification and leak risk

Vehicle is a side-specific plot-table wrapper but retains Object/BaseObject references to authoritative ObjectBase. It exposes estimates alongside truth-bearing RealUnityPosition and UnityVelocityVector. Task 013 observed an identified enemy platform class (wp_mig-23mld) and position estimate; it did not read enemy weapons/loadout. The inspected UI gates class text on Identified; the enemy information page did not select its true loadout.

ClassInformation has candidate bits plus overrideIdentification, _preventTaskforceAnalysis, and isFalse. Vehicle.TransferFromComponents resolves Vehicle.Class when there is one class bit; multiple candidates do not become parent category/subcategory in this wrapper. For override identification or a weapon, it instead uses Object.getObjectIniName(). Vehicle.Identified follows whether Class has a value. False/disguise paths exist, but this wrapper does not expose a partial hierarchy.

| Observer datum | Evidence and safe interpretation |
|---|---|
| Position/uncertainty | Use PositionEstimate/ellipse; never use Object.Position/RealUnityPosition fallback. |
| Side | Use populated observer-side Vehicle.Side only. |
| Detecting sensor | Vehicle.Type/DetectingSensors describes observer detector, not target inventory. |
| Exact platform type | One class bit yields Vehicle.Class; Task 013 observed an identified enemy platform. Map only after faction-safe gate and stable ID resolution. |
| Partial category/dimension | Candidate bits may exist, but Vehicle.Class exposes no parent. Not faithfully available through inspected wrapper. |
| False/disguised identity | isFalse, override and AI.DisguisedAs mean true object type cannot substitute for perceived class. |
| Weapon class | Wrapper uses true object INI name for weapon branch. High-risk; faction-visible behavior requires runtime check. |
| Enemy loadout | Unknown; no exact fit observed. |
| Own loadout | Explicit variant can populate authoritative unit if resolved; keep live ammo/system state separate. |

**Adapter concern:** SeaPower.Client/managed/src/NavalCommand/SeaPower/Client/NativeGameFacade.cs:406-445 creates observations from selected-side tracks. Lines 441–444 copy populated track.Class without checking track.Identified, despite the warning at lines 419–420. This creates a potential observer leak through weapon/override branches. No code was changed.

## Missile/weapon axes

Ammunition parameters separate kind, _targetType/_secondaryTargetType (AAW/ASuW/ASW), _guidanceType, AirLaunched, land-attack, and _ballisticLoft. _ballisticLoft is a trajectory parameter, not a comprehensive ballistic-missile class. Compatible launcher/platform and active engagement are separate again.

An anti-ship cruise missile is a missile physically, has cruise flight profile, surface target class, guidance, and launch constraints. Do not encode it as a combined subtype or platform role. Keep physical family in SubCategory, target/guidance/flight in weapon metadata, orders/engagement in operational state.

## APP-6 compatibility

APP-06 E1 describes symbols composed from frame, fill, icon, modifiers, and amplifiers. The frame communicates standard identity, dimension and status; dimensions include Land, Sea Surface, Sea Subsurface, Air and Space. These are semantic alignments, not direct SIDC mappings:

| Entity | APP-6 semantic dimension/function | Limit |
|---|---|---|
| Surface ships/merchant/auxiliary | Sea Surface; function icon depends on role | Current hull subcategories often lack native evidence. |
| Submarine | Sea Subsurface | Role may refine function if validated; exact codes not checked. |
| Mine | Sea Subsurface / mine-warfare function | Threat/status exceeds static type; native mine is excluded from ordinary trackability. |
| Aircraft/helicopter | Air | Function may use curated role; identity/status separate. |
| In-flight missile | Air while airborne | Flight/target are separate; exact icon not verified. |
| Torpedo/MOSS | Sea Subsurface while underwater | Current dimension may depend on deployment state. |
| Sonobuoy | Sea Surface for buoy body; sonar capability separate | Exact icon not verified. |
| Land equipment/installations | Land | Native subtype should select function; broaden current values. |
| Decoys | Physical/current dimension plus observer identity certainty | No single symbol follows from Deco marker. |

Do not derive APP-6 from Category alone. Friend/hostile/unknown/suspect, status, role/function, and dimension have separate sources. Official references: [NATO-hosted APP-06 Edition E Version 1 PDF](https://coi.nato.int/EWCOI/EW%20COI%20Shared%20Documents/WGs/NEWWG/EW%20Info%20Exchange%20Requirements%20Panel%20%28IERP%29/01_Governing%20Documents/Publications/APP-06%20NATO%20Symbology/APP-06%20EDE%20V1%20E.pdf); [U.S. DLA APP-6 status listing](https://quicksearch.dla.mil/qsDocDetails.aspx?ident_number=275338), which lists E Amendment 2 active/promulgated in 2025. Full E2 tables were unavailable, so exact codes/icons remain unverified. No numeric identifier belongs in Domain.Api.

## Proposed refinements (report only)

These are sketches, not applied changes.

### Sourceable physical classification

Add generic values for native families and defer unsupported precision. Illustrative only:

~~~cpp
enum class SubCategory
{
    FixedWingAircraft,
    RotaryWingAircraft,
    Aircraft, // Generic fallback
    Missile,
    SurfaceVessel,
    Submarine,
    Torpedo,
    NavalMine,
    Sonobuoy,
    Noisemaker,
    GroundVehicle,
    SurfaceToAirMissileLauncher,
    AntiAircraftGun,
    Radar,
    LandInstallation,
    BiologicalEntity
};
~~~

Add generic Missile/Torpedo/SurfaceVessel and broader land fallbacks; add BiologicalEntity if runtime confirms agent-facing use. Remove/defer unsupported cruise/ballistic, heavy/light torpedo and military/civilian installation splits unless stable sources exist. Category::LandUnit is unmapped and should be removed or given a purpose. Preserve unknown/unresolved classification rather than forcing false subtype; keep Category and Dimension derived.

### Type-level roles

~~~cpp
struct Type
{
    TypeId Id;
    Classification::Api::SubCategory UnitSubCategory;
    std::vector<Classification::Api::Role> Roles; // curated from native type metadata
    std::vector<Equipment::Api::Loadout> Loadouts;
};

struct Loadout
{
    LoadoutId Id;
    std::vector<Fit> Equipment;
};
~~~

Type-level roles are justified by native metadata. Avoid “DesignedRoles” unless semantics are documented: AI.Role also includes civilian identity and simulation markers. Normalize only vetted mappings; keep missions/orders separate; do not infer role from weapons.

### Orthogonal weapon characteristics

Use physical family for identity and optional target/flight/guidance/launch traits in equipment metadata, rather than combination subcategories:

~~~cpp
struct WeaponCharacteristics
{
    std::optional<FlightProfile> Flight;
    std::vector<TargetKind> IntendedTargets;
    std::optional<GuidanceKind> Guidance;
    std::optional<LaunchDomain> LaunchDomain;
};

struct Type
{
    TypeId Id;
    std::vector<Effect> Effects;
    std::vector<Operation> Operations;
    std::optional<WeaponCharacteristics> Weapon;
};
~~~

Sketch only; add fields only with stable native mappings and complete semantics. Do not create a Units::Api::Type for every ammunition definition—only independently simulated entities in the agent/contact scope.

### Loadout identity

Scope ID to catalog revision + folder/INI type key + native loadout name. Preserve service year/date applicability. Mark synthesized empty/default entries as normalized/generated. Keep selected configuration, live inventory, damage/readiness, deployed weapons, and current mission separate.

## Unresolved questions and smallest safe follow-ups

No runtime experiment was performed here.

| Question | Smallest safe experiment |
|---|---|
| Are weapon INI names faction-visible before legitimate identification? | Approved isolated two-side scenario; record only selected-side Vehicle.Class, Identified, UI label and classification for one missile, torpedo and sonobuoy through identification. Do not read hidden target fields. |
| Can partial category/dimension be inferred or downgraded? | Compare selected-side bits/UI through one sensor-quality progression and track-loss case; observe isFalse, bits, Vehicle.Class, Identified. |
| Are mines/noisemakers/Deco/MOSS ordinary contacts? | One example each; inspect only observer table/UI and track lifecycle. Separate world trackability from faction identification. |
| Is sonobuoy visible and where is it plotted? | Deploy one active and passive buoy; compare observer contact position to buoy and sensor depth. |
| Is _loadoutVariant correct through rearm/change? | One own aircraft with two fits; record explicit variant and equipment at launch, recovery, rearm and transition. |
| Can different names resolve to equal equipment? | Read-only catalog comparison including date alternatives and extra stores. |
| Which role mappings apply to ships/submarines/multi-role aircraft? | Enumerate AI.Role by family and trace representative use sites; leave unmapped values explicit. |
| Are biological entities agent contacts? | Observe one existing biological object through selected-side table/UI without changing it. |
| Exact APP-6 E2 symbols for sonobuoys, decoys, weapons and mines? | Consult effective E2 tables; resolve dimension and function separately. |

## Evidence status and limits

- **Verified:** assembly identity; inspected classes, members and branches; current Domain.Api declarations; baseline configure/build/test.
- **Inferred:** role crosswalks, mapping recommendations and whether every native family needs an agent-facing type.
- **Unverified:** weapon/decoy/mine visibility in every state, rearm lifecycle, duplicate-equipment fits, mod conventions, biological contactability, exact APP-6 E2 symbols.
- A field’s existence does not prove it is populated in every mission. Enabled mods can override original INIs; implementation must use active merged content and a content revision.
