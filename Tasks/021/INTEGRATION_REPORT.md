# Task 021 — Full-Project Classification Integration

**Status:** Implemented and pushed to `feature/classification` across the functional repositories. Automated builds and tests pass. A live Sea Power comparison remains for the owner to perform.

## Summary

The revised Domain.Api model now flows through the Gateway protobuf/codecs, gRPC provider and client, World, TacticalDisplay, and SeaPower.Mod adapter. CommandCenter consumes the updated dependencies. All changes are on `feature/classification`; no changes were made to `Build.CMake`.

The adapter now publishes native unit-type definitions, selected authoritative loadouts, platform roles, observer-supported classification, observed faction, and destruction status. The Mod config selects a conservative `safe` or broader `aggressive` observer classification mode. `safe` is the default.

## Owner-approved contract decisions

- Keep the complete `Dimension → Category → SubCategory → TypeId` hierarchy. `Units::Api::Type` stores one `SubCategory`; its category and dimension are derived.
- Add `Category::SeaAnimal` and `SubCategory::Whale`. `SeaAnimal` maps to `Dimension::SeaSubsurface`. Add generic `Category::Other`, `SubCategory::Other`, and `Dimension::Other`.
- Add authoritative `bool IsDestroyed` to units and `Exact<bool> IsDestroyed` to observations. Sea Power observations always carry a known boolean.
- Do not add `ObservedAlignment`; derive alignment from the observer faction, known observed faction, and faction relationship. Keep alignment independent from classification.
- Do not add `IsDormant`. The adapter does not publish freshness as classification or destruction. Its contributor list remains empty when it cannot attribute concrete observer/sensor identities.
- Authoritative units do not use uncertainty. If selected loadout metadata cannot be resolved, synthesize a stable empty loadout. Observation loadout remains unknown unless the adapter can establish a player-visible value.
- Map Sea Power native roles to `PlatformRoles`; expose type-specific overrides through a Mod configuration provider. Keep the provider interface independent of INI parsing.

## Contracts and transport

`Domain.Api` preserves the existing classification and role model and appends the approved whale/generic values without changing existing enum ordinals. Unit instances carry `CurrentLoadout` and `IsDestroyed`. Observation instances carry `Resolution<TypeId, SubCategory, Category, Dimension>`, optional known `CurrentLoadout`, and optional known `IsDestroyed`.

### Migration from the prior integrated contract

The previous Gateway/adapter/display path used the removed flat `Units::Api::Classification` enum and exposed less of the revised model. It now uses `Classification::Api::SubCategory` for authoritative unit types, derives category/dimension from that value, and preserves each observation's most-specific supported `Resolution` level instead of forcing contacts into an exact type or a flat unit enum. Platform roles and type-scoped loadouts flow through the type catalog. Unit selected loadout and destruction are authoritative fields; observation loadout and destruction preserve uncertainty/presence. Alignment is no longer modeled as an observation subtype or separate observed-alignment field: known factions remain independent and the selected observer's relationship supplies alignment.

`Gateway.Api` is the only owner of canonical protobuf domain messages. The schema now carries unit subcategories, platform/loadout roles, selected unit loadouts, each observation resolution alternative, optional observed faction, and presence-aware observation loadout/destruction values. The observation classification `oneof` represents Unknown by having no alternative. Optional booleans preserve known `false`; optional strings preserve the difference between unknown and a known value.

`UnitType.sub_category` is an optional wire scalar so an omitted required authoritative classification cannot silently decode as enum value zero. The C++ codec rejects a missing subcategory and validates numeric enum ranges. C++ codec tests and the C#/C++ conformance test cover the presence and resolution semantics.

## SeaPower.Client mapping

### Authoritative units and type catalog

- Canonical type IDs use lowercase `<content-folder>/<INI-name>` identities from active Sea Power metadata.
- `UnitType` definitions are read from the active `UnitMetadataProvider.UnitData` and candidate `UnitIdMap`; encountered active unit metadata is added to the catalog if absent.
- Curated encyclopedia-type labels refine known platform subcategories. MOSS (`ammunition/usn_moss`, `wp_mg-44`, `wp_mg-74`) maps to `SelfPropelledAcousticDecoy`; the eleven vanilla SKF/SSQ/RGB sonobuoy identities map to `Sonobuoy`, regardless of their native implementation family. The three vanilla whale IDs map directly to `Whale`. Other unmapped native types use a generic subcategory, normally `Other`.
- Native loadout names and fixed loadouts are enumerated where metadata exposes them. IDs are type-scoped as `<type-id>::loadout::<escaped-name>`. `empty` is included for each type. The selected `_loadoutVariant` is used when it matches the catalog; otherwise the unit receives its synthesized `empty` identity.
- Sea Power metadata does not currently provide a validated translation of loadout equipment fits or configuration-specific roles into Domain.Api. Those fields remain empty rather than fabricated. Observation `CurrentLoadout` remains unknown because exact platform identification does not itself disclose the selected configuration.
- Destroyed units are retained in the authoritative unit list and carry the native `ObjectBase.IsDestroyed` value.

