# Pulse — Coding Agent Skill

## What Pulse Is

Pulse is a slim, embeddable test runner that runs `[PulseCase]` / `[PulseMatrix]` suites inside a
**real running .NET host app** (Blazor, MAUI, WPF, WinForms, …) using the consumer's real DI graph and
real platform services. Built on `Microsoft.Testing.Platform` (MTP) hosted in-process; returns a
strongly-typed `TestRunReport`.

**Pulse runs next to `dotnet test`, never instead of it.** When Pulse passes, the equivalent boundary
tests in `dotnet test` are redundant.

Trigger this skill when:
- Adding or modifying code in the Pulse repo (`Circuids/Pulse`)
- Reviewing Pulse PRs
- Working on Pulse features or fixing Pulse bugs
- Writing Pulse tests

---

## Hard Architectural Rules — Never Violate

1. **One package, forever.** No `Circuids.Pulse.Blazor`, `.Maui`, `.Wpf`, `.WinForms`, `.Avalonia`,
   `.Uno`, `.RazorPages`, `.Reporters`, or `.TestExplorer`. Per-host integrations live under
   `sample/` as copy-paste reference, not as packages.
2. **Two runtime dependencies, period.** `Microsoft.Extensions.DependencyInjection.Abstractions` and
   `Microsoft.Testing.Platform`. Adding a third needs an explicit justification of "no other path
   exists."
3. **Never pull in `Microsoft.Testing.Platform.MSBuild`** — it surfaces Pulse tests in Test Explorer,
   which is the wrong pathway and causes issues with discovery, parallelization, and cancellation.
4. **Sequential execution only.** No parallelization within or across suites. Boundary tests share
   platform state.
5. **Cooperative cancellation only.** Never `Thread.Abort`. Per-test deadlines are linked-CTS based;
   tests must accept a trailing `CancellationToken` parameter to be enforceable.
6. **No method ever carries both `[Fact]` and `[PulseCase]`.** Conformance bases live in
   attribute-free contracts assemblies; concrete subclasses tag for one runtime each.
7. **No UI in core.** The runner returns `Task<TestRunReport>`. Rendering belongs to the consumer.
8. **The JSON shape of `TestRunReport` is the only stability contract.** Changes are additive only.
   Use `PulseJsonContext` for serialization.
9. **No assembly scanning.** Discovery is scoped reflection over types explicitly registered via
   `PulseBuilder.AddSuite<T>()`. The trimmer must be free to drop unregistered types.
10. **`AssignedPlatform` is freeform.** Never invent a `PulsePlatforms` enum. If unset, the report
    carries the literal `"(unassigned)"`.

---

## Project Layout

```
src/Circuids.Pulse/                  ← THE shipped package (and only package)
  ITestExecutor.cs                   ← Public entry point
  IPulseLifetime.cs                  ← Optional per-suite InitializeAsync / DisposeAsync hooks
  PulseBuilder.cs                    ← Configuration for AddPulse(...)
  PulseAssert.cs                     ← Middle-ground assertion library
  PulseAssertionException.cs         ← Thrown by PulseAssert on failure
  PulseCaseAttribute.cs              ← [PulseCase] (TimeoutMs, Skip, DisplayName)
  PulseMatrixAttribute.cs            ← [PulseMatrix] (TimeoutMs, Skip, DisplayName)
  PulseRowAttribute.cs               ← [PulseRow]
  PulseSkipException.cs              ← Throw to skip at runtime
  PulseJsonContext.cs                ← System.Text.Json source-gen
  TestRunReportFormatter.cs          ← Canonical plain-text report formatter
  Models/                            ← TestRunReport, TestResult, TestOutcome, RuntimeEnvironment
  Extensions/                        ← AddPulse(this IServiceCollection)
  Internal/                          ← TestExecutor, PulseTestFramework, PulseRunContext,
                                       ReflectionDiscovery, RuntimeEnvironmentProbe, SuiteRegistration

src/Circuids.Pulse.UnitTests/                       ← xUnit.v3 unit tests for the runner
sample/Circuids.Pulse.*.Sample/                     ← Reference consumers (Blazor WASM, MAUI, etc.)
skills/pulse.md                  ← This file — coding agent skill reference
```

---

## Public API Surface

