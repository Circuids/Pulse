# Pulse

A slim, embeddable test runner for .NET that runs your conformance tests **inside a real running host app** — Blazor, .NET MAUI, WPF, WinForms, Avalonia, Uno, anything that boots a `IServiceProvider` — using the consumer's real DI graph and real platform services. Built on `Microsoft.Testing.Platform`, hosted in-process, returning a strongly-typed `TestRunReport` you render however you like.

Pulse runs *next to* `dotnet test`, never instead of it. When Pulse passes, the equivalent boundary tests in `dotnet test` are redundant — because Pulse just exercised them against the real `IJSRuntime`, the real `Preferences.Default`, the real `HttpClient`, the real platform.

> **Status:** experimental (`0.1.0-experimental` on NuGet). The public API surface is small and intentional, but it may shift before 1.0. The JSON shape of `TestRunReport` is the only stability contract today.

## Why this exists

`dotnet test` runs your code under .NET's test host, not under your host app. The test process has no `IJSRuntime`, no `MainThread` dispatcher, no `IServiceProvider` shaped like your app's. That's fine for unit tests, and a poor fit for the boundary code that *only matters* when wired to the real platform.

Pulse fills that gap. You write one abstract conformance spec; one subclass tags it `[Fact]` for `dotnet test` against fakes, the other tags it `[PulseCase]` and runs it inside the real app against the real services. Same spec, same assertions, two runtimes — and one of them is the runtime that ships to the user.

## Pulse is not a test project

This is the most important thing to understand before you start:

**Pulse is not a `*.Tests` project. You do not run `dotnet test` on it. It does not replace or extend xUnit, NUnit, or MSTest.**

Pulse suites live inside your actual application — registered in the same `IServiceCollection` as your services, resolved by the same `IServiceProvider` that runs the rest of the app. The runner is triggered by calling `ITestExecutor.RunAsync()` from within the running app: a button click, a startup hook, a diagnostic endpoint, anything you want. The results come back as a plain `TestRunReport` record that you handle yourself.

Your xUnit / NUnit / MSTest projects stay exactly as they are. Pulse sits alongside them, not inside them. The typical layout is:

```
MyApp/                    ← your Blazor / MAUI / WPF / etc. host app
  Services/
  Pages/
  MauiProgram.cs          ← AddPulse(...) registered here

MyApp.Tests/              ← ordinary dotnet test project (xUnit, NUnit, MSTest)
  FakeHttpClientTests.cs  ← tests against fakes and mocks, no platform needed

MyApp.Conformance/        ← optional shared contracts assembly, no test-framework references
  HttpClientConformance.cs  ← abstract spec, no [Fact] or [PulseCase]
```

The `MyApp` host runs Pulse against the real platform. `MyApp.Tests` runs xUnit against fakes. Both use the same abstract spec from `MyApp.Conformance`. Neither replaces the other.

```pwsh
dotnet add package Circuids.Pulse --prerelease
```

Targets `net8.0`, `net9.0`, `net10.0`. Two runtime dependencies: `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Testing.Platform`. That's it.

## Quick start

Register Pulse alongside the rest of your DI graph and point it at the suites you want to run:

```csharp
builder.Services.AddPulse(p =>
{
    p.AssignedPlatform = "Blazor.WebAssembly";
    p.DefaultTestTimeout = TimeSpan.FromSeconds(10);
    p.AddSuite<HttpClientSuite>();
    p.AddSuite<WebAssemblyHostSuite>();
});
```

Write a suite. Suites are plain classes — Pulse resolves them through your `IServiceProvider`, so they receive the same dependencies the rest of your app receives:

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

Need to sweep across many inputs? Use `[PulseMatrix]` + `[PulseRow]` — every row reports as its own independent test result:

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

Run the suites from anywhere your DI container can reach (a button click, a startup hook, a CI smoke endpoint):

```csharp
@inject ITestExecutor Executor

var report = await Executor.RunAsync();
// report.Success, report.Total, report.Passed/Failed/Skipped, report.Results, report.RuntimeEnvironment
```

The report is a record. Render it however you want — there's no built-in UI.

## What's in the box

- **`[PulseCase]`** for individual tests, **`[PulseMatrix]` + `[PulseRow]`** for parameterized rows. Both attributes carry `TimeoutMs` for per-test cooperative deadlines.
- **`PulseAssert`** — a focused middle-ground assertion library: `True/False`, `Equal/NotEqual` (with comparer), `Same/NotSame`, `Null/NotNull`, `Contains/StartsWith/EndsWith`, `Empty/NotEmpty`, `Equivalent`, `InRange`, `Throws/ThrowsAsync`, plus `Skip` and `Fail`. One consistent failure shape, invariant culture, no fluent surface.
- **`IPulseLifetime`** — optional `InitializeAsync` / `DisposeAsync` per suite. `IDisposable` and `IAsyncDisposable` on a suite are also honored at tear-down.
- **Per-test `CancellationToken` injection** — declare a trailing `CancellationToken` parameter and Pulse threads through a linked token bound to `TimeoutMs` (or `PulseBuilder.DefaultTestTimeout`). Cooperative cancellation only — Pulse never aborts threads.
- **`TestRunReport`** — strongly-typed, `System.Text.Json`-source-gen-friendly via `PulseJsonContext`. The JSON shape is the stable wire contract; field additions are always backward compatible.
- **`RuntimeEnvironment`** — captured once and registered as a DI singleton, so suites can inject it directly.

## What's *not* in the box (and won't be)

Pulse stays small on purpose. A few things are out of scope by design:

- A separate `Circuids.Pulse.Blazor` / `.Maui` / `.Wpf` package — there's one package, forever. Per-host integration is a copy-paste exercise demonstrated in [`sample/`](sample/).
- A reporter framework, a Test Explorer entry point, a mocking framework, parallel execution, auto-retry, member-data, or a fluent assertion DSL.
- Anything that drags `Microsoft.Testing.Platform.MSBuild` into the runtime closure.

## Samples

Two reference consumers sit under [`sample/`](sample/) and are kept in sync as cross-host features land:

- [`Circuids.Pulse.Blazor.WebAssembly.Sample`](sample/Circuids.Pulse.Blazor.WebAssembly.Sample/) — runs the suites inside a live WASM runtime; results render at `/conformance`.
- [`Circuids.Pulse.Maui.Sample`](sample/Circuids.Pulse.Maui.Sample/) — runs the suites inside the live MAUI host on Android, iOS, MacCatalyst, and Windows.

Both samples demonstrate the load-bearing pattern: an abstract conformance spec inherited by a thin per-host subclass that picks the runtime.

## Contributing

Issues, discussions, and pull requests are welcome on [github.com/Circuids/Pulse](https://github.com/Circuids/Pulse). Pulse keeps a deliberately small surface — one package, two runtime dependencies, sequential execution, cooperative cancellation, additive-only `TestRunReport` — so please open an issue before any change that touches the public API or adds a dependency.

If Pulse saves you time, you can support continued work on it through GitHub Sponsors:

[**Sponsor Circuids on GitHub →**](https://github.com/sponsors/Circuids)

## License

Circuids.Pulse is released under the [MIT License](LICENSE). © Circuids.
