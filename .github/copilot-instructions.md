# Copilot Instructions for Circuids.Pulse

> **Read [`internal_docs/pulse-architecture.md`](../internal_docs/pulse-architecture.md) first.** It is the single source of truth for what Pulse is, how it's built, and where it's going. Everything below is a quick-reference distillation.

## What Pulse is (one line)

A slim, embeddable test runner that runs `[PulseCase]` / `[PulseMatrix]` suites **inside a real running .NET host app** (Blazor, MAUI, WPF, WinForms, …) using the consumer's real DI graph and real platform services. Built on `Microsoft.Testing.Platform` (MTP) hosted in-process; returns a strongly-typed `TestRunReport`.

**Pulse runs *next to* `dotnet test`, never instead of it.** When Pulse passes, the equivalent boundary tests in `dotnet test` are redundant.

## Hard architectural rules — do not violate

1. **One package, forever.** No `Circuids.Pulse.Blazor`, `.Maui`, `.Wpf`, `.WinForms`, `.Avalonia`, `.Uno`, `.RazorPages`, `.Reporters`, or `.TestExplorer`. Per-host integrations live under [`sample/`](../sample/) as copy-paste reference, not as packages.
2. **Two runtime dependencies, period.** `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Testing.Platform`. Adding a third needs an explicit justification of "no other path exists."
3. **Never pull in `Microsoft.Testing.Platform.MSBuild`.** It would surface Pulse tests in Test Explorer, which is exactly the wrong pathway — see architecture §3.3.
4. **Sequential execution only.** No parallelization within or across suites. Boundary tests share platform state.
5. **Cooperative cancellation only.** Never `Thread.Abort`. Per-test deadlines are linked-CTS based; tests must accept a `CancellationToken` to be enforceable.
6. **No method ever carries both `[Fact]` and `[PulseCase]`.** Conformance bases live in attribute-free contracts assemblies; concrete subclasses tag for one runtime each.
7. **No UI in core.** The runner returns `Task<TestRunReport>`. Rendering belongs to the consumer.
8. **The JSON shape of `TestRunReport` is the only stability contract.** Changes to it are additive only. Use [`PulseJsonContext`](../src/Circuids.Pulse/PulseJsonContext.cs) for serialization.
9. **No assembly scanning.** Discovery is scoped reflection over types explicitly registered via `PulseBuilder.AddSuite<T>()`. The trimmer must be free to drop unregistered types.
10. **`AssignedPlatform` is freeform.** Never invent a `PulsePlatforms` enum. If unset, the report carries the literal `"(unassigned)"`.

## Project layout

```
src/Circuids.Pulse/                  ← THE shipped package (and only package)
  ITestExecutor.cs                   ← Public entry point
  PulseBuilder.cs                    ← Configuration for AddPulse(...)
  PulseCaseAttribute.cs              ← [PulseCase]
  PulseMatrixAttribute.cs            ← [PulseMatrix]
  PulseRowAttribute.cs               ← [PulseRow]
  PulseSkipException.cs              ← Throw to skip at runtime
  PulseJsonContext.cs                ← System.Text.Json source-gen
  Models/                            ← TestRunReport, TestResult, TestOutcome, RuntimeEnvironment
  Extensions/                        ← AddPulse(this IServiceCollection)
  Internal/                          ← TestExecutor, PulseTestFramework, PulseRunContext,
                                       ReflectionDiscovery, RuntimeEnvironmentProbe, SuiteRegistration

src/Circuids.Pulse.UnitTests/        ← xUnit unit tests for the runner
sample/Circuids.Pulse.Blazor.WebAssembly.Sample/   ← Reference Blazor WASM consumer
internal_docs/pulse-architecture.md  ← Living architecture document — UPDATE THIS when behavior changes
```

## Coding conventions

- **TargetFrameworks:** `net8.0;net9.0;net10.0`. Anything new must work across all three.
- **Nullability:** enabled. Public APIs have `[NotNull]` / `?` / nullable annotations.
- **Records for models** (`TestRunReport`, `TestResult`, `RuntimeEnvironment`). Immutable, source-gen-friendly — no polymorphism, no `object`, no `dynamic`.
- **`internal sealed class`** for everything under `Internal/`. Public surface is the bare minimum.
- **Triple-slash XML docs on every public member.** `GenerateDocumentationFile` is on; `CS1591` is suppressed only because we already document everything.
- **No async over sync, no sync over async.** `RunAsync` is the only public entry; internal helpers are async all the way.
- **`ConfigureAwait(false)`** on every `await` inside `Internal/`. Consumer code may not have a sync context, but Blazor does.
- **No `Task.Run`** anywhere — Pulse is hosted code, the host owns the threading model.

## Public surface — what already exists

| Type | Lives at | Purpose |
|---|---|---|
| `ITestExecutor` | [src/Circuids.Pulse/ITestExecutor.cs](../src/Circuids.Pulse/ITestExecutor.cs) | Consumer entry point. `RunAsync(CancellationToken)` and `RunAsync(string suiteName, CancellationToken)`. |
| `PulseBuilder` | [src/Circuids.Pulse/PulseBuilder.cs](../src/Circuids.Pulse/PulseBuilder.cs) | Configuration. `AddSuite<T>()`, `AddSuite<T>(factory)`, `AddSuite(Type, factory)`, `AssignedPlatform`. |
| `PulseServiceCollectionExtensions.AddPulse` | [src/Circuids.Pulse/Extensions/](../src/Circuids.Pulse/Extensions/PulseServiceCollectionExtensions.cs) | DI registration. Singleton builder, scoped executor. |
| `[PulseCase]` / `[PulseMatrix]` / `[PulseRow]` | src/Circuids.Pulse/ | Discovery attributes. Mutually exclusive Case vs Matrix. |
| `PulseSkipException` | [src/Circuids.Pulse/](../src/Circuids.Pulse/PulseSkipException.cs) | Throw from a body to skip at runtime. xUnit's `SkipException` is also recognized by name. |
| `TestRunReport` / `TestResult` / `TestOutcome` / `RuntimeEnvironment` | [src/Circuids.Pulse/Models/](../src/Circuids.Pulse/Models/) | Strongly-typed report. CI/UI stability contract. |
| `PulseJsonContext` | [src/Circuids.Pulse/](../src/Circuids.Pulse/PulseJsonContext.cs) | `System.Text.Json` source-gen for the report. |