| Type | Purpose |
|---|---|
| `ITestExecutor` | Consumer entry. `RunAsync(CancellationToken)` / `RunAsync(suiteName, ct)`. |
| `IPulseLifetime` | Optional per-suite `InitializeAsync` / `DisposeAsync`. |
| `PulseBuilder` | Config: `AddSuite<T>()`, `AddSuite<T>(factory)`, `AssignedPlatform`, `DefaultTestTimeout`. |
| `PulseServiceCollectionExtensions.AddPulse` | DI registration. Singleton builder + runtime env, scoped executor. |
| `[PulseCase]` / `[PulseMatrix]` / `[PulseRow]` | Discovery attributes. |
| `PulseSkipException` | Throw to skip at runtime. |
| `PulseAssert` / `PulseAssertionException` | Middle-ground assertion library. |
| `TestRunReport` / `TestResult` / `TestOutcome` / `RuntimeEnvironment` | Strongly-typed report. CI/UI stability contract. |
| `PulseJsonContext` | `System.Text.Json` source-gen for the report. |
| `TestRunReportFormatter` | Static `Format(TestRunReport)` — pure plain-text transformation. |

---

## Coding Conventions

- **TargetFrameworks:** `net8.0;net9.0;net10.0`. New code must work across all three.
- **Nullability:** enabled. Public APIs have `[NotNull]` / `?` / nullable annotations.
- **Records for models** (`TestRunReport`, `TestResult`, `RuntimeEnvironment`). Immutable,
  source-gen-friendly — no polymorphism, no `object`, no `dynamic`.
- **`internal sealed class`** for everything under `Internal/`. Public surface is bare minimum.
- **Triple-slash XML docs on every public member.** `GenerateDocumentationFile` is on.
- **No async over sync, no sync over async.** `RunAsync` is the only public entry; internal helpers
  are async all the way.
- **`ConfigureAwait(false)`** on every `await` inside `Internal/`.
- **No `Task.Run`** anywhere — Pulse is hosted code, the host owns the threading model.
- **Obvious comments are noise.** Keep comments to non-obvious intent only. Code should be
  self-documenting.

---

## Per-Test Timeouts and `CancellationToken` Injection

A `[PulseCase]` / `[PulseMatrix]` method whose **last parameter is `CancellationToken`** is invoked
with a per-test linked token; the framework appends it at call time. The token's deadline is
`attribute.TimeoutMs` (when > 0) or `PulseBuilder.DefaultTestTimeout` (when set). When the deadline
fires before the outer run is cancelled, the test is reported as `Failed` with
`"Test exceeded timeout of {N}ms"`. A test that ignores the token still hangs — by design.

---

## PulseAssert — The Assertion Middle Ground

- **Position:** more than `True/Equal/NotNull`, less than xUnit.Assert / Shouldly.
- **Style:** prefix-positional `Method(expected, actual, because)`. **Never fluent.**
- **Bar to add a new method:** two unrelated consumers must independently hit the same gap.

---

## Report and Formatting

- `TestRunReport` is the machine-readable contract — serialized via `PulseJsonContext`.
- `TestRunReportFormatter.Format(report)` produces the canonical plain-text representation. Pure
  transformation, zero knowledge of Console/logging/DI. No ANSI codes.
- Changes to the JSON shape are **additive only**.

---

## Build and Test Commands

```pwsh
# Build the package (all three TFMs)
dotnet build src/Circuids.Pulse/Circuids.Pulse.csproj --nologo

# Run unit tests (net10.0)
dotnet test src/Circuids.Pulse.UnitTests/Circuids.Pulse.UnitTests.csproj --nologo

# Run specific test filter
dotnet test src/Circuids.Pulse.UnitTests/Circuids.Pulse.UnitTests.csproj --nologo --filter "FullyQualifiedName~TestRunReportFormatter"
```

---

## Anti-Goals — Auto-Reject If Proposed

- A UI package (`.Blazor` / `.Maui` / `.Wpf` / …)
- A reporter framework / `ITestRunReporter`
- A `dotnet test` Test Explorer entry point in the core package
- A mocking framework / fixture container beyond `IServiceProvider`
- Parallel test execution
- Auto-retry on failure
- Test ordering attributes
- Member-data (`[PulseRowSource(nameof(...))]`) before AOT/trimming story verified
- A full fluent assertion library
- A `PulsePlatforms` enum or constants class
- Any third runtime dependency without "no other path" justification

---

## Pre-Change Checklist

When making a change to Pulse, verify:

- [ ] Preserves the two-dependency closure? No new transitive packages without justification.
- [ ] Keeps `Microsoft.Testing.Platform.MSBuild` out?
- [ ] Unit tests added in `Circuids.Pulse.UnitTests` for new public API?
- [ ] Sample usage added in `sample/*/` for new cross-host behavior?
- [ ] Is the change at risk of being a per-host concept? If yes, keep it in `sample/`.
- [ ] `TestRunReport` JSON shape — backwards compatible (additive only)?
- [ ] For `PulseAssert` additions: have two unrelated consumers asked for it?
- [ ] `ConfigureAwait(false)` on every `await` in `Internal/`?
- [ ] XML docs on every new public member?
- [ ] Builds on all three TFMs (`net8.0;net9.0;net10.0`)?
- [ ] Full unit test suite passes?