### Platform-role mapping

The built-in native-role crosswalk is:

| Native `AI.Role` | NavalCommand `PlatformRole` |
|---|---|
| `Deco`, `SeaMine` | `Decoy`, `MineWarfare` |
| `Passenger`, `Transport`, `Landing`, `RAS`, `Airliner`, `SAR`, `LifeRaft` | `Transport` |
| `Minesweep` | `MineWarfare` |
| `Spy`, `Recon` | `Reconnaissance`, `Surveillance` |
| `FAC`, `ASuW`, `ASuW_Gun`, `ASM`, `Gun` | `SurfaceWarfare` |
| `ASuW_LR`, `ASuW_VLR` | `SurfaceWarfare`, `Strike` |
| `NGFS` | `LandAttack` |
| `AAW`, `SAM` | `AirDefence` |
| `ASW`, `SSN` | `AntiSubmarineWarfare` |
| `SSG` | `SurfaceWarfare` |
| `SSGN` | `SurfaceWarfare`, `LandAttack` |
| `SSB`, `SSBN`, `Bomber`, `HeavyBomber` | `Strike` |
| `Fighter` | `AirSuperiority` |
| `AEW` | `AirborneEarlyWarning`, `Surveillance` |
| `EW` | `ElectronicWarfare` |
| `MPA` | `MaritimePatrol`, `AntiSubmarineWarfare` |
| `ESM`, `Radar`, `Targeting` | `Surveillance` |
| `SEAD` | `LandAttack`, `ElectronicWarfare` |
| `Attack` | `Strike`, `LandAttack` |

`None`, `SmallCivilian`, `Merchant`, `Carrier`, `CVS`, `CVL`, `SS`, `Airfield`, `Port`, and `Target` map to no operational role. They describe no corresponding Domain role or are marker/type values, so they do not create a tactical capability. Duplicate mapped roles are removed.

`SeaPower.Mod/platform-roles.ini` contains 558 generated type-specific entries from the installed game's original unit `Role` metadata, annotated with `language_en` encyclopedia type labels where present. `scripts/Generate-PlatformRoleMappings.ps1` regenerates the file from the installed game content. Its output was compared with the committed file and matched line for line. Empty values intentionally clear a type's native roles. Unlisted active mod types use the built-in crosswalk.

The INI parser implements `IPlatformRoleMappingProvider` and validates type IDs, role names, duplicate entries, and sections. The Mod composition root constructs the concrete INI provider and injects it into SeaPower.Client. The package allow-list includes the default INI. `config.json` selects `classificationMode` (`safe` or `aggressive`) and the mapping file path; invalid modes fail configuration loading.

## Observer classification and faction

Observation membership remains restricted to the selected faction's `PlottingTable.Vehicles`. The adapter never creates a contact from the global entity/object registry.

In `safe` mode, the adapter reads `ClassInformation` from the contact's current observer ECS state. It returns Unknown for missing state, an empty candidate set, `isFalse`, `overrideIdentification`, or any candidate missing from the active metadata catalog. It maps all remaining candidates and emits the most-specific level shared by every candidate: subcategory, category, or dimension. Exact `TypeId` is emitted only when all candidates resolve to one canonical type and Sea Power reports `Identified=true`. A singleton candidate before exact identification stays at subcategory level.

In `aggressive` mode, the same safe candidate result is retained. Only when it is Unknown does the adapter consult the native map view model's `VisualType`, using this explicit whitelist:

| Native `VisualType` | Domain resolution |
|---|---|
| `Air`, `Surface`, `Installation`, `MobileUnit`, `Port`, `Bridge`, `Biologic` | Corresponding broad category (`Aircraft`, `SurfaceVessel`, `LandInstallation`, `LandUnit`, `SeaAnimal`) |
| `Helicopter`, `Submarine`, `Missile`, `Torpedo`, `Airbase`, `AAA`, `MobileAAA`, `SAM`, `Radar`, `SubDecoy`, `ActiveSonobouy`, `PassiveSonobouy` | Corresponding generic subcategory |
| `AirDecoy` | `Category::Other` |

The visual fallback never supplies an exact `TypeId`. This deliberately allows aggressive mode to report generic map-symbol information that safe mode leaves Unknown. It reads no `Vehicle.Class` value and does not infer a precise identity from a hidden object.

`ObservedFaction` is populated only when the observer-state `DetectedSide` entity is valid and carries a `TaskforceReference`; the adapter also requires Sea Power's side wrapper to be present. TacticalDisplay derives Friendly/Neutral/Hostile from the observation's observer faction, observed faction, and faction relationship. It does not copy `VisualPosture`.

## Destruction, loadout, and freshness semantics

Authoritative unit `IsDestroyed` is copied from the unit. Observation `IsDestroyed` is always set from `track.Object?.IsDestroyed ?? false`, matching the owner-approved Sea Power contract that the track destruction state is known. The map UI's destroyed color comes from this same native flag. The null-object fallback is implemented as `false`; confirm this case during manual comparison.