## PulseAssert — the assertion middle ground (planned, not yet shipped)

Pulse will ship a `PulseAssert` static class that sits **between** a tiny per-consumer helper and a full assertion library. See architecture §8 for the full surface.

- **Position:** more than `True/Equal/NotNull`, less than xUnit.Assert / Shouldly. Includes equality (with comparer overload), reference identity, null, string contains/starts/ends, sequence empty/equivalent, numeric `InRange`, `Throws`/`ThrowsAsync`, `Skip`, `Fail`.
- **Style:** prefix-positional `Method(expected, actual, because)`. **Never fluent** — fluent surfaces drag a vocabulary that competes with xUnit/Shouldly.
- **Failure type:** `PulseAssertionException`. The executor already maps any exception to `Outcome = Failed`.
- **Failure format:** one consistent shape — `PulseAssert.{Method} failed: …\n  Expected: …\n  Actual: …\n  Because: …`. Invariant culture, quoted strings, `[a, b, c]` enumerable rendering with truncation past 10.
- **Bar to add a new method:** **two unrelated consumers must independently hit the same gap.** Same pressure that gates Pulse 1.0.

## Running the conformance pattern

Conformance bases (`abstract class`, no test attributes) live in a shared contracts assembly. Concrete subclasses in two unrelated projects pick the runtime:

```csharp
// In a contracts assembly — no xUnit, no Pulse references:
public abstract class TokenStorageConformance
{
    protected abstract ITokenStorage CreateStorage();
    public virtual async Task Store_ThenRetrieve_ReturnsSameValue() { … }
}

// dotnet test consumer:
public sealed class InMemoryTokenStorageTests : TokenStorageConformance
{
    protected override ITokenStorage CreateStorage() => new InMemoryTokenStorage();
    [Fact] public override Task Store_ThenRetrieve_ReturnsSameValue()
        => base.Store_ThenRetrieve_ReturnsSameValue();
}

// Pulse consumer:
public sealed class BrowserLocalStorageTokenStorageTests : TokenStorageConformance
{
    private readonly ITokenStorage _real;
    public BrowserLocalStorageTokenStorageTests(ITokenStorage real) => _real = real;
    protected override ITokenStorage CreateStorage() => _real;
    [PulseCase] public override Task Store_ThenRetrieve_ReturnsSameValue()
        => base.Store_ThenRetrieve_ReturnsSameValue();
}
```

The single conformance rule: **only the abstraction being validated by the conformance class** must be the real platform implementation in a Pulse run. Every other dependency can be mocked.

## When making changes — the checklist

- [ ] **Does the change preserve the two-dependency closure?** No new transitive packages without explicit justification.
- [ ] **Does it keep `Microsoft.Testing.Platform.MSBuild` out?**
- [ ] **Did you add unit tests** in `Circuids.Pulse.UnitTests` for new public API?
- [ ] **Did you add a sample usage** in [`sample/Circuids.Pulse.Blazor.WebAssembly.Sample/Conformance/`](../sample/Circuids.Pulse.Blazor.WebAssembly.Sample/Conformance/) for new behavior?
- [ ] **Is the change at risk of being a per-host concept?** If yes, keep it in the sample.
- [ ] **`TestRunReport` JSON shape — backward compatible?** Additive only.
- [ ] **For `PulseAssert` additions:** have two unrelated consumers asked for it?
- [ ] **Did you update [`internal_docs/pulse-architecture.md`](../internal_docs/pulse-architecture.md)?** It is the living spec; PRs that change behavior without updating it are incomplete.
- [ ] **`ConfigureAwait(false)` on every `await` in `Internal/`.**
- [ ] **XML docs on every new public member.**

## Anti-goals — auto-reject if proposed

- A UI package (`.Blazor` / `.Maui` / `.Wpf` / …)
- A reporter framework / `ITestRunReporter`
- A `dotnet test` Test Explorer entry point in the core package
- A mocking framework / fixture container beyond `IServiceProvider`
- Parallel test execution
- Auto-retry on failure
- Test ordering attributes
- Member-data (`[PulseRowSource(nameof(...))]`) before the AOT/trimming story is verified
- A full fluent assertion library
- A `PulsePlatforms` enum or constants class

## Quick links

- **Architecture (read this first):** [internal_docs/pulse-architecture.md](../internal_docs/pulse-architecture.md)
- **Sample (real consumer):** [sample/Circuids.Pulse.Blazor.WebAssembly.Sample/](../sample/Circuids.Pulse.Blazor.WebAssembly.Sample/)
- **Sample suites:** [Conformance/](../sample/Circuids.Pulse.Blazor.WebAssembly.Sample/Conformance/)
- **Unit tests:** [src/Circuids.Pulse.UnitTests/](../src/Circuids.Pulse.UnitTests/)
- **Solution:** [src/Circuids.Pulse.slnx](../src/Circuids.Pulse.slnx)
