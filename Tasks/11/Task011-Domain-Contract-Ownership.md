# Task 011 — Extract operational domain contracts from Gateway.Api

**Type:** Bounded architectural ownership correction and build-migration task. **Status:** Proposed / not executed.

**Scope:** C++ domain contract ownership only. This is a structural extraction, not Task 006 (orders implementation), Task 008 (formations/weapon posture investigation), or a revision of the canonical wire protocol.

## 1. Architecture decision and observed starting point

The current `NavalCommand/Gateway.Api` repository contains 13 domain-only C++ headers under `public/NavalCommand/{Orders,World}/Api/`, even though it should own canonical wire serialization, the wire/domain codec and the gateway-facing provider interface. The headers are already in their intended namespaces and have no direct Unity, Sea Power, Protobuf or gRPC dependencies. `World.Api::Unit` depends on `Orders.Api::Eligibility`; `Orders.Api` does not depend on `World.Api`. `Gateway.Api::IProvider` and `Codec` use both sets of types. The `SeaPower.Client` C# provider currently uses the canonical generated wire types directly; do not claim there is a parallel C# domain model.

**Decision for this task:** introduce **one additional independently versioned Git repository, `NavalCommand/Domain.Api`**, owning **two independently identifiable, header-only CMake `INTERFACE` targets**, `Orders.Api` and `World.Api`. This is deliberately *one repository with two public domains*, not a new repository for every value object or a single opaque `Domain.Api` catch-all CMake library. Keep ownership, namespaces and dependencies explicit. Future domain additions require a justified scope decision, not automatic addition to this repository.

New source tree (illustrative, adjust empty directories only when necessary):

```text
Domain.Api/
  CMakeLists.txt
  CMakePresets.json
  .gitignore
  .clang-format
  README.md
  src/CMakeLists.txt
  public/NavalCommand/Orders/Api/{Availability,Eligibility,Order,Request,Result,Stage,Waypoint}.h
  public/NavalCommand/World/Api/{Facility,Position,Stock,Track,Unit,View}.h
  test/CMakeLists.txt
  test/DomainHeaderSmoke.cpp
```

The `src/CMakeLists.txt` defines the two **INTERFACE/header-only** libraries without speculative compiled binaries. `Orders.Api` exports its own include root. `World.Api` exports its own include root and `target_link_libraries(World.Api INTERFACE Orders.Api)` because `World.Api/Unit.h` includes `Orders.Api/Eligibility.h`. `Gateway.Api` consumes both and must expose them **PUBLIC** transitively, because its public `IProvider.h` and codec API use domain types. Preserve the existing public CMake target names of all established repositories. If a native consumer uses a direct domain type without the gateway, it should link the corresponding domain target, not `Gateway.Api`.

The resulting dependency direction is:

```text
Orders.Api  (no World/Gateway/Protobuf dependency)
    ↑
World.Api   (uses Orders.Api for per-unit eligibility)
    ↑
Gateway.Api (uses World.Api AND Orders.Api; owns protobuf + codec + IProvider)
    ↑
Gateway.Grpc / Gateway.Grpc.Api and native compositions (existing relationships)
```

Arrows point from dependency to consumer; `Gateway.Api` also directly consumes `Orders.Api`.

Do **not** change the namespace or public include spelling: `<NavalCommand/Orders/Api/Request.h>`, `<NavalCommand/World/Api/View.h>`, etc. No duplicate physical implementations or competing `Gateway.Api/public/NavalCommand/{Orders,World}/Api` shadows may remain after migration.

## 2. Authoritative policy and boundaries

Read the **latest** separate Docs repository before editing: `AGENTS.md`, `ARCHITECTURE.md`, `MODULE_STRUCTURE.md`, `MANAGED_BUILD.md`, `DEVELOPMENT.md`, `CODE_STYLE.md`, `CodeStyle.xml`, `LOCAL_SOURCES.md`, Task 010's report, plus the current relevant repository checkouts. Inspect actual `Gateway.Api/src/NavalCommand/Gateway/Api/Codec.cpp` and all its public headers, CMake targets, consumer includes, root dependency registrations, managed projects and packaging.

