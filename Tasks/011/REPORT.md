# Task 011 — Domain contract ownership

Implemented locally on 2026-10-10. The ownership correction and local build/compatibility checks passed. Public-remote dependency resolution is **blocked by the unpublished Domain.Api repository**. The existing Debug gateway/host process-exit failures reproduced and remain unresolved. No repository was pushed or created remotely.

## Baseline and ownership

Read the current permanent AGENTS, ARCHITECTURE, MODULE_STRUCTURE, MANAGED_BUILD, DEVELOPMENT, CODE_STYLE, CodeStyle.xml and LOCAL_SOURCES documents, Task 010 report, and actual current source/build checkouts. The supplied specification was located at [../11/Task011-Domain-Contract-Ownership.md](../11/Task011-Domain-Contract-Ownership.md); this report uses the requested `Tasks/011/` deliverable location. No historical documents were restored and no parent AGENTS.md was created.

[Before inventory](../../Evidence/Task011/before-inventory.json) records repository revisions, working status and actual CMake files/targets. Gateway.Api had two pre-existing working edits: SchemaRoundtrip.cpp formatting, and an Order.h removal TODO. The formatting remains uncommitted and untouched. Order.h, including its existing comment, moved intact; the compatibility umbrella remains as required by this task. The comment was not treated as authorization to remove the public include.

| Source/types inspected | Classification and resulting owner | Dependency |
|---|---|---|
| Availability, Eligibility, Order, Request, Result, Stage, Waypoint | Domain.Api: Orders.Api public headers | C++ standard library only; no World dependency |
| Facility, Position, Stock, Track, Unit, View | Domain.Api: World.Api public headers | Unit includes Orders.Api/Eligibility.h |
| Gateway/Api/IProvider.h and Error.h | Gateway.Api provider boundary | View(), Submit(), Status() expose owned world/order values |
| Gateway/Api/Codec.h and Codec.cpp | Gateway.Api domain ↔ canonical wire conversion | Existing Encode/Decode overloads and Protobuf bindings |
| Gateway.Api/proto and managed/Gateway.Api.csproj | Gateway.Api canonical wire representation and generated C# | Existing schema, protoc and NuGet unchanged |
| SeaPower.Client managed implementation | Game-facing canonical wire provider | No separate C# domain model introduced |
| Gateway mock/server/tests, mod host/ABI and CLI | Existing consumers/compositions | Existing Gateway target dependencies now propagate domain includes |

The migration plan was to inventory and hash the 13 actual working headers, move them without edits, define separate domain INTERFACE targets, reconnect Gateway.Api PUBLIC dependencies, then clean-build the existing consumers. [Header hashes](../../Evidence/Task011/header-hashes.json) prove all 13 moved files are byte-identical. Per-repository reference receipts under Evidence/Task011 record current include/type consumers. No domain header shadows or forwarding copies remain in Gateway.Api.

## Implementation

`Domain.Api` is an independent local Git repository. Root CMake bootstraps Build.CMake and composes subdirectories. `src/CMakeLists.txt` defines `Orders.Api` and `World.Api` as header-only INTERFACE libraries, each exporting its repository public include root. World.Api links Orders.Api INTERFACE. There are no compiled production libraries, managed projects, transport/backend dependencies, third-party packages or speculative domain additions.

The existing namespace-mirrored `public/NavalCommand/{Orders,World}/Api/` paths, namespaces, member spellings, enum values, defaults, aggregate shapes, Order.h umbrella and View.h surface remain intact. Separate orders-only and world-only consumers link their respective targets in `test/`, covering every header through the existing include graph without Gateway/Protobuf/gRPC/game dependencies.

Gateway.Api registers `https://github.com/NavalCommand/Domain.Api.git` at `main` through NavalCommandAddDependency before module composition. Its existing Gateway.Api library links Orders.Api and World.Api PUBLIC alongside protobuf. A new regression consumer includes the original order/world paths and exercises the existing codec, linking only Gateway.Api with no manual domain include directory.

No shared resolver modification was needed. The new canonical identity resolves to the deterministic 15-character FetchContent name `nc_eea0c16a12ca`. CMP0168 NEW, C++26 policy and static `/MT` Release / `/MTd` Debug remain centralized. Domain.Api needs no vcpkg manifest; existing consumers retain x64-windows-static dependencies. Actual compile-command runtime flags were audited.

