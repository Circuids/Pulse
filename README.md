<div align="center">
  <img src="https://github.com/Circuids/Pulse/blob/master/images/cover_logo_min.jpg?raw=true" alt="Pulse logo" height="250" width="1000" />
</div>

<div align="center">

[![NuGet](https://img.shields.io/nuget/v/Circuids.Pulse.svg)](https://nuget.org/packages/Circuids.Pulse/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/Circuids/Pulse/blob/master/LICENSE)

</div>

# Pulse

Run tests inside your real app, not a test host.

Pulse runs your tests inside the real app so you can verify behavior where it actually matters: in the runtime your users get.

It is a slim, embeddable test runner for .NET host applications: Blazor, .NET MAUI, WPF, WinForms, Avalonia, Uno, console hosts, or anything that boots an `IServiceProvider`. Pulse executes `[PulseCase]` and `[PulseMatrix]` suites in-process with the consumer's real DI graph, host services, and platform APIs, then returns a strongly typed `TestRunReport` you can render, serialize, post, or store however you like.

Pulse runs next to `dotnet test`, never instead of it. Pulse conformance tests are an extra verification layer, not a replacement for unit, integration, UI, end-to-end, or other existing test types. Unit tests prove your abstraction is internally consistent. Pulse proves the same boundary behavior inside the runtime that ships to users.

> **Status:** v1 preview (`1.0.0-preview1`). This preview is intended for real project adoption and production conformance pilots while the .NET community reviews the architecture and suggests improvements before stable v1. The public API is intentionally small, and the JSON shape of `TestRunReport` is the stability contract. Report changes are additive.

## Why Pulse Exists

Some failures only appear inside the real runtime host:

- incorrect DI registration in the actual app host
- runtime integration mismatches across platforms
- platform-specific service behavior
- dispatcher and thread-affinity issues
- `IJSRuntime`, MAUI, WPF, or other host-binding integration issues
- `HttpClient` and other runtime configuration differences between fakes and the real host

Pulse validates behavior where the application actually runs, using the real DI container, runtime services, and platform integrations.

Pulse is especially useful for reusable libraries and framework-level components that need to behave consistently across multiple application hosts: runtime abstractions, storage and auth integrations, dispatcher abstractions, platform services, Blazor and MAUI integrations, and other host-bound infrastructure.

## Where Pulse Fits

| Tool | Primary focus |
|---|---|
| xUnit / NUnit / MSTest | Isolated logic and integration testing. |
| WebApplicationFactory | ASP.NET Core host testing in a synthetic test host. |
| Playwright / Selenium | Browser and UI automation. |
| XHarness / DeviceRunners | Launching tests on platforms and devices. |
| Pulse | Runtime validation inside the real application host. |

Pulse complements existing testing tools rather than replacing them. A typical layering looks like this:

```text
XHarness / DeviceRunners
 └─ launches the platform or device

Application Host (Blazor, MAUI, WPF, WinForms, …)
 ├─ DI Container
 ├─ Platform Services
 ├─ Runtime Integrations
 └─ Pulse
      └─ runtime validation inside the running host
```

Pulse is not a UI automation framework, a Playwright or Selenium replacement, a device runner, an XHarness or DeviceRunners replacement, or a replacement for xUnit, NUnit, or MSTest.

## Install

```pwsh
dotnet add package Circuids.Pulse --version 1.0.0-preview1
```

Targets `net8.0`, `net9.0`, and `net10.0`.

Runtime dependencies:

| Dependency | Why it is there |
|---|---|
| `Microsoft.Extensions.DependencyInjection.Abstractions` | Pulse resolves suites through the host service provider. |
| `Microsoft.Testing.Platform` | Pulse hosts MTP in-process as the execution engine. |

There is no `Microsoft.Testing.Platform.MSBuild` dependency. Pulse is hosted by your app, not discovered by `dotnet test` or Test Explorer.

## Try Pulse In 5 Minutes

Register Pulse with your host services:

```csharp
builder.Services.AddPulse(p =>
{
    p.AssignedPlatform = "Blazor.WebAssembly";
    p.DefaultTestTimeout = TimeSpan.FromSeconds(10);
    p.AddSuite<HttpClientSuite>();
});
```

Write a suite. Suites are plain classes resolved through the same `IServiceProvider` as the rest of your app:

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
        PulseAssert.True(_http.BaseAddress!.IsAbsoluteUri, $"Expected absolute URI, got {_http.BaseAddress}.");
    }

    [PulseCase(TimeoutMs = 5000)]
    public async Task GET_root_returns_success(CancellationToken ct)
    {
        using var response = await _http.GetAsync("", ct);
        PulseAssert.True(response.IsSuccessStatusCode, $"Expected 2xx, got {(int)response.StatusCode}.");
    }
}
```

Run the suite from inside the running app:

```csharp
@inject ITestExecutor Executor

