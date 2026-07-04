<div align="center">
  <img src="https://github.com/Circuids/Pulse/blob/master/images/cover_logo_min.jpg?raw=true" alt="Pulse logo" height="250" width="1000" />
</div>

<div align="center">

[![NuGet](https://img.shields.io/nuget/v/Circuids.Pulse.svg)](https://nuget.org/packages/Circuids.Pulse/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/Circuids/Pulse/blob/master/LICENSE)

</div>

---

# Pulse

**Pulse is the execution engine for Runtime Conformance.**

It is a slim, embeddable test runner for .NET host applications — Blazor, .NET MAUI, WPF, WinForms, Avalonia, Uno, console hosts, and anything that boots an `IServiceProvider`. Pulse executes `[PulseCase]` and `[PulseMatrix]` suites in-process with the consumer's real DI graph, host services, and platform APIs, then returns a strongly-typed `TestRunReport`.

> **Status:** v1 preview (`1.0.0-preview2`). The public API is intentionally small. The JSON shape of `TestRunReport` is the stability contract; changes are additive only.

## The Problem

Modern applications depend on runtime behavior that cannot be faithfully validated using mocks:

- **localStorage**, **sessionStorage**, **IndexedDB** in the browser
- **Windows DPAPI**, **Keychain**, **SecureStorage** on device
- Platform lifecycle events: **suspend**, **resume**, **backgrounding**
- **Network detection**, **connectivity changes**
- **Dispatchers**, **message pumps**, **thread affinity**
- **Browser APIs**, **platform notifications**

These are real runtime concerns. A unit test with a fake `IJSRuntime` cannot tell you whether `localStorage` actually works in the real browser. A mocked `HttpClient` cannot tell you whether the host configured the base address correctly. A fake dispatcher cannot prove the real UI thread processes work in order.

Runtime correctness demands runtime execution.

## The Missing Testing Layer

The testing landscape has a structural gap:

```text
Unit Tests
  │  Validates deterministic logic in isolation using fakes and mocks.
  │
  ▼
Integration Tests
  │  Validates communication with external systems (databases, APIs, message queues).
  │
  ▼
Runtime Conformance  ◀── Pulse lives here
  │  Validates that real runtime implementations satisfy their behavioral contracts
  │  under production conditions — using the real OS, browser, platform services,
  │  lifecycle, storage, and host environment.
  │
  ▼
End-to-End Tests
     Validates complete user workflows from the outside.
```

Each layer answers a distinct question:

| Layer | Question it answers |
|---|---|
| **Unit Tests** | Does this function produce the right output for a given input? |
| **Integration Tests** | Does this system communicate correctly with its external dependencies? |
| **Runtime Conformance** | Does this runtime implementation honor its behavioral contract in the real environment? |
| **End-to-End Tests** | Does the user's workflow complete successfully from start to finish? |

Unit tests validate deterministic code. Integration tests validate external communication. End-to-end tests validate user workflows. **Runtime Conformance validates that the platform itself behaves correctly under your abstractions.**

## The Runtime Rule

Determining whether a test belongs in Runtime Conformance comes down to a single architectural question:

> Does validating this behavior require the **real runtime**, operating system, browser, platform service, lifecycle, storage implementation, or host environment?

If the answer is yes, it belongs in Runtime Conformance.

The runtime — not Pulse — is the architectural boundary. Pulse is simply the execution engine that makes it possible to run these validations inside the real app.

## What Runtime Conformance Validates

Runtime Conformance validates three things:

1. **Behavior** — Does the real runtime implementation behave as specified?
2. **Contracts** — Does the implementation satisfy its contract under real conditions?
3. **Runtime correctness** — Does the implementation work correctly when the platform is involved?

It does **not** validate implementation details. The conformance target is the observable behavior at the boundary, not the internal wiring.

**Pulse executes specifications against real runtime implementations.** The specification describes *what* the behavior should be. Pulse proves it holds inside the real environment.

## Quick Start

```pwsh
dotnet add package Circuids.Pulse --version 1.0.0-preview2
```

Targets `net8.0`, `net9.0`, and `net10.0`.

### Runtime dependencies

| Dependency | Purpose |
|---|---|
| `Microsoft.Extensions.DependencyInjection.Abstractions` | Pulse resolves suites through the host service provider. |
| `Microsoft.Testing.Platform` | Pulse hosts MTP in-process as the execution engine. |

There is no `Microsoft.Testing.Platform.MSBuild` dependency. Pulse is hosted by your app, not discovered by `dotnet test` or Test Explorer.

### 1. Register Pulse

```csharp
builder.Services.AddPulse(p =>
{
    p.AssignedPlatform = "Blazor.WebAssembly";
    p.DefaultTestTimeout = TimeSpan.FromSeconds(10);
    p.AddSuite<HttpClientSuite>();
});
```

### 2. Write suites

Suites are plain classes resolved through the same `IServiceProvider` as the rest of your app:

```csharp
public sealed class HttpClientSuite
{
    private readonly HttpClient _http;

    public HttpClientSuite(HttpClient http)
    {
        _http = http;
    }

    [PulseCase]
    public void HttpClient_has_an_absolute_base_address()
    {
        PulseAssert.NotNull(_http.BaseAddress, "BaseAddress must be configured.");
        PulseAssert.True(_http.BaseAddress!.IsAbsoluteUri,
            $"Expected absolute URI, got {_http.BaseAddress}.");
    }

    [PulseCase(TimeoutMs = 5000)]
    public async Task GET_root_returns_success(CancellationToken ct)
    {
        using var response = await _http.GetAsync("", ct);
        PulseAssert.True(response.IsSuccessStatusCode,
            $"Expected 2xx, got {(int)response.StatusCode}.");
    }
}
```

### 3. Run and inspect the report

```csharp
var report = await executor.RunAsync();

Console.WriteLine(report.Success ? "All conformance tests passed." : "Failures detected.");
// report.Total, report.Passed, report.Failed, report.Skipped, report.Results, report.RuntimeEnvironment
```

Pulse is report-first: `RunAsync` returns a failed report for failed tests. It does not throw because a test case failed.

## Runner-Neutral Specifications

The strongest Pulse pattern is: **write the behavior once, run it everywhere.**

Specifications should depend only on:

- Your product abstractions (interfaces, models)
- The .NET BCL
- Shared assertion helpers (or ordinary exceptions)

Specifications should **never** depend on:

- Pulse
- xUnit, NUnit, MSTest
- DI containers
- Mocking libraries

This keeps the specification reusable across runners and test types.

### Shared spec (no runner dependency)

```csharp
public abstract class TokenStorageSpec
{
    protected abstract ITokenStorage CreateStorage();

    protected async Task Store_then_retrieve_returns_same_value_core(CancellationToken ct = default)
    {
        var storage = CreateStorage();
        await storage.StoreAsync("auth", "abc-123", ct);

        var actual = await storage.RetrieveAsync("auth", ct);
        if (actual != "abc-123")
        {
            throw new InvalidOperationException(
                $"Expected stored value to round-trip. Expected 'abc-123', got '{actual}'.");
        }
    }
}
```

### Unit test adapter (fake runtime)

```csharp
public sealed class InMemoryTokenStorageTests : TokenStorageSpec
{
    protected override ITokenStorage CreateStorage() => new InMemoryTokenStorage();

    [Fact]
    public Task Store_then_retrieve_returns_same_value()
        => Store_then_retrieve_returns_same_value_core(TestContext.Current.CancellationToken);
}
```

### Pulse adapter (real runtime)

```csharp
public sealed class BrowserTokenStorageSuite : TokenStorageSpec, IPulseLifetime
{
    private readonly ITokenStorage _storage;

    public BrowserTokenStorageSuite(ITokenStorage storage)
    {
        _storage = storage;
    }

    protected override ITokenStorage CreateStorage() => _storage;

    public Task InitializeAsync(CancellationToken ct) => ClearKnownKeysAsync(ct);
    public Task DisposeAsync(CancellationToken ct) => ClearKnownKeysAsync(ct);

    [PulseCase(TimeoutMs = 5000)]
    public Task Store_then_retrieve_returns_same_value(CancellationToken ct)
        => Store_then_retrieve_returns_same_value_core(ct);
}
```

The same specification validates `ITokenStorage` against an in-memory fake in `dotnet test` and against real browser `localStorage` in Pulse. The spec never changes. **Specs define behavior. Adapters choose the runtime.**

For the complete rulebook on shared specs, adapters, and boundary-focused matrices, see [Conformance Specs And Rules](docs/conformance-specs-and-rules.md).

## Writing Suites

Use `[PulseCase]` for individual tests and `[PulseMatrix]` with `[PulseRow]` for parameterized rows. Every matrix row produces its own `TestResult`.

```csharp
public sealed class BrowserStorageBoundarySuite : KeyValueStoreSpec, IPulseLifetime
{
    private readonly IJSRuntime _js;

    public BrowserStorageBoundarySuite(IJSRuntime js)
    {
        _js = js;
    }

    protected override IConformanceKeyValueStore CreateStore() => new BrowserLocalStorageStore(_js);

    public Task InitializeAsync(CancellationToken ct) => ClearKnownKeysAsync(ct);
    public Task DisposeAsync(CancellationToken ct) => ClearKnownKeysAsync(ct);

    [PulseCase(TimeoutMs = 2000)]
    public Task Set_then_get_round_trips_value(CancellationToken ct)
        => Set_then_get_round_trips_value_core(ct);

    [PulseMatrix(DisplayName = "localStorage batch round-trip", TimeoutMs = 3000)]
    [PulseRow(1)]
    [PulseRow(5)]
    [PulseRow(20)]
    public async Task Batch_round_trips_under_budget(int count, CancellationToken ct)
    {
        var store = CreateStore();
        var keys = Enumerable.Range(0, count)
            .Select(i => $"conformance.batch.{i}").ToArray();

        try
        {
            foreach (var key in keys)
            {
                await store.SetAsync(key, key, ct);
                PulseAssert.Equal(key, await store.GetAsync(key, ct),
                    "localStorage must return the value just written.");
            }
        }
        finally
        {
            foreach (var key in keys)
                await store.RemoveAsync(key, CancellationToken.None);
        }
    }
}
```

Use `PulseAssert` in Pulse-only suites and Pulse adapter bodies. Shared `*.TestSupport` specs stay runner-agnostic and throw ordinary BCL exceptions.

### Execution semantics

| Aspect | Behavior |
|---|---|
| Registration | Only explicitly registered suites run. No assembly scanning. |
| Suite filter | `RunAsync(string suiteName)` matches `Type.FullName` exactly. |
| Order | Sequential. Boundary tests share real host state. |
| Construction failure | Recorded as a failed `(suite construction)` result; later suites continue. |
| Test failure | The thrown exception becomes a failed `TestResult`. `RunAsync` does not throw. |
| Runtime skip | Throw `PulseSkipException`, call `PulseAssert.Skip(...)`, or throw xUnit's skip exception by name. |
| Cancellation | `RunAsync(ct)` is honored between tests. Mid-test deadlines require a trailing `CancellationToken`. |
| Timeouts | Cooperative. Pulse creates a linked token when the method declares a trailing `CancellationToken`. No thread abort. |

## Report Contract

`TestRunReport` is the integration contract for UIs, CI, and dashboards:

```csharp
public sealed record TestRunReport
{
    public string Schema { get; init; } = "pulse/v1";
    public required string AssignedPlatform { get; init; }
    public required RuntimeEnvironment RuntimeEnvironment { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required IReadOnlyList<TestResult> Results { get; init; }
    public TimeSpan Duration { get; init; }
    public int Total { get; }
    public int Passed { get; }
    public int Failed { get; }
    public int Skipped { get; }
    public bool Success { get; }
}
```

Serialize with the source-generated context:

```csharp
var json = JsonSerializer.Serialize(report, PulseJsonContext.Default.TestRunReport);
```

### Human-readable formatting

Use `TestRunReportFormatter.Format` to produce Pulse's canonical plain-text representation for console output, CI logs, or bug reports:

```csharp
var report = await executor.RunAsync();
Console.WriteLine(TestRunReportFormatter.Format(report));
```

The formatter is a pure transformation with zero knowledge of Console, Terminal, logging, or DI. It produces deterministic plain text — no ANSI codes — suitable for any text sink.

```
────────────────────────────────────────────────
Pulse Test Run Report
────────────────────────────────────────────────

Status   : Passed
Duration : 2.14 s
Platform : Blazor.WebAssembly

Runtime
  .NET      : .NET 10.0.2
  OS        : Microsoft Windows 10.0.26100
  Runtime   : browser-wasm
  Processor : Wasm (16 cores)
  Machine   : MYMACHINE

Summary
  Passed  : 18
  Failed  : 0
  Skipped : 0
  Total   : 18

────────────────────────────────────────────────
Results
────────────────────────────────────────────────

✓ HttpClientSuite (2 tests, 215 ms)
  ✓ GET_root_returns_success (112 ms)
  ✓ HttpClient_has_an_absolute_base_address (103 ms)
```

The report is additive-only. Future fields may appear; existing fields retain their meaning under the `pulse/v1` schema.

## What Runtime Conformance Is NOT

| Scenario | Test Type |
|---|---|
| Pure deterministic logic | Unit Test |
| JSON serialization / deserialization | Unit Test |
| Firebase Emulator | Integration Test |
| SQLite in-memory database | Integration Test |
| IndexedDB quota exhaustion | **Runtime Conformance** |
| Windows DPAPI / Keychain | **Runtime Conformance** |
| Android lifecycle (suspend / resume) | **Runtime Conformance** |
| Browser `localStorage` round-trip | **Runtime Conformance** |
| MAUI `Preferences.Default` on device | **Runtime Conformance** |
| WinForms message pump dispatch | **Runtime Conformance** |
| OAuth login flow | End-to-End |
| Clicking buttons through the UI | End-to-End |

The boundary is clear: if removing the real runtime makes the test meaningless, it is Runtime Conformance.

## Pulse's Responsibility

Pulse owns **runtime behavioral validation**. It is deliberately focused.

Pulse intentionally does **not** attempt to become:

- A unit test framework
- An integration test framework
- A device orchestration framework
- A UI automation framework
- A reporting or dashboarding system
- A CLI runner

Keeping this responsibility narrow is an architectural decision. Pulse complements `dotnet test`, xUnit, NUnit, Playwright, and XHarness — it does not compete with them.

## Hosting Pulse

Pulse suites live inside your actual application. The recommended project layout:

```text
MyProduct.App/                 # Blazor / MAUI / WPF / WinForms
  Program.cs / MauiProgram.cs  # AddPulse(...) registered here
  Conformance/                 # Pulse suites over real platform services

MyProduct.Tests/               # dotnet test project
  TokenStorageTests.cs         # adapter over fakes

MyProduct.TestSupport/         # pure support library
  TokenStorageSpec.cs          # shared abstract spec
  Fakes/
  Builders/

MyProduct.ConformanceHost/     # optional dedicated app host for Pulse
  Conformance/
```

Pulse does not ship a CLI or reporter framework. Automation starts from inside the real host app. Common patterns:

| Pattern | Shape |
|---|---|
| Diagnostic endpoint | Expose a protected endpoint that calls `ITestExecutor.RunAsync()` and returns serialized JSON. |
| Startup flag | Run Pulse at startup with a config flag, emit JSON, then exit or mark the host unhealthy. |
| Dedicated conformance host | Boot a small `*.ConformanceHost` that registers the same platform services and runs Pulse for CI. |

```csharp
app.MapPost("/_pulse/run", async (ITestExecutor executor, CancellationToken ct) =>
{
    var report = await executor.RunAsync(ct);
    var json = JsonSerializer.Serialize(report, PulseJsonContext.Default.TestRunReport);
    return Results.Text(json, "application/json");
});
```

Authentication, retries, and storage policy belong to your app. Pulse provides the stable report; the host owns transport.

## What's In The Box

- `[PulseCase]`, `[PulseMatrix]`, and `[PulseRow]` for explicit suite discovery
- `PulseAssert` — a focused assertion library with consistent failure messages
- `IPulseLifetime` for per-suite `InitializeAsync` / `DisposeAsync`; `IDisposable` and `IAsyncDisposable` also honored
- Cooperative per-test timeout/cancellation through trailing `CancellationToken` injection
- `TestRunReport`, `TestResult`, `TestOutcome`, and `RuntimeEnvironment` models
- `PulseJsonContext` for source-generated `System.Text.Json` serialization
- `TestRunReportFormatter` for canonical plain-text report rendering
- `RuntimeEnvironment` registered as a DI singleton

## What's Not In The Box

Pulse stays small by design:

- No `Circuids.Pulse.Blazor`, `.Maui`, `.Wpf`, `.WinForms`, `.Avalonia`, `.Uno`, `.Reporters`, or `.TestExplorer` packages
- No Test Explorer integration — Pulse tests only make sense inside the real app host
- No reporter framework — serialize `TestRunReport` directly
- No mocking framework, fixture container, parallel execution, auto-retry, member-data row source, test ordering attributes, or fluent assertion DSL
- No `Microsoft.Testing.Platform.MSBuild`

## A Failure Pulse Is Meant To Catch

A unit test can prove your `IWeatherClient` handles a successful response from a fake `HttpMessageHandler`. That is useful, but it cannot prove the Blazor host registered `HttpClient.BaseAddress` correctly:

```csharp
public sealed class WeatherClientHostSuite
{
    private readonly HttpClient _http;

    public WeatherClientHostSuite(HttpClient http)
    {
        _http = http;
    }

    [PulseCase]
    public void Host_configured_HttpClient_base_address()
    {
        PulseAssert.NotNull(_http.BaseAddress,
            "The running host must configure HttpClient.BaseAddress.");
        PulseAssert.True(_http.BaseAddress!.IsAbsoluteUri,
            "Relative requests must resolve in the real app.");
    }
}
```

If the fake-backed unit test passes but this Pulse case fails in the real host, Pulse is telling you something important: the abstraction is plausible, but the app wiring is broken.

## Samples

Reference consumers under [`sample/`](sample/):

- [`Circuids.Pulse.Blazor.WebAssembly.ConformanceHost`](sample/Circuids.Pulse.Blazor.WebAssembly.ConformanceHost/) — suites inside a live WASM runtime
- [`Circuids.Pulse.Maui.ConformanceHost`](sample/Circuids.Pulse.Maui.ConformanceHost/) — suites inside a live MAUI host on Android, iOS, MacCatalyst, and Windows
- [`Circuids.Pulse.WinForms.ConformanceHost`](sample/Circuids.Pulse.WinForms.ConformanceHost/) — suites inside a real WinForms message loop
- [`Circuids.Pulse.WPF.ConformanceHost`](sample/Circuids.Pulse.WPF.ConformanceHost/) — suites inside a real WPF dispatcher
- [`Circuids.Pulse.TestSupport`](sample/Circuids.Pulse.TestSupport/) — pure shared specs used by host-specific suites

Samples are copy-paste references, not packages. Host integration belongs to the consuming app.

## Troubleshooting

| Symptom | What to check |
|---|---|
| Test Explorer does not show Pulse tests | Expected. Pulse is not a `dotnet test` project. |
| A timeout did not stop a hanging test | The method must accept a trailing `CancellationToken` and observe it. |
| `RunAsync(string suiteName)` returns an empty report | Pass the suite type's exact `FullName`; matching is ordinal and case-sensitive. |
| Shared specs need assertions | In `*.TestSupport`, throw ordinary BCL exceptions. Use `PulseAssert` only in Pulse suites or adapter bodies. |
| A Pulse suite needs fake dependencies | Keep the conformance target real. Non-target dependencies can be fake. |

## Contributing

Issues, discussions, and pull requests are welcome on [github.com/Circuids/Pulse](https://github.com/Circuids/Pulse). Pulse keeps a deliberately small surface: one package, two runtime dependencies, sequential execution, cooperative cancellation, and additive-only reports. Please open an issue before any change that touches public API or adds a dependency.

If Pulse saves you time, you can support continued work through GitHub Sponsors:

[**Sponsor Circuids on GitHub →**](https://github.com/sponsors/Circuids)

## License

Circuids.Pulse is released under the [MIT License](LICENSE). Copyright Circuids.