No dormancy or freshness field was added. `HasLiveTrack` and age-out remain adapter-internal and do not change classification, contact identity, or destruction. An observation remains present while Sea Power retains its plotting-table row. Contributor identities remain empty because sensor modalities alone do not identify the contributing unit/equipment IDs.

## Downstream presentation

TacticalDisplay now labels the exact TypeId, subcategory, category, or dimension resolution without promotion. It shows observed faction plus derived alignment, selected loadout when known, destruction status, unit subcategory/category/dimension, platform roles, and selected loadout roles. The development fixture includes a partial-resolution contact and a known false destruction value.

Gateway.Grpc.Client and World use the current `Classification::Api::Alignment` enum. No separate alignment wire field was added. CommandCenter needed no source changes; its CMake dependency revisions now select the coordinated feature commits.

## Builds and tests

Windows MSVC Release builds used the repository's CMake/Ninja setup and the installed Sea Power 0.8.3 references.

| Repository | Result |
|---|---|
| Domain.Api | Build passed; CTest 65/65 |
| Gateway.Api | Build passed; CTest 9/9, including managed conformance |
| Gateway.Grpc.Api | Build passed; no test target |
| Gateway.Grpc | Build passed; CTest 1/1 |
| Gateway.Grpc.Client | Build passed; CTest 7/7 |
| World | Build passed; CTest 9/9 |
| TacticalDisplay | Build passed; CTest 1/1 |
| CommandCenter | Build passed; CTest 1/1 |
| SeaPower.Client | Managed provider/classification tests passed; Release build passed |
| SeaPower.Mod | Managed runtime/configuration tests passed; Release build passed |

The managed builds report existing Sea Power reference-version conflict warnings (`MSB3277`); the C++ gRPC builds report the existing `IdentityKeyCertPair` deprecation warning. All completed builds had zero errors. No Sea Power process was launched for Task 021; the owner plans the live safe/aggressive comparison.

## Known limits and remaining acceptance

- The prior Task 020 live capture sampled only `overrideIdentification=true`; it did not validate the ordinary multi-candidate observer path. Safe mode deliberately withholds classification for override contacts. The newly implemented ordinary candidate-consensus path still needs the owner's live comparison.
- Task 020 traced `VisualType` statically but did not validate all displayed values in a live scenario. Aggressive mode's whitelist therefore needs manual player-view comparison.
- Destruction rendering was traced to `ObjectBase.IsDestroyed` and the owner reported the Kilo symbol turning black, but Task 021 did not run a destruction scenario. Confirm destroyed and retained contacts manually. The adapter treats a missing object reference as known `false`.
- Active mod content without a curated semantic label uses generic `Other` classifications unless native object-family fallback can classify it. New type-specific role assignments can be added to `platform-roles.ini`.
- Loadout equipment fits and loadout-specific roles remain empty until native metadata supports a validated mapping. Observation loadouts remain unknown unless independently established.
- No observer freshness/dormancy status is represented. The plotting table controls contact membership; an empty contributor list means no contributor identity was reported by this adapter.

## Manual comparison plan

1. Package/install the Mod with its `config.json` and `platform-roles.ini`; keep `classificationMode` at `safe` for the first run.
2. Start Sea Power and CommandCenter with the same player faction and scenario. Compare the first contact, partial candidate progression, exact platform reveal, observed faction/alignment, retained destroyed contact, weapon/device contacts, and any whale contacts.
3. Change only `classificationMode` to `aggressive`, restart the Mod/game session, and repeat the same scenario. Compare whether whitelisted generic `VisualType` labels add a broad classification while exact IDs still wait for the safe identification gate.
4. Check the no-object destruction fallback and ensure contact retention follows the native plotting-table row. Report any discrepancy between the player's map and CommandCenter's displayed resolution.

## Coordinated feature revisions

All commits are on `feature/classification`; dependency declarations pin the exact revisions below. `Build.CMake` was not changed.

| Repository | Commit |
|---|---|
| Domain.Api | `53e969150af4719f12e94b749edceb0313f91b9a` |
| Gateway.Api | `36ec4c5f3a9629f31b8d298ac40f55b61f2231b4` |
| Gateway.Grpc.Api | `a90fd10dc7f57189840ac6b19ed39855e1c7801f` |
| Gateway.Grpc | `8c1bfb771182eef0f0fc9ae2054fd908d7fdf138` |
| Gateway.Grpc.Client | `4fc239e328a314063b0b7e7c829425d5b9596fbf` |
| SeaPower.Client | `9a0dcb687e9323d706e6dc19f76cf6029f1d1521` |
| SeaPower.Mod | `d6814d28d152a7be900fd0674d90c41aecbfaae9` |
| World | `660b32c3066d8d99e57ed2e2015bd5afb6fb0fb1` |
| TacticalDisplay | `a835a6a1ccb3741b923c452caa6278cd223f3cf9` |
| CommandCenter | `7fc2579d9ce3cb783e64dc2117387eec1e492ba4` |
