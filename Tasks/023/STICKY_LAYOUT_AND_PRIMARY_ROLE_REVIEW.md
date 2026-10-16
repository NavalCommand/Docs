# Sticky layout and primary-role review

Follow-up to the owner's 2026-10-16 report. Supersedes the forced-primary policy
and the Kilo primary decision in the earlier fill/hysteresis report.

## Layout

The GUI has Adaptive (existing default) and Sticky decluttering modes. Hover
identity is removed from the solution cache: an unchanged measured scene returns
without searching even when hover changes. Real label geometry changes still
participate in layout; hovered icons remain pinned.

Sticky preserves clear previous offsets before candidate enumeration, including
when icons shrink, labels disappear or geographic positions separate. Collisions
with current presentation bounds, geographic markers or viewport limits trigger
repair. New objects receive a placement. Switching to Adaptive permits relaxation.
The deterministic bounded search and best-effort behavior for impossible layouts
remain. Session/perspective changes reset state. No movement/observation contracts
or timing-driven suppression were introduced.

## Applicable primary modifiers

Physical subcategory and primary role remain separate. Common APP-06 sector 1
modifiers now compose with the supported main icons rather than only with cruiser
or aircraft special cases. ASW uses common modifier 151; Udaloy draws ASW and DD.
Other common mappings cover surface warfare, mine warfare, EW, ISR, attack, cargo
and guided missile. Local AAW and aircraft mission modifiers remain distinct;
missile destination sectors cover air/surface/subsurface. Corrected local air 2
(bomber) and 6 (tanker); ASW no longer incorrectly uses local air 2.

An intentionally absent primary never derives a priority from PlatformRoles.
A present but inapplicable role/classification combination produces an explicit
conversion error rather than silently dropping its role. SAM/AAA air defence,
ballistic-submarine strike and acoustic-decoy purpose are already encoded by their
main icon and need no redundant modifier. Converter tests cover every supported
physical subcategory's applicable role path, including exact observations.
No Domain.Api or transport changes were needed.

The renderer composes main geometry and role consistently in measurement, halo
and foreground passes. Hostile open air/subsurface triangles use a compact
interior composition for common roles. Filled/unfilled styles and supported main
icons are covered. [Visual fixture](primary-role-icons.png): columns ASW destroyer,
ASW submarine, no-primary submarine, ASW carrier, cargo auxiliary and ASW helo;
rows friendly, hostile, neutral, unknown and assumed friendly.

## Complete installed inventory review

[Maintained policy and sources](../../../SeaPower.Mod/classification/SOURCES.md)
and [per-type decisions](../../../SeaPower.Mod/classification/platform-primary-roles.csv)
record all 558 original platform definitions plus 444 ammunition definitions in
the installed Sea Power 8.3 inventory. Supporting fragments without platform
metadata are excluded; ammunition includes payloads that never become tactical
objects. No original game files were modified or distributed.

The review combines researched mission-family descriptions with each exact game
type/variant and weapon target metadata. These are authored classification
inferences, not independent historical verification of every export variant.
Specialists retain secondary capabilities without losing their principal purpose:
Udaloy, Spruance and specialist ASW aircraft remain ASW; E-2C remains AEW and EA-6B
EW. Kilo and other general attack submarines, fleet carriers and multirole
fighter/attack aircraft have explicit empty primaries. Guided-missile cruisers
retain the owner-approved GuidedMissile designation exception, preserving CG.

The ledger distinguishes 488 primary decisions, 16 designation exceptions,
197 multipurpose types, 207 unrepresented purposes/support payloads and 94
unspecified cases. Fictional/unbuilt types and insufficient evidence remain
unset with reasons; rescue/training are not invented generic enum values.
Single-domain weapons retain an applicable mission; dual-domain torpedoes remain
multipurpose. Native ASuW includes land targets: explicit land-only weapons use
LandAttack instead of SurfaceWarfare.

The generator now consumes this ledger rather than a forced-priority table. All
1002 primary entries are explicit, including empty entries that clear native
primary defaults. Reviewed primaries are included in capability lists. It rejects
duplicate, new/unreviewed and stale IDs. Custom content requires a reviewed row.

## Validation

| Suite | Result |
|---|---:|
| Symbology.Nato.Api Debug | 30/30 passed |
| Symbology.Nato.ImGui Debug | 20/20 passed |
| TacticalDisplay Debug | 22/22 passed |
| TacticalDisplay Release | 22/22 passed |
| CommandCenter Debug integration | 2/2 passed |
| SeaPower.Client managed provider | Passed |
| SeaPower.Mod managed runtime/configuration | Passed |
| Deterministic regeneration and duplicate/missing/stale rejection | Passed |
| Runtime/harness package manifest checksums | 30/30 matched |

Tests cover unchanged hover geometry, sticky shrink/separation preservation,
collision repair with hover pinning, subcategory/role conversion, measured versus
drawn bounds, and actual packaged configuration against all ledger entries.
Visual smoke output was inspected. CommandCenter and runtime/harness packages
rebuilt successfully. Package build retains existing managed assembly-version
warnings and reports zero errors. No deployment, loader activation or game launch
was performed; MajorCombat moving-contact acceptance remains a live check.

Logs: affected module `o/sticky-roles-*.log`. Temporary research downloads, build
orchestration, regeneration rejection fixtures and draft review scripts remain
ignored under Docs/o, classified as disposable experiments. No experimental
activation/source entered production targets. The maintained generator and ledger
are development/configuration assets; runtime packages consume only the generated
INI. Regression fixtures and ledger copies are test-only, outside runtime packages.

## Revisions

| Repository | Commit |
|---|---|
| Symbology.Nato.Api | `6585ad91f58dbc950d76a244617d9c54218d9181` |
| Symbology.Nato.ImGui | `fea5986c7b3c111105e2d8b96c8c78da5bd18735` |
| TacticalDisplay | `f2d4dc86c4aa3d81f690ec56ea84d4b1f641fa54` |
| SeaPower.Mod | `39fcaf07cdf5166b06c316e215eaf18af4677193` |
| CommandCenter | `a75586b2d4f134d1a9faafcba98dc29b3cf2cddd` |