Developer-owned, ignored top-level Build.Local.cmake files in the five existing functional repositories now register the Domain.Api checkout. These registrations apply before FetchContent declarations and transitively from Gateway.Grpc.Api, Gateway.Grpc, SeaPower.Client and SeaPower.Mod. No sibling discovery, second resolver or machine-specific tracked build configuration was added. Build.CMake still uses its standard bootstrap override.

Permanent AGENTS, ARCHITECTURE, MODULE_STRUCTURE and LOCAL_SOURCES now describe six functional repositories plus Build.CMake, physical domain ownership and publication constraints. Domain.Api and Gateway.Api READMEs and the task/evidence indexes reflect the change. Managed build rules and projects required no changes.

## Verification

Windows verification used VS 2026 Insiders MSVC, CMake 4.2.2, Ninja, .NET SDK 10.0.401 and the existing provisioned x64-windows-static vcpkg prefix. Third-party packages were reused, **not rebuilt**. Each affected source repository was independently configured with `--fresh`, cleaned through its native clean target, rebuilt and tested in both configurations. The commands/environment and exit results are recorded by [Verify.ps1](../../Evidence/Task011/Verify.ps1), [Release results](../../Evidence/Task011/results-Release.json) and [Debug results](../../Evidence/Task011/results-Debug.json).

| Check | Release | Debug |
|---|---|---|
| Domain.Api independent configure/build and orders/world consumers | 2/2 CTest passed | 2/2 passed |
| Gateway.Api codec, original-include consumer, C# ↔ C++ conformance | 3/3 passed | 3/3 passed |
| Gateway.Grpc.Api import regression and CLI build | 1/1 passed | 1/1 passed |
| Gateway.Grpc native gateway regressions | 1/1 passed | CTest failed, exit 8: all three test cases passed, then SegFault at shutdown |
| SeaPower.Client fake-provider regressions | 1/1 passed | 1/1 passed |
| SeaPower.Mod host ABI and managed runtime/harness | 2/2 passed | Managed passed (27 checks); host assertion passed then shutdown SegFault, CTest exit 8 |
| CMake production runtime and separate harness packaging | Passed | Passed |
| Direct ordinary runtime/harness .csproj builds after configuration | Passed | Passed |
| Package hashes, allowlists, disabled runtime default and separation | Passed | Passed |

The ordinary runtime build transitively compiled SeaPower.Client → Gateway.Api through unchanged MSBuild ProjectReferences. Managed conformance retained oneof, optional presence, stable IDs/units and unknown-field checks; native codec retained unknown enum and optional-zero behavior. The existing schema-lock script and its incompatible-field negative regression passed. Protected proto, C#, csproj, native implementation and provider/ABI header files were compared against baseline Git content; no changes were found. Generated C++ Protobuf outputs are identical between Debug and Release; generated C# payload files match across configurations. Descriptor sets regenerated from the pre-task Git schema and current schema have identical hashes. See source/output, generated-Protobuf and descriptor compatibility receipts.

The three new C++ test files passed clang-format dry-run after formatting and were rebuilt/retested in both configurations. Relocated public headers were intentionally not reformatted, preserving exact contents and existing public-name compatibility exceptions. Source and permanent-document whitespace checks passed; raw command logs retain their emitted trailing blank lines, which Git reports as whitespace notices.

The shared Build.CMake CTest suite passed 2/2, covering Git/local resolution, direct/transitive substitution, canonical reuse, invalid/conflicting/late registrations, shortened identities and managed context/ProjectReference behavior. An isolated child-only Git URL rewrite tested snapshots of the actual working repositories with **no Build.Local.cmake**: Gateway.Api fetched Domain.Api directly and passed its full CTest; SeaPower.Mod resolved Domain.Api transitively and built its ordinary runtime project graph. [Git resolution receipt](../../Evidence/Task011/git-resolution.json) records the snapshot commit IDs and actual fetched paths. This verifies Git transport/resolution mechanics, **not public GitHub availability**. Final formatting of consumer tests was separately rebuilt/tested; the library/header build graph was unchanged.

The Release mock-host and independent naval-cli completed the existing waypoint smoke round trip, including duplicate submission behavior and 111 m **mock** displacement. The fixture host was stopped by its owned PID afterward. Embedded native host ABI/session/port/bounds regressions and managed runtime/harness checks passed in Release. No live game or loader operation was performed for this header ownership change; live regression and interactive Visual Studio/Rider sessions were **not run**.