var report = await Executor.RunAsync();
// report.Success, report.Total, report.Failed, report.Results, report.RuntimeEnvironment
```

Render the report in your app UI, write it to a file, post it to a dashboard, or fail a CI step after inspecting `report.Success`. Pulse is report-first: `RunAsync` returns a failed report for failed tests; it does not throw just because a test case failed.

## Pulse In Your Test Strategy

Pulse suites live inside your actual application. You do not run `dotnet test` on a Pulse host, and Pulse does not extend xUnit, NUnit, or MSTest.

Think of Pulse conformance tests as a host-runtime layer in your existing test strategy. Keep your unit, integration, UI, end-to-end, and contract tests where they already provide value; add Pulse where the real app host, DI graph, or platform service is part of the behavior that must be proven.

The typical product layout is:

```text
MyProduct.App/                 # Blazor / MAUI / WPF / WinForms / etc.
  Program.cs / MauiProgram.cs  # AddPulse(...) registered here
  Conformance/                 # Pulse suites over real platform services

MyProduct.Tests/               # ordinary dotnet test project
  TokenStorageTests.cs         # adapter over fakes/in-memory implementations

MyProduct.TestSupport/         # optional pure support library
  TokenStorageSpec.cs          # shared abstract spec, no test-framework attributes
  Fakes/
  Builders/

MyProduct.ConformanceHost/     # optional dedicated app host for Pulse runs
  Conformance/                 # Pulse adapters over real app services
```

Use `*.TestSupport` for reusable specs, fakes, builders, and sample data. Use `*.Tests` for `dotnet test`. Use `*.ConformanceHost` when you want a dedicated real app host for Pulse instead of putting suites in the production app.

For the detailed TestSupport/spec rules, see [Conformance Specs And Rules](docs/conformance-specs-and-rules.md).

## When To Use Pulse

| Use this | When you need to prove | Typical dependencies |
|---|---|---|
| `dotnet test` | Pure logic, contracts, error handling, serialization, fast feedback. | Fakes, mocks, in-memory stores, normal test frameworks. |
| Pulse | Behavior at the boundary where the real host matters. | Real DI graph, real `IJSRuntime`, real MAUI services, real `HttpClient`, real platform APIs. |
| Both | A reusable abstraction must behave the same over fakes and the real platform implementation. | Shared `*.TestSupport` spec plus thin adapters. |

Pulse is additive. It does not eliminate mocks, replace existing test suites, or turn every behavior check into an in-app test. The conformance target should be real in a Pulse run; supporting dependencies can still be fake when appropriate.

## A Failure Pulse Is Meant To Catch

An ordinary unit test can prove your `IWeatherClient` handles a successful response from a fake `HttpMessageHandler`. That is useful, but it cannot prove the Blazor host actually registered `HttpClient.BaseAddress` correctly.

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
        PulseAssert.NotNull(_http.BaseAddress, "The running host must configure HttpClient.BaseAddress.");
        PulseAssert.True(_http.BaseAddress!.IsAbsoluteUri, "Relative requests must resolve in the real app.");
    }
}
```

If the fake-backed `dotnet test` passes but this Pulse case fails in the real host, Pulse is telling you something important: the abstraction is plausible, but the app wiring is wrong.

## Shared Conformance Specs

The strongest Pulse pattern is: write the behavior once, run it against fakes with `dotnet test`, then run the same behavior inside the real app with Pulse.

The shared spec lives in a pure support library. It has no Pulse reference, no xUnit/NUnit/MSTest reference, no DI dependency, and no test attributes. It fails with ordinary BCL exceptions.

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

The `dotnet test` adapter chooses the fake runtime:

```csharp
public sealed class InMemoryTokenStorageTests : TokenStorageSpec
{
    protected override ITokenStorage CreateStorage() => new InMemoryTokenStorage();

    [Fact]
    public Task Store_then_retrieve_returns_same_value()
        => Store_then_retrieve_returns_same_value_core(TestContext.Current.CancellationToken);
}
```

The Pulse adapter chooses the real host implementation:

```csharp
public sealed class BrowserTokenStorageSuite : TokenStorageSpec
{
    private readonly ITokenStorage _storage;

    public BrowserTokenStorageSuite(ITokenStorage storage)
    {
        _storage = storage;
    }

    protected override ITokenStorage CreateStorage() => _storage;

    [PulseCase(TimeoutMs = 5000)]
    public Task Store_then_retrieve_returns_same_value(CancellationToken ct)
        => Store_then_retrieve_returns_same_value_core(ct);
}
```

**Specs define behavior. Adapters choose the runtime.**

An interface-shaped spec can work as an alternate checklist when the body is tiny or each adapter should use its native assertion library, but the abstract implementation spec is the canonical v1 pattern because it keeps one behavior body shared across both runtimes.

The full rulebook for shared specs, adapters, TestSupport, cleanup, and boundary-focused matrices lives in [Conformance Specs And Rules](docs/conformance-specs-and-rules.md).

## Writing Pulse Suites

Use `[PulseCase]` for one test and `[PulseMatrix]` with `[PulseRow]` for parameterized rows. Every matrix row becomes its own `TestResult`.

```csharp
public sealed class ViewportMatrixSuite
{
    [PulseMatrix(DisplayName = "Aspect ratio classification")]
    [PulseRow(390, 844, "portrait")]
    [PulseRow(1920, 1080, "landscape")]
    [PulseRow(768, 768, "square")]
    public void Aspect_ratio_is_classified(int width, int height, string expected)
    {
        var actual = width > height ? "landscape"
            : width < height ? "portrait"
            : "square";

        PulseAssert.Equal(expected, actual, $"Classification for {width}x{height}.");
    }
}
```

Use `PulseAssert` in Pulse-only suites and concrete Pulse adapter bodies. Shared `*.TestSupport` specs should stay runner-agnostic and throw ordinary exceptions instead.

## Execution Semantics

| Aspect | Behavior |
|---|---|
| Registration | Only explicitly registered suites run: `PulseBuilder.AddSuite<T>()` or factory overloads. No assembly scanning. |
| Suite filter | `RunAsync(string suiteName)` matches the registered type's `Type.FullName` exactly. |
| Order | Suites and tests run sequentially. Boundary tests share real host state, so Pulse does not parallelize. |
| Construction failure | Recorded as a failed `(suite construction)` result; later suites continue. |
| Discovery failure | Recorded as a failed `(discovery)` result; constructed suites are still disposed. |
| Initialization failure | Records `(suite InitializeAsync)`, skips discovered tests in that suite, tears down, then continues with the next suite. |
| Test failure | The thrown exception becomes a failed `TestResult`; ordinary test failures do not make `RunAsync` throw. |
| Runtime skip | Throw `PulseSkipException`, `PulseAssert.Skip(...)`, or xUnit's skip exception by name. |
| Re-entrancy | One active run per Pulse registration/service provider. Concurrent runs throw `InvalidOperationException`. |
| Cancellation | `RunAsync(ct)` is honored between tests. Mid-test deadlines require the test to accept and honor a trailing `CancellationToken`. |

Timeouts are cooperative. `[PulseCase(TimeoutMs = 5000)]` or `PulseBuilder.DefaultTestTimeout` creates a linked token only when the test method declares a trailing `CancellationToken`. Pulse never aborts threads.

## Report Contract

`TestRunReport` is the integration contract for UIs, CI, and dashboards.

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

Serialize reports with the source-generated context:

```csharp
var json = JsonSerializer.Serialize(report, PulseJsonContext.Default.TestRunReport);
```

The report is additive-only. Future fields may appear, but existing fields keep their meaning for the `pulse/v1` schema.

## CI Trigger Patterns

Pulse does not ship a CLI, reporter framework, or upload sink. Automation should still start from inside the real host app.

Common host-owned patterns:

| Pattern | Shape |
|---|---|
| Diagnostic endpoint | Map a protected endpoint that calls `ITestExecutor.RunAsync()` and returns serialized JSON. |
| Startup flag | If a config value or command-line flag is set, run Pulse at startup, emit JSON, then exit or mark the host unhealthy. |
| Dedicated conformance host | Boot a small `*.ConformanceHost` app that registers the same platform services and runs Pulse for CI. |

Minimal endpoint example:

```csharp
app.MapPost("/_pulse/run", async (ITestExecutor executor, CancellationToken ct) =>
{
    var report = await executor.RunAsync(ct);
    var json = JsonSerializer.Serialize(report, PulseJsonContext.Default.TestRunReport);
    return Results.Text(json, "application/json");
});
```

