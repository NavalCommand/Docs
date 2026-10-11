# Task 013 — bounded runtime findings

**Status:** Bounded runtime sample completed. The run confirmed catalog availability, one friendly unit’s current equipment, and a player-visible enemy class. Firing, reload, damage transitions and enemy equipment were not exercised.

## Conditions and cleanup

- Sea Power **0.8.3 Build 261001 Public Release**; `Seapower-Scripts.dll` SHA-256 `0FE6AAF2A6D9982E7E2EB41FD6035CB97C149773202C90821D71C00A9FDEB01A`.
- Used the existing `SeaPower.Mod/scripts/Start-Live.ps1` in `Harness` mode, with `Docs/Tasks/012/Fixture/AircraftEngagementHold.ini`, side `Taskforce1`, and the one-off fixture source in [Fixture](Fixture/Task013CatalogProbe.csproj). The scenario contains one player F-14A and one opposing MiG-23MLD; both have `WeaponStatus=Hold`, and no attack task or trigger is assigned.
- Before launch, checked the exact known path `BepInEx/plugins/Task012EngagementProbe.dll`; it was absent. `doorstop_config.ini` was already disabled and had SHA-256 `3B5FBCF2C104F967F63687A0FD240CB79A5253D08F4338A358EE796C17B5AA0A`.
- The fixture ran only on `Globals.mainThread`. It read catalog caches, the own F-14’s system/ammunition fields and selected-side plotting-table fields. For the enemy contact it read only `Identified`, `Class`, `HasLiveTrack` and `PositionEstimate.HasValue`; it did not access `Vehicle.Object`, `BaseObject`, weapon systems or enemy ammunition.
- The scenario produced a ready receipt. The installed loader emitted its known `UnityLogWriter` compatibility error, but BepInEx reached chainloader-ready and the fixture captured all samples.
- The fixture compiled successfully against the installed game references; the compiler emitted framework-version conflict warnings from those references, with no errors.
- The harness removed its owned plugin directories and restored the original doorstop bytes. `cleanup.json` confirms `restoredDoorstop=true` and the same SHA-256; `enabled = false`. The Task 012 DLL remained absent, no game process remained, and no vendor assembly was changed. The runner used its documented forced-owned-process cleanup, so this run is not a graceful-exit measurement.

Full receipts and the captured fixture lines are in [Run 1 evidence](../../Evidence/Task013/Run1/).

## Measurements

| Sample | Runtime observation | What it establishes |
|---|---|---|
| Loaded catalog | 642 unit metadata entries: 558 base keys plus 84 synthetic loadout keys; 443 ammunition definitions; 508 sensor definitions. Platform entries: 92 Aircraft, 21 Helicopter, 268 Vessel, 70 Submarine, 188 LandUnit and 3 Biologic. | The cache is not limited to the two units in this scenario. These counts describe the active file set for this run. |
| F-14A catalog entry | Native cache key `aircraft/usn_f-14a`; runtime cache ID 66; five loadout records: `AirToAir`, `AirToAirIntercept`, `AirToAirLongRange`, `Empty` and `Ferry`. Representative counts include four AIM-7M, four chaff charges and 675 20 mm rounds in `AirToAir`. | Confirms alternative fits and the provider-generated `Empty`/`Ferry` entries. Numeric ID 66 is run-local. |
| Perry surface-vessel entry | `vessels/usn_ffg_oliver_hazard_perry`, cache ID 499, `Default` and `Late` loadouts. | Confirms a surface vessel with alternate fits is in the type catalog, regardless of current scenario membership. ID 499 is run-local. |
| Los Angeles submarine entry | `vessels/usn_ssn_los_angeles`, cache ID 526, `Default` and `Late`; the resolved ammunition counts are listed in [static findings](STATIC_FINDINGS.md). | Confirms a submarine’s alternative fitted capabilities are available in the same catalog. ID 526 is run-local. |
| Friendly deployed aircraft | Current loadout `AirToAir`; ammunition dictionary: 4 `usn_aim-7m`, 4 `usn_aim-9m`, 25 `all_air_chaff_standard`, 675 `usn_cal_20mm`, 2 `usn_tank_267_f-14`. Six sensor systems were present: ECM and ESM on, one AWG-9 radar off and the Phoenix radar on, plus two visual systems on. Four native weapon-system objects were present. | Runtime counts and activation differ from the nominal catalog description. Date-based stations resolved to the 1985 scenario’s AIM-9M. The fuel tanks are fitted stores, not weapon effects. |
| Player-side enemy track | `Identified=true`, visible class `wp_mig-23mld`, live track and a position estimate present. The probe did not read exact equipment. | The player can know the class while the exact loadout and weapons collection remain unknown. |

The full fixture lines are preserved in `Run1/loader.log`. The sample is observational: both aircraft remained on Hold and no weapons were fired. It therefore does not establish ammunition decrement after fire, reloading behavior, damage/inoperability transitions or post-deployment changes. Task 003 separately validated air-facility launch/recovery lifecycle; it does not establish those equipment transitions.

## Limits

- This run did not enable a custom mod. Static inspection shows the provider uses the same active virtual-file mechanism for selected content, but mod override discovery was not live-tested.
- No on-screen screenshot was captured. UI identification/loadout gating was established by static inspection of `MapUnitViewModel`, `ObjectInformationViewModel` and `VehicleInfoViewModel`; the runtime sample confirms the selected plotting table held the identified class.
- Counts in `AmmunitionAmountDictionary` are one native aggregate. This run did not reconcile loaded weapon rounds against magazine inventory or invoke the separately aggregating `GetAllAvailableAmmo` path.
- Communications definitions, complete sensor performance normalization, all-platform loadout closure and exact enemy equipment disclosure remain open.