- Domain APIs are independent of transport/backends/serialization. No protobuf-generated types, gRPC, Sea Power, Unity, BepInEx, P/Invoke, simulation launch/configuration or decision policy in `Domain.Api` headers or targets.
- Keep `Gateway.Api` as the **sole canonical owner of `.proto` payload definitions and domain ↔ wire conversion**, plus its existing gateway-facing provider interface. Keep `Gateway.Grpc.Api` as the independent external gRPC RPC owner. Do not copy/move `.proto` files, create a second proto namespace, or redefine the wire schema inside the domain repository.
- Preserve the existing `NavalCommand::Orders::Api` and `NavalCommand::World::Api` types, field names, enumerator values, defaults, aggregate initialization layout, header includes and semantic behavior. This is a zero-semantic-change relocation, not permission to remodel world or order data.
- Preserve the `Gateway.Api` C++ codec's public functions and target identity, and the existing `Gateway.Api` managed project/Protobuf generation. Preserve C# transitive `ProjectReference` and CMake-generated managed contexts from Task 010. **Do not** introduce C# domain duplicates without an actual separate authorized feature/task.
- Keep no-game mock support, selected-faction observation limits, protected native automation and gRPC host interoperability. Do not change the native C ABI, protobuf field numbers, RPC services, scenario fixtures, task scripts or game binaries.
- Preserve `Build.CMake` as the canonical FetchContent/Build.Local.cmake resolver, with stable repository identities, normal Git source resolution and static CRT policy. No CPM, workspace-parent umbrella source directory or duplicate custom dependency loader.

## 3. Gate A — Source inventory, ownership map and migration impact

Before moving files, inventory all 13 original domain headers with hashes and identify their current references: `Gateway.Api` codec and provider headers, native tests, gateway server/CLI/mocks, host/composition, managed projects, documentation and any other tracked sources. Classify each type as domain, gateway-specific provider interface, codec/wire representation or unrelated. Avoid changing code that already has the correct owner.

Explicitly confirm these current relationships in the actual checkout:

- `World.Api/Unit.h` includes `Orders.Api/Eligibility.h`.
- `Gateway.Api/IProvider.h` exposes `World.Api::View` and `Orders.Api::{Request,Result}`.
- `Gateway.Api/Codec.h/.cpp` encode/decode domain objects to/from canonical protobuf classes.
- The production `SeaPower.Client` C# implementation presently deals in canonical wire messages, not these native domain value objects.

Output: a concise dependency/ownership table and migration plan in the Task 011 report. If the checkout differs from this baseline, record differences and make the narrowest equivalent correction.

## 4. Gate B — Create and validate Domain.Api

1. Prepare an independent local `Domain.Api` Git repository following established source, CMake, preset, naming and documentation conventions. Define **two** explicit INTERFACE CMake targets `Orders.Api` and `World.Api`, with the correct independent include exports and one-way domain dependency. Do not create unnecessary compiled sources, C# projects, vcpkg packages or speculative functionality.
2. Move the 13 canonical handwritten headers without altering their content beyond strictly necessary include/build adjustments; verify before/after content identity or explain every difference. Preserve `Order.h` as its existing compatibility umbrella and `View.h` as the current canonical world header.
3. Add an independent compile-only/header-consumer regression that includes and instantiates both types, including `World.Api::Unit` with order eligibility, with **no** `Gateway.Api`/protobuf/gRPC/Sea Power dependencies. Verify distinct target usage: link `Orders.Api` alone for an orders-only consumer, and `World.Api` for a world consumer (transitively Orders.Api).
4. Show that the new targets are consumed by target-based includes rather than accidental old `Gateway.Api/public` paths. Keep `World.Api` independent of Gateway and `Orders.Api` independent of World.

**Important publication dependency:** A new GitHub repository URL such as `https://github.com/NavalCommand/Domain.Api.git` cannot be assumed to exist. **Do not silently create or push remote repositories without authorization.** During the initial local migration use explicit ignored `Build.Local.cmake` registrations to the new local checkout, or an isolated local Git mirror for ordinary Git-fetch behavior. Clearly mark public-remote FetchContent unverified until the owner has created/published the new repository and verified its URL. Do not claim a fully deployable clean remote build if new dependencies are not publicly fetchable at the expected URL.

## 5. Gate C — Move ownership and reconnect dependents

