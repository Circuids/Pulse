# Conformance Specs And Rules

Pulse conformance tests prove behavior inside the real app runtime. Use this guide when deciding what belongs in a shared spec, what belongs in a Pulse suite, and how to structure `*.TestSupport`.

## Core Idea

Use `dotnet test` to prove behavior against fakes. Use Pulse to prove the same boundary against the real app host.

A good Pulse test answers a question a normal test host cannot answer honestly:

- does browser `localStorage` behave through the real `IJSRuntime`?
- does MAUI `Preferences.Default` round-trip on this device?
- does `MainThread` dispatch onto the real UI thread?
- does the WPF dispatcher or WinForms message loop exist and marshal work correctly?
- did the app configure `HttpClient`, DI, runtime services, or platform services correctly?

## Recommended Project Shape

```text
MyProduct.TestSupport/       # shared specs, fakes, builders, sample data
MyProduct.Tests/             # ordinary dotnet test adapter over fakes
MyProduct.ConformanceHost/   # real app host adapter over real platform services
```

`*.TestSupport` is optional, but it is the recommended place for reusable behavior specs. It is owned by the consumer, not Pulse.

## TestSupport Rules

A `*.TestSupport` project should stay portable.

Do:

- keep specs as plain C#;
- reference only product abstractions and the BCL;
- use ordinary exceptions for failed invariants;
- include reusable fakes, builders, sample data, and shared assumptions;
- expose minimal abstract factories or protected properties for the target under test.

Do not:

- reference `Circuids.Pulse`;
- reference xUnit, NUnit, MSTest, Shouldly, or FluentAssertions;
- use `[PulseCase]`, `[PulseMatrix]`, `[PulseRow]`, `[Fact]`, or `[Theory]`;
- depend on DI, `IServiceProvider`, app startup, or host frameworks;
- use `PulseAssert` or `Debug.Assert`;
- mock the abstraction the spec claims to validate.

## Canonical Spec Shape

Prefer an abstract implementation spec when you want one behavior body shared by both runtimes.

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

Why this shape works:

- the behavior is written once;
- the test project chooses fakes;
- the Pulse host chooses the real platform implementation;
- both runtimes prove the same rule.

## dotnet test Adapter

```csharp
public sealed class InMemoryTokenStorageTests : TokenStorageSpec
{
    protected override ITokenStorage CreateStorage() => new InMemoryTokenStorage();

    [Fact]
    public Task Store_then_retrieve_returns_same_value()
        => Store_then_retrieve_returns_same_value_core(TestContext.Current.CancellationToken);
}
```

The test adapter owns test-framework attributes and test-framework assertions when it needs them.

## Pulse Adapter

```csharp
public sealed class BrowserTokenStorageSuite : TokenStorageSpec
{
    private readonly ITokenStorage _realStorage;

    public BrowserTokenStorageSuite(ITokenStorage realStorage)
    {
        _realStorage = realStorage;
    }

    protected override ITokenStorage CreateStorage() => _realStorage;

    [PulseCase(TimeoutMs = 5000)]
    public Task Store_then_retrieve_returns_same_value(CancellationToken ct)
        => Store_then_retrieve_returns_same_value_core(ct);
}
```

The Pulse adapter owns Pulse attributes, timeout budgets, host services, cleanup, and `PulseAssert` usage.

## The One Conformance Rule

The target being validated must be real in the Pulse run.

If the spec validates `ITokenStorage`, then the Pulse adapter must use the real browser, device, desktop, or platform-backed implementation of `ITokenStorage`.

Other dependencies may be fake when they are not the boundary under test. Faking logging, time, telemetry, configuration, or unrelated network calls is fine when it keeps the conformance target focused.

## Pulse-Only Suites

Not every Pulse suite needs a matching shared spec. Use Pulse-only suites for behavior that has no useful fake equivalent:

- host `HttpClient` base address;
- `IJSRuntime` module import;
- MAUI `DeviceInfo` and `AppInfo`;
- WinForms `Application.MessageLoop`;
- WPF `Application.Current`;
- runtime environment facts;
- DI graph validation.

Use `PulseAssert` in these suites.

## Matrix Rules

`[PulseMatrix]` is useful when each row still represents a host-relevant input or host call.

Good matrix rows:

- JS module calls with different browser inputs;
- dispatcher calls with different values;
- storage round-trips with different payload shapes;
- platform capability checks;
- device or runtime facts.

Avoid matrices that are only arithmetic, string classification, or hard-coded label mapping. Those are unit tests.

## State And Cleanup

Pulse runs inside a real app, so tests may touch real state.

Use cleanup whenever a test mutates:

- browser storage;
- MAUI preferences or secure storage;
- app settings;
- files;
- UI state;
- singleton services;
- device or platform state.

Prefer `IPulseLifetime` for suite-wide cleanup and local `try/finally` for per-test cleanup.

## Naming Guidance

Use names that say what runtime owns the boundary.

Good names:

- `BrowserStorageBoundarySuite`
- `BrowserInteropBoundarySuite`
- `MauiPreferencesBoundarySuite`
- `MauiDispatcherBoundarySuite`
- `WinFormsUiThreadBoundarySuite`
- `WpfDispatcherBoundarySuite`

Avoid names that describe only the mechanism, such as `AsyncSuite` or `TimeoutSuite`, unless the mechanism itself is the boundary being validated.

## Quick Checklist

Before adding a Pulse conformance test, ask:

- Does this require the real app host to be meaningful?
- Is the conformance target real in the Pulse adapter?
- Could this be a normal unit test instead?
- Does the shared spec avoid Pulse and test-framework dependencies?
- Does the test clean up real state it mutates?
- Does the test accept a trailing `CancellationToken` when it can block or cross a host boundary?
- Does a matrix row exercise host-relevant behavior instead of padded logic?

If most answers are yes, the test probably belongs in Pulse.