Keep endpoint auth, upload retries, redaction, and storage policy in your app. Pulse provides the stable report; the host owns transport.

## What Is In The Box

- `[PulseCase]`, `[PulseMatrix]`, and `[PulseRow]` for explicit suite discovery.
- `PulseAssert`, a focused assertion library with consistent failure messages and no fluent surface.
- `IPulseLifetime` for per-suite `InitializeAsync` / `DisposeAsync`; `IDisposable` and `IAsyncDisposable` are also honored.
- Cooperative per-test timeout/cancellation through trailing `CancellationToken` injection.
- `TestRunReport`, `TestResult`, `TestOutcome`, and `RuntimeEnvironment` models.
- `PulseJsonContext` for source-generated `System.Text.Json` serialization.
- `RuntimeEnvironment` registered as a DI singleton so suites can inject it directly.

## What Is Not In The Box

Pulse stays small on purpose:

- No `Circuids.Pulse.Blazor`, `.Maui`, `.Wpf`, `.WinForms`, `.Avalonia`, `.Uno`, `.Reporters`, `.TestExplorer`, or `.Specs` package.
- No Test Explorer integration. Pulse tests only make sense inside the real app host.
- No reporter framework. Serialize `TestRunReport` directly.
- No official conformance helper package. Use your own `*.TestSupport` library for reusable specs and fakes.
- No mocking framework, fixture container, parallel execution, auto-retry, member-data row source, test ordering attributes, or fluent assertion DSL.
- No `Microsoft.Testing.Platform.MSBuild` in the runtime closure.

## Samples

Reference consumers live under [`sample/`](sample/):

- [`Circuids.Pulse.Blazor.WebAssembly.ConformanceHost`](sample/Circuids.Pulse.Blazor.WebAssembly.ConformanceHost/) runs suites inside a live WASM runtime and renders results at the app root.
- [`Circuids.Pulse.Maui.ConformanceHost`](sample/Circuids.Pulse.Maui.ConformanceHost/) runs suites inside the live MAUI host on Android, iOS, MacCatalyst, and Windows.
- [`Circuids.Pulse.WinForms.ConformanceHost`](sample/Circuids.Pulse.WinForms.ConformanceHost/) runs suites inside a real WinForms message loop.
- [`Circuids.Pulse.WPF.ConformanceHost`](sample/Circuids.Pulse.WPF.ConformanceHost/) runs suites inside a real WPF dispatcher.
- [`Circuids.Pulse.TestSupport`](sample/Circuids.Pulse.TestSupport/) contains pure shared specs used by the host-specific suites.

Samples are copy-paste references, not packages. The host integration belongs to the consuming app.

## Troubleshooting

| Symptom | What to check |
|---|---|
| Test Explorer does not show Pulse tests. | Expected. Pulse is not a `dotnet test` project and does not integrate with Test Explorer. |
| A timeout did not stop a hanging test. | The method must accept a trailing `CancellationToken`, and the body must pass/observe that token. |
| A debugger pauses on `PulseAssert` failure. | Assertion failures are real exceptions caught by Pulse. Debugger break behavior depends on the host/debugger settings. Normal sweeps should run without a debugger attached. |
| `RunAsync(string suiteName)` returns an empty report. | Pass the suite type's exact `FullName`; matching is ordinal and case-sensitive. |
| Shared specs need assertions. | In `*.TestSupport`, throw ordinary BCL exceptions. Use `PulseAssert` only in Pulse suites or concrete Pulse adapter bodies. |
| A Pulse suite needs fake dependencies. | Keep the conformance target real. Non-target dependencies can be fake or stubbed when that keeps the boundary under test focused. |

## Trust Boundaries

Pulse does not modify your test runner, require a custom host, scan assemblies, create a UI, or interfere with existing `dotnet test` projects. It runs only the suites you explicitly register from inside the host application you already control.

## Contributing

Issues, discussions, and pull requests are welcome on [github.com/Circuids/Pulse](https://github.com/Circuids/Pulse). Pulse keeps a deliberately small surface: one package, two runtime dependencies, sequential execution, cooperative cancellation, and additive-only reports. Please open an issue before any change that touches public API or adds a dependency.

If Pulse saves you time, you can support continued work through GitHub Sponsors:

[**Sponsor Circuids on GitHub ->**](https://github.com/sponsors/Circuids)

## License

Circuids.Pulse is released under the [MIT License](LICENSE). Copyright Circuids.