[Source/output audit](../../Evidence/Task011/source-and-output-audit.json) found no generated build artifacts outside each repository's o/, no old physical gateway domain headers and matching static CRT flags. Separate Debug/Release package receipts verify every manifest hash and production/harness exclusions. Proprietary assemblies and development harness/probes are excluded from production; package staging did not deploy or enable the loader. No gameplay, orders, ABI, RPC/schema, managed runtime, Task 006 or Task 008 change was made.

## Publication and remaining limits

At the time of the original Task 011 report, `git ls-remote https://github.com/NavalCommand/Domain.Api.git` returned exit 128, “Repository not found.” Public-remote FetchContent verification was then blocked because Domain.Api had not been published and had no configured Git remote. That historical publication blocker is now resolved; see the verification appended below.

The known Debug gRPC/host exit crashes remain unresolved and unsuppressed. Raw CTest failure logs and exit status are retained. Fresh isolated managed builds also retain existing MSB3277 game-reference version warnings (6 warnings, 0 errors); no assembly-binding or gateway rewrite was added to this task.

## Local commits and developer workflow

Source commits: Domain.Api `3ee1a46` and Gateway.Api `55a700d`; Docs has its own separate implementation/evidence commit. [Final revisions](../../Evidence/Task011/final-revisions.json) records full source revisions and unchanged repositories. Only the owner's original SchemaRoundtrip.cpp working edit remains in Gateway.Api. No pushes or remote repository creation occurred.

From a VS x64 developer shell, configure Domain.Api independently using its standard windows-msvc-debug/release preset and the standard Build.CMake bootstrap override, then build and run CTest under `o/<preset>`. For Gateway.Api or any transitive top-level consumer, add this to the **ignored** Build.Local.cmake before configuring:

```cmake
NavalCommandSetDependencyPath(
    https://github.com/NavalCommand/Domain.Api.git
    "/absolute/path/to/Domain.Api")
```

Existing gateway/mod preset, CMake package targets and direct .csproj workflows remain unchanged. Reconfigure consumers when changing source mappings; do not manually add include paths or edit generated managed context files.

## Public repository verification

**Status: passed; original publication blocker resolved.** `git ls-remote --symref https://github.com/NavalCommand/Domain.Api.git HEAD 'refs/heads/*' 'refs/tags/*'` confirmed the public `main` (`0e49e27ec2173e47669ac25a988806f550c74c50`), which matched the commit fetched through both direct Gateway.Api and transitive SeaPower.Mod resolution. The same check confirmed public `main` heads for Build.CMake, Gateway.Api, Gateway.Grpc.Api, Gateway.Grpc, SeaPower.Client and SeaPower.Mod; the Gateway.Api and SeaPower.Mod clone revisions matched those heads. Each verification used a disposable clean clone of its public top-level repository; neither clone contained `Build.Local.cmake`. `git config --show-origin --get-regexp '^url\..*\.insteadof$'` returned no rewrites; no `FETCHCONTENT_SOURCE_DIR_*` override was supplied, and corresponding CMake cache entries were empty. All fetched internal repositories had their expected public HTTPS `origin` and lived under the clean clone's `_deps`; SeaPower.Mod's generated managed context pointed Gateway.Api and SeaPower.Client at those fetched sources. The x64-windows-static prefix was reused with manifest installation disabled; Release compile commands contained `/MT`.

Executed in the temporary clones: `git clone --depth 1 --branch main https://github.com/NavalCommand/Gateway.Api.git $gateway`; `git clone --depth 1 --branch main https://github.com/NavalCommand/SeaPower.Mod.git $mod`; `cmake --preset windows-msvc-release --fresh -DCMAKE_MAKE_PROGRAM=<Ninja> -DVCPKG_INSTALLED_DIR=<provisioned-static-prefix> -DVCPKG_MANIFEST_INSTALL=OFF` (SeaPower.Mod also used `-DSEAPOWER_GAME_ROOT=<read-only game reference root>`); `cmake --build o/windows-msvc-release --target clean`; `cmake --build o/windows-msvc-release --parallel 8`; `ctest --test-dir o/windows-msvc-release --build-config Release --output-on-failure --no-tests=error`. Gateway.Api configuration and compilation passed; CTest passed 3/3, including domain transitive includes and managed conformance. SeaPower.Mod configuration and compilation passed; `cmake --build o/windows-msvc-release --parallel 8 --target package-runtime` passed and produced the 21-file runtime package in the temporary clone; `dotnet build managed/runtime/SeaPower.Mod.csproj -c Release --nologo` passed; CTest passed 2/2. Existing MSB3277 managed reference and gRPC deprecation warnings were non-fatal. No game installation files were modified or deployed.