1. Remove the original domain headers from `Gateway.Api/public/NavalCommand/{Orders,World}/Api`. Do not leave forwarding wrappers with the **same** public include spelling in two repositories, which would introduce include-order ambiguity; preserving include spelling is achieved via transitive include paths.
2. Add the new canonical repository dependency to `Gateway.Api` through `NavalCommandAddDependency(...)` **before** its CMake target composition. Link its `Gateway.Api` target `PUBLIC` to the required `Orders.Api` and `World.Api` INTERFACE targets. Ensure the dependency is available to `Gateway.Api` standalone and transitively from `Gateway.Grpc.Api`, `Gateway.Grpc` and `SeaPower.Mod` without manual sibling paths.
3. Keep `World.Api`/`Orders.Api` includes source-compatible in `Gateway.Api/IProvider.h`, `Codec.h/.cpp`, mocks, tests and independent consumer code. Change only build dependencies that genuinely need updating. No `Gateway.Api` dependency from the extracted domain repository.
4. Verify that consumers can compile with target-based public dependency propagation; they must not need to manually add `Domain.Api/public` or depend on a previously fetched hidden source path.
5. Retain full `Gateway.Api` codec tests, including unknown enum/protobuf-presence behavior, schema-lock checks and all managed/codegen tests. Domain migration must not change any generated protobuf file contents or descriptors.
6. Recheck all CMake source resolver paths (local overrides, transitive fetch, repeated identity, source collisions) and Task 010 output-layout guarantees. All new generated files remain under each owning repository's `o/`.

## 6. Gate D — Tests, publication gates and reporting

Required evidence, with exact revisions and commands:

- Independent CMake configure/build/CTest for `Domain.Api` (Debug/Release) and header-only compile checks, **without linking protobuf, gRPC or game code**.
- Clean Debug and Release configure/build/test of `Gateway.Api`, `Gateway.Grpc.Api`, `Gateway.Grpc` and `SeaPower.Mod` through local source resolution, plus one ordinary Git-fetch test of new repository revisions *when the remote is published*. Keep the existing static `/MT` and `/MTd` linkage; do not substitute `static-md`.
- Existing mock-host/CLI, codec round trip, unknown enum / optional presence, schema-lock and managed C# transitive builds/tests. Confirm no new domain type duplication in managed sources or proto definitions.
- Public include compatibility: compile a small consumer that still includes the **original** `NavalCommand/Orders/Api/...` and `NavalCommand/World/Api/...` headers, linking `Gateway.Api` without manual domain include directories. A separate domain-only consumer must compile without `Gateway.Api`.
- No tracked-domain headers left in the old gateway repository; no source changes to C# provider logic, game-control behavior, bridge ABI or user-facing orders.
- Optional previously approved live game regression only if safe and already authorized; otherwise state **not run** rather than perform game/loader changes just to validate header extraction.
- Record the known pre-existing Debug native gRPC/host shutdown crashes separately and preserve honest CTest exit status. Do not mask, suppress or redesign the gateway to make this unrelated task appear green.

**Deliverables:** local commits in each affected repository (new `Domain.Api`, updated `Gateway.Api`, documentation, and only consumer changes demonstrated necessary); no pushes without approval. Put `Docs/Tasks/011/REPORT.md` and small `Docs/Evidence/Task011/` receipts in the separate Docs repository. Update the normative repository count/dependency table and domain ownership in `Docs/AGENTS.md`, `Docs/ARCHITECTURE.md`, `Docs/MODULE_STRUCTURE.md`, plus relevant READMEs; **do not rewrite historical Tasks 001–010 evidence**. Update `Docs/Tasks/README.md` index once implementation proceeds.

**Completion criterion:** The `Orders.Api` and `World.Api` C++ value contracts are owned by independent, gateway-free targets in the new `Domain.Api` repository. `Gateway.Api` is solely the canonical wire/codec/provider-boundary owner and publicly depends on those domains. Existing include strings, aggregate shapes, wire contracts, native and managed client behavior, independent buildability, packaging and safety behavior remain intact. Any unmet remote-publication or existing Debug-runtime exit gates are reported explicitly, not silently claimed passed.

## Explicit exclusions

No Task 006 movement/facility orders, Task 008 formations/weapons APIs, new domain objects, additional gRPC services, catalog/event schema design, native gameplay refactors, managed C# domain mirror, changes to Task 010 build infrastructure other than the minimum needed for the added repository, general code readability sweep, or new third-party dependencies.
