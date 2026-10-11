# NavalCommandFramework — Code style and readability

**Applies to every hand-authored source file in every repository:** C++, C#, CMake, Python, PowerShell, Protocol Buffers, JSON/configuration and test code. Generated code, vendored code and untouched proprietary game code are excluded from reformatting; configure code generators rather than editing their outputs.

## Authority and precedence

- `Docs/CodeStyle.xml` is the **unaltered CLion/JetBrains export** supplied by the project owner. It is the primary source of truth for **settings actually present in that export**, predominantly C++ formatting and naming. It must be kept in version control under the canonical `Docs/` directory, not replaced by a loosely approximated style.
- This document is the source of truth for **human readability across languages**, and for converting the explicit exported C++ options into practical review rules. C# and other languages are **not specified by the C++ naming settings** in the export. Use their language-appropriate conventions for unspecified details, but maintain semantic names, readable structure and consistent style within the repository.
- If `.clang-format`, `.editorconfig`, IDE settings or formatter defaults disagree with an explicit exported rule, resolve the discrepancy and document it. Do not claim an automatically generated formatter is an exact translation unless it has been checked. Do not use wholesale formatting of generated/vendor trees.
- Project architecture takes precedence over cosmetic changes. Pre-release contracts may intentionally break when all affected consumers migrate together; do not retain obsolete shapes for compatibility alone. Avoid unrelated cosmetic API churn.

## Non-negotiable human readability (all languages)

1. **Never minify manually authored code.** One-line methods containing multiple statements, chained declarations, squeezed lambdas, or compressed test fixtures are unacceptable even if syntax-valid or under the line limit. Each logical action gets its own line; separate stages of computations and validation with whitespace.
2. **Meaningful names are mandatory.** Parameters and local variables must convey domain role, quantities and units where relevant (`latitudeRadians`, `earthRadiusMeters`, `distanceMeters`). Names like `a`, `b`, `x`, `y`, `tmp`, `data`, `obj`, `la`, `lob` are unacceptable for unrelated geometric or operational quantities. Tiny loop indices or standard mathematical symbols are acceptable only when immediately obvious from context.
3. **Express intent rather than compressing expressions.** Use named intermediate values for complex arithmetic and conditions. Extract well-named helpers only when they represent a coherent abstraction; do not fragment simple code into meaningless wrappers. Avoid multiple unrelated statements per line.
4. **Use explicit structural formatting.** Control flow must be visually distinguishable. Do not omit braces on `if`, `for`, `while` or similar branches merely to shorten code. Prefer straightforward early returns when they improve legibility.
5. **Replace unexplained literals with meaningful named constants** when they represent physical units, protocol limits, timeouts, distances or significant domain values. Avoid inventing constants for commonplace harmless values such as zero and one. Make physical units explicit in names or types where appropriate.
6. **Use comments for reasons, invariants, threading/lifetime assumptions, native-game quirks and surprising behaviors**, not for obvious line-by-line narration. Avoid walls of compressed comments or tests that hide their setup/assertions in a single expression.
7. **Do not sacrifice readability to minimize token count, line count or file count.** Readability and maintenance are review gates, not optional polish. Formatter success alone is insufficient.
8. **Preserve behavior during readability refactors**: distinguish semantic corrections from cosmetic cleanups, keep source mappings clear, run affected unit/contract/integration tests, and report unverified changes.

## Explicit C++ settings from `CodeStyle.xml`

| Setting | Required interpretation |
|---|---|
| `RIGHT_MARGIN=180` | Formatting margin of 180 columns; this is **not** permission to put a multi-step function on one 180-character line |
| `CppFormatting/INDENT_STYLE=Tab` | Use tabs for C++ indentation. Other languages inherit general readability rules; configure indentation explicitly per language/repository rather than guessing from this C++ export |
| Namespaces, types, functions, properties, events, public fields, enumerators | `PascalCase` (`AaBb`) |
| Local/global variables and parameters | `camelCase` (`aaBb`) |
| Private and protected C++ fields | `m_PascalCase` prefix (`m_` plus `AaBb`); note that the export sets `Inspect=False` for these fields but still stores this preference |
| Global/other constants and macros | `UPPER_SNAKE_CASE` (`AA_BB`); scope and compiler/vendor-required names take precedence |
| Non-type template parameters | `PascalCase` |
| `BracesInIf/For/WhileStatement=Required` | Braces required, even for single statements |
| Includes | Sort include directives; avoid relative include paths; use angle brackets when the style engine considers them appropriate |
| Member initializers | Prefer uniform initialization per export |
| `LINKAGE_SPECIFICATION_BRACES=NEXT_LINE` | Place **linkage-specification** braces on the next line; the export does not establish a universal brace-placement convention for every construct |
| Parameter, initializer, base-clause and braced-list wrapping | `CHOP_IF_LONG`; keep lengthy constructs legible rather than compressed |
| Paired C++ files | Source `.cpp`, header `.h`, PascalCase filenames |

The export does **not** explicitly specify a complete formatting scheme for every C++ construct, C# naming/indentation, PowerShell, Python, CMake or Protobuf. Do not misrepresent it as if it did.

## Additional C++ project conventions

