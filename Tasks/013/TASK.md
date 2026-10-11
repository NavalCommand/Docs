# Task 013 — Sea Power unit catalog and equipment mapping

**Status:** Investigation complete. Reports and bounded runtime evidence are ready for design review.

The primary design reference was Task 012’s `DOMAIN_MODEL_PROPOSAL.md`. The method-first `API_DESIGN_PROPOSAL.md` remains deferred and was not treated as an approved contract.

## Objective

Determine how the installed Sea Power build enumerates platform definitions, alternative fits, equipment capabilities and deployed equipment state. Compare those sources with the current composition based domain proposal in [Task 012](../012/DOMAIN_MODEL_PROPOSAL.md).

This is an investigation only. It does not authorize production C++, domain API, Protobuf or RPC implementation.

## Scope

- Synchronize the Docs repository from `main` and read the current architecture and Task 012 proposal.
- Inspect native metadata, loadout, weapon, sensor, runtime unit and contact structures.
- Check the existing player facing UI paths that identify contacts and show class information.
- Use a bounded no fire run of the existing opt in harness to sample the complete loaded catalog, one friendly F-14A and one identified enemy track.
- Record evidence, proposed domain mappings, limits and recommended model adjustments.
- Commit the documentation locally without pushing.

## Acceptance criteria

- [x] Repository synchronization preserved the existing clean `main` checkouts; Docs was fast forwarded from GitHub.
- [x] Static findings distinguish file backed catalog enumeration from units present in a scenario and document mod directory and cache behavior.
- [x] Loadouts, nominal counts, weapon and sensor capabilities, active ECM/countermeasures, and live equipment state have native source references.
- [x] Enemy track findings use only selected-side plot information and the UI's actual identification/loadout reference gates.
- [x] Reports include source backed examples for an aircraft, surface vessel, submarine, weapon, sensor and friendly/enemy instance.
- [x] Runtime evidence records the bounded sample, no fire conditions, preflight check and exact loader cleanup result.
- [x] No production repository, public API, schema or RPC was changed.
- [x] Deliverables committed locally; no push.

## Deliverables

- [STATIC_FINDINGS.md](STATIC_FINDINGS.md)
- [RUNTIME_FINDINGS.md](RUNTIME_FINDINGS.md)
- [DOMAIN_MAPPING.md](DOMAIN_MAPPING.md)
- [Run 1 evidence](../../Evidence/Task013/Run1/)