- **No closing-namespace comments.** Close namespaces with a plain `}`, not `} // namespace` or `} // namespace NavalCommand::Orders::Api`. Modern IDEs display the surrounding namespace. The extra comments provide little value and create maintenance noise. Remove them from handwritten files when editing the surrounding code; do not mass-reformat unrelated files solely for this reason.
- **Prefer one independently usable public type per header.** A public header should not force consumers to include unrelated type definitions they do not use. Treat header inclusion as a dependency and apply the intent of the [Interface Segregation Principle](https://en.wikipedia.org/wiki/Interface_segregation_principle): keep each header's public responsibility narrow. A file containing several unrelated or independently reusable public classes, structs or enums is discouraged; place those types in separate appropriately named headers.
- **Exception for inseparable, closely related types.** Multiple types in a single header are acceptable when they form one cohesive abstraction and cannot reasonably be used independently. Prefer a nested type when a helper exists only as part of its owner. For example, `LaunchOption` should be a nested member type of `Stock` (such as `Stock::LaunchOption`) if it has no meaningful standalone use; if other interfaces or consumers use it independently, give it a dedicated `LaunchOption.h` instead. Do not nest it solely to avoid creating a file if it is conceptually independent.
- These are additional project conventions, **not** settings claimed to originate in `CodeStyle.xml`. When adjusting existing public headers, preserve operational meaning and migrate affected source/wire consumers together, and test affected consumers; do not launch an unrelated mass header-refactoring campaign.


## Example of forbidden density

The following **shape** is not acceptable regardless of language:

```csharp
public static double Distance(double la,double lo,double lb,double lob){double a=la*Math.PI/180,b=lb*Math.PI/180,x=(lb-la)*Math.PI/180,y=(lob-lo)*Math.PI/180;double h=Math.Sin(x/2)*Math.Sin(x/2)+Math.Cos(a)*Math.Cos(b)*Math.Sin(y/2)*Math.Sin(y/2);return 6371000*2*Math.Atan2(Math.Sqrt(h),Math.Sqrt(Math.Max(0,1-h)));}
```

Prefer explicitly named parameters and stages, for example (illustrative readable refactor, **not** a drop-in compatibility patch for a specific API):

```csharp
public static double CalculateDistanceMeters(
    double startLatitudeDegrees,
    double startLongitudeDegrees,
    double endLatitudeDegrees,
    double endLongitudeDegrees)
{
    const double EARTH_RADIUS_METERS = 6_371_000;
    const double DEGREES_TO_RADIANS = Math.PI / 180.0;

    double startLatitudeRadians = startLatitudeDegrees * DEGREES_TO_RADIANS;
    double endLatitudeRadians = endLatitudeDegrees * DEGREES_TO_RADIANS;
    double latitudeDifferenceRadians = (endLatitudeDegrees - startLatitudeDegrees) * DEGREES_TO_RADIANS;
    double longitudeDifferenceRadians = (endLongitudeDegrees - startLongitudeDegrees) * DEGREES_TO_RADIANS;

    double latitudeTerm = Math.Pow(Math.Sin(latitudeDifferenceRadians / 2.0), 2.0);
    double longitudeTerm = Math.Cos(startLatitudeRadians) * Math.Cos(endLatitudeRadians)
        * Math.Pow(Math.Sin(longitudeDifferenceRadians / 2.0), 2.0);
    double haversine = Math.Clamp(latitudeTerm + longitudeTerm, 0.0, 1.0);

    return 2.0 * EARTH_RADIUS_METERS * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1.0 - haversine));
}
```

Keep the public signature unchanged unless authorized, and do not assume newer APIs such as `Math.Clamp` exist in Sea Power's target framework; the example demonstrates **style**, not platform compatibility.

## Enforcement in code changes

Native modules use `.clang-format` for the representable C++ export rules: tab indentation, the 180-column margin, braces, sorted includes and linkage-specification brace placement. LLVM defaults fill in settings the export does not specify. This is a formatting subset, not an exact translation of naming, definition ordering or project structure; those remain manual review requirements. Compatibility exceptions for existing public member names, generated methods and ABI exports must be explicit.

Managed repositories use `.editorconfig` with ordinary C# spacing, indentation and multiline-block rules. Those settings do not inherit the C++ naming policy. Apply tools only to enumerated handwritten files; generated bindings and dependency trees are excluded.

- Before writing a file, inspect the governing repo style, `Docs/CodeStyle.xml` and this document. If a reference is only in the shared project documentation, bring the relevant instructions into the actual working repository context.
- Preserve the XML verbatim as a review reference. Establish project-local `.editorconfig`, C++ formatter configuration, and language-specific tooling only for settings that can be represented faithfully; note any limitations. Do **not** create a competing generic formatting configuration that silently overrides the owner's settings.
- For any file modified in a change, clean up nearby manually authored code with minified methods, obfuscated names and dense statements. Perform a focused readability audit across handwritten source/test/build files in the affected repositories; identify remaining issues rather than claiming a wholesale cleanup without inspection.
- Formatting automation is a supplemental check. Review methods and test bodies manually for semantic names, intelligible control flow and domain terminology.
- Tests, smoke harnesses, one-off helpers and scripts **must meet the same standard** as production code. Generated Protobuf/C# bindings, third-party packages and game binaries must remain untouched.
- Report formatting/lint checks and any deliberate exceptions in the associated change report. Do not trade verified functionality for speculative renames or mass reformat unrelated files.
