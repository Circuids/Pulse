# Authoring Runtime Conformance Specifications

This document is the engineering handbook for writing Runtime Conformance specifications. It covers how to structure specifications, how to wire adapters, what belongs in `*.TestSupport`, and — critically — what architectural mistakes to avoid.

Runtime Conformance is defined in the [README](../README.md). If you have not read it yet, start there. The README explains **what** Runtime Conformance is and **why** it exists. This document explains **how** to author specifications correctly.

---

## Two Ways To Use Pulse

Pulse supports two patterns. Choose based on whether the behavior needs to be proven across multiple runtimes.

| Pattern | When to use | Where it lives |
|---|---|---|
| **Shared specification + adapters** | The same behavioral contract must hold against fakes (`dotnet test`) AND the real runtime (Pulse). | Specification in `*.TestSupport`; adapters in `*.Tests` and `*.ConformanceHost`. |
| **Pulse-only suite** | The behavior has no useful fake equivalent — the validation demands the real host to be meaningful. | Directly in the host app or `*.ConformanceHost`. No shared spec needed. |

**You do not need a shared specification to use Pulse.** A Pulse-only suite is a perfectly valid starting point. If you later discover the same behavior needs to be proven in `dotnet test` as well, extract a shared specification then.

The rest of this document covers both patterns in detail. Pulse-only suites are covered in [their own section](#pulse-only-suites) at the end.


## Specification Litmus Test

> **This test applies when deciding whether to create a *shared specification*.** If you are writing a Pulse-only suite, skip to the [Pulse-Only Suites](#pulse-only-suites) section.

Before writing a shared Runtime Specification, verify it passes this test:

| Question | Expected answer |
|---|---|
| Does this validate **observable behavior**? | Yes |
| Does it describe a **reusable contract**? | Yes |
| Can **multiple implementations** execute it? | Yes |
| Can the implementation change while the specification stays unchanged? | Yes |
| Does the specification avoid **host-specific assumptions**? | Yes |
| Would a unit test already prove this behavior? | **No** |

If three or more answers are wrong, this probably should not become a shared Runtime Specification. Consider a Pulse-only suite instead, or keep it as a unit test.

A Runtime Specification must describe **what** correct behavior looks like — not **how** a particular implementation achieves it. If the spec would need to change when you swap the underlying implementation, the spec is wrong.


## The Separation

Every Runtime Conformance test has two parts. They are never in the same project.

```text
Specification (TestSupport)          Adapter (Tests or ConformanceHost)
════════════════════════════         ═══════════════════════════════════
Defines WHAT behavior to prove       Defines HOW to exercise it
No runner dependency                 Owns test-framework attributes
No DI, no IServiceProvider           Wires real or fake implementations
Throws ordinary BCL exceptions       Owns assertions (PulseAssert / xUnit)
Immutable across platforms           Platform-specific; one per host
```

Specifications define behavior. Adapters choose the runtime.


## How To Write A Specification

### What a specification contains

A specification is an `abstract class` in a `*.TestSupport` project. It exposes:

- An **abstract factory** — the single seam the adapter fills in (`protected abstract IFoo CreateFoo()`)
- **Protected `*_core` methods** — one per behavioral guarantee, each exercising the factory and throwing an ordinary exception on failure
- A **`CancellationToken` parameter** — trailing, defaulted, on every async `*_core` method
- Optional **shared helpers** — cleanup, key generation, shared data builders

```csharp
// MyProduct.TestSupport / KeyValueStoreSpec.cs
public abstract class KeyValueStoreSpec
{
    protected virtual string KeyPrefix => $"conformance.{GetType().Name}";

    protected abstract IConformanceKeyValueStore CreateStore();

    protected async Task Set_then_get_round_trips_value_core(CancellationToken ct = default)
    {
        var store = CreateStore();
        var key = $"{KeyPrefix}.roundtrip";

        try
        {
            await store.SetAsync(key, "alpha", ct);
            var actual = await store.GetAsync(key, ct);

            if (actual != "alpha")
                throw new InvalidOperationException(
                    $"A stored value must round-trip unchanged. Expected 'alpha', got '{actual}'.");
        }
        finally
        {
            await store.RemoveAsync(key, CancellationToken.None);
        }
    }

    // More behavioral guarantees follow the same pattern...
}
```

### What a specification must never contain

> **Specifications must stay pure.** Every violation of this list erodes the architectural boundary.

- ❌ References to `Circuids.Pulse`
- ❌ References to xUnit, NUnit, MSTest, Shouldly, or FluentAssertions
- ❌ `[PulseCase]`, `[PulseMatrix]`, `[PulseRow]`, `[Fact]`, or `[Theory]` attributes
- ❌ `IServiceProvider`, DI containers, or service resolution
- ❌ Host framework references (Blazor, MAUI, WPF, WinForms)
- ❌ Platform detection (`OperatingSystem.IsBrowser()`, `RuntimeInformation`)
- ❌ Conditional execution based on runtime environment
- ❌ Mocks, stubs, or fakes of the abstraction being validated
- ❌ `PulseAssert`, `Debug.Assert`, or any assertion library
- ❌ `[Fact]` or `[PulseCase]` attributes — those belong in adapters only

### Why this matters

A specification that references Pulse cannot be executed by `dotnet test`. A specification that uses DI cannot be reused without a container. A specification that detects the platform cannot remain stable across platforms. Every impurity removes a reuse path.

The specification's only job is to state: **when this abstraction is used correctly, it must behave in the following way.** Nothing else.


## How To Write Adapters

### The unit test adapter (`dotnet test`)

The unit test adapter lives in a `*.Tests` project. It:

- Inherits the abstract specification class
- Provides a **fake** or **in-memory** implementation of the abstraction
- Tags each `*_core` method with `[Fact]` or `[Theory]`
- Forwards the test framework's `CancellationToken`
- May use xUnit assertions directly in the adapter body when needed

```csharp
// MyProduct.Tests / InMemoryKeyValueStoreTests.cs
public sealed class InMemoryKeyValueStoreTests : KeyValueStoreSpec
{
    protected override IConformanceKeyValueStore CreateStore() => new InMemoryKeyValueStore();

    [Fact]
    public Task Set_then_get_round_trips_value()
        => Set_then_get_round_trips_value_core(TestContext.Current.CancellationToken);
}
```

The unit test adapter owns: the test framework reference, the fake implementation, and the `[Fact]` attribute.

### The Pulse adapter (real runtime)

The Pulse adapter lives in the host application or a dedicated `*.ConformanceHost`. It:

- Inherits the same abstract specification class
- Provides the **real platform implementation** of the abstraction
- Tags each `*_core` method with `[PulseCase]` (or `[PulseMatrix]`)
- Sets appropriate `TimeoutMs` values for real-world I/O
- Implements `IPulseLifetime` when suite-wide setup or teardown is needed
- Uses `PulseAssert` for any additional assertions in adapter-only methods
- Cleans up real state (storage, files, preferences) in `try/finally` or `IPulseLifetime`

```csharp
// MyProduct.ConformanceHost / BrowserStorageBoundarySuite.cs
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
}
```

### The adapter contract

| Responsibility | Specification | Unit Test Adapter | Pulse Adapter |
|---|---|---|---|
| Define behavior | ✅ | — | — |
| Provide implementation | — | Fake / in-memory | Real platform |
| Own test attributes | — | `[Fact]` / `[Theory]` | `[PulseCase]` / `[PulseMatrix]` |
| Own assertions | Ordinary exceptions | xUnit assertions (optional) | `PulseAssert` |
| Set timeouts | — | Test framework default | Explicit `TimeoutMs` |
| Manage lifecycle | — | Constructor / Dispose | `IPulseLifetime` |
| Clean up real state | Shared helpers | N/A (fakes are ephemeral) | `try/finally` + `IPulseLifetime` |


## The One Conformance Rule

> **The target being validated must be real in the Pulse run.**

If the specification validates `IConformanceKeyValueStore`, the Pulse adapter must provide the real browser, device, desktop, or platform-backed implementation. A fake passed through a Pulse adapter makes the run meaningless.

Non-target dependencies may be fake. Faking logging, time, telemetry, configuration, or unrelated network calls is acceptable when it keeps the conformance target focused. **The conformance target is the boundary.**


## Project Layout

```text
MyProduct.TestSupport/          # Owned by consumer — zero runner dependencies
  Storage/
    KeyValueStoreSpec.cs        # Abstract specification (behavior only)
    IConformanceKeyValueStore.cs # Product abstraction
    InMemoryKeyValueStore.cs    # Fake implementation (used by Tests)
    FileSystemKeyValueStore.cs  # Fake implementation (used by Tests)
  Builders/                     # Shared test data builders
  Fixtures/                     # Shared setup helpers

MyProduct.Tests/                # dotnet test project
  InMemoryKeyValueStoreTests.cs # Adapter over InMemoryKeyValueStore

MyProduct.ConformanceHost/      # Pulse application host
  BrowserStorageBoundarySuite.cs  # Adapter over real browser localStorage
  BrowserLocalStorageStore.cs     # Real platform implementation
  MauiPreferencesBoundarySuite.cs # Adapter over real MAUI Preferences
  MauiPreferencesStore.cs         # Real platform implementation
```

### What belongs in TestSupport

- Shared abstract specifications
- Product abstractions (`IConformanceKeyValueStore`, `ITokenStorage`)
- Shared fake implementations (in-memory, file-system)
- Shared builders and sample data
- Shared fixture helpers
- Shared assertion helpers (plain BCL-based, not library-specific)

### What does NOT belong in TestSupport

- Pulse references
- Test framework references (xUnit, NUnit, MSTest)
- Assertion library references
- Host framework references (Blazor, MAUI, WPF, WinForms)
- DI references
- Platform-specific implementations (those belong in the conformance host)


## Anti-Patterns

These patterns violate the architecture. Each one erodes the separation between specification and adapter, making the specification less reusable and the boundary harder to maintain.

### ❌ Injecting `IServiceProvider` into a specification

```csharp
// WRONG — the specification now depends on DI
public abstract class BrokenSpec(IServiceProvider sp) { ... }
```

The specification must never know how implementations are created. That is the adapter's job. Use an abstract factory instead: `protected abstract IFoo CreateFoo()`.

### ❌ Resolving dependencies inside a specification

```csharp
// WRONG — the specification reaches into a container
protected IFoo CreateFoo() => _serviceProvider.GetRequiredService<IFoo>();
```

Container resolution belongs in the adapter constructor. The specification only calls the abstract factory.

### ❌ Mocking the abstraction under test

```csharp
// WRONG — the specification validates a mock, not the real implementation
var mock = new Mock<ITokenStorage>();
mock.Setup(...).Returns(...);
```

If the abstraction is mocked, the specification is not validating anything real. The conformance target must be real.

### ❌ Referencing Pulse APIs in a shared specification

```csharp
// WRONG — the specification now depends on Pulse and cannot run in dotnet test
PulseAssert.Equal(expected, actual);
```

Use ordinary BCL exceptions in specifications. Save `PulseAssert` for Pulse adapter bodies.

### ❌ Depending on platform APIs

```csharp
// WRONG — the specification assumes a specific runtime
if (OperatingSystem.IsBrowser()) { ... }
```

Platform-conditional logic breaks the specification's reusability. If a platform needs different behavior, that difference belongs in the adapter, not the specification.

### ❌ Duplicating specifications across platforms

```text
// WRONG — three copies of the same behavioral rules
BrowserStorageSpec.cs
WindowsStorageSpec.cs
MauiStorageSpec.cs
```

One specification, multiple adapters. If the specification needs to differ by platform, either the abstraction is wrong or the specification is testing implementation details.

### ❌ Testing implementation details

```csharp
// WRONG — the specification asserts internal state
Assert.AreEqual(42, store._internalCounter);
```

Specifications validate observable behavior at the boundary. Internal counters, private fields, and implementation choices are not observable behavior.

### ❌ Platform-specific adapters that rewrite the specification

```csharp
// WRONG — the adapter reimplements behavior instead of delegating
public Task Set_then_get_round_trips_value(CancellationToken ct)
{
    // Custom logic that doesn't call Set_then_get_round_trips_value_core
}
```

Adapters delegate to `*_core` methods. If a platform genuinely needs different behavior, the specification itself should cover that behavior — not the adapter.


## Pulse-Only Suites

Not every Pulse test needs a matching shared specification. Some behaviors have no meaningful fake equivalent. When the validation itself demands the real host, write a Pulse-only suite directly in the conformance host:

```csharp
// MyProduct.ConformanceHost / HttpHostBoundarySuite.cs
public sealed class HttpHostBoundarySuite
{
    private readonly HttpClient _http;

    public HttpHostBoundarySuite(HttpClient http)
    {
        _http = http;
    }

    [PulseCase]
    public void HttpClient_base_address_is_absolute()
    {
        PulseAssert.NotNull(_http.BaseAddress,
            "The host must configure HttpClient.BaseAddress.");
        PulseAssert.True(_http.BaseAddress!.IsAbsoluteUri,
            "Relative requests must resolve in the real app.");
    }

    [PulseCase(TimeoutMs = 5000)]
    public async Task App_root_returns_success(CancellationToken ct)
    {
        using var response = await _http.GetAsync("", ct);
        PulseAssert.True(response.IsSuccessStatusCode,
            $"GET / must succeed, got {(int)response.StatusCode}.");
    }
}
```

Pulse-only suites are appropriate for:

- Host DI graph validation
- `HttpClient` base address verification
- `IJSRuntime` module import
- MAUI `DeviceInfo` / `AppInfo`
- WinForms `Application.MessageLoop`
- WPF `Application.Current`
- Runtime environment facts
- Behaviors where a fake has no architectural value

Use `PulseAssert` in these suites — they live entirely inside the Pulse host and have no reuse requirement.


## Matrices

`[PulseMatrix]` with `[PulseRow]` produces one `TestResult` per row. Each row must represent a host-relevant input or a host call.

### Good matrix rows

These exercise the real runtime across variations that matter:

- JS interop calls with different browser inputs
- Dispatcher calls with different argument shapes
- Storage round-trips with different payload sizes
- Platform capability checks across different configuration values
- Device or runtime fact variations

```csharp
[PulseMatrix(DisplayName = "localStorage batch round-trip", TimeoutMs = 3000)]
[PulseRow(1)]
[PulseRow(5)]
[PulseRow(20)]
public async Task Batch_round_trips_under_budget(int count, CancellationToken ct)
{
    // Each row exercises the real localStorage with a different batch size
}
```

### Bad matrix rows

These should be unit tests, not Runtime Conformance:

- Pure arithmetic (`[PulseRow(1, 2, 3)]` adding two numbers)
- String classification (`[PulseRow("hello", "lowercase")]`)
- Hard-coded label mapping
- Logic that never touches a host boundary

If removing the row would make no difference to a `dotnet test` run of the same logic, it should not be a matrix.


## State And Cleanup

Pulse runs inside a real application. Tests may mutate real state. **Always clean up.**

### What needs cleanup

- Browser storage (`localStorage`, `sessionStorage`, `IndexedDB`)
- MAUI preferences and secure storage
- Application settings
- File system state
- UI state
- Singleton service state
- Device or platform state

### How to clean up

**Suite-wide cleanup** — use `IPulseLifetime`:

```csharp
public sealed class BrowserStorageBoundarySuite : KeyValueStoreSpec, IPulseLifetime
{
    public Task InitializeAsync(CancellationToken ct) => ClearKnownKeysAsync(ct);
    public Task DisposeAsync(CancellationToken ct) => ClearKnownKeysAsync(ct);
}
```

**Per-test cleanup** — use `try/finally` inside the `*_core` method:

```csharp
protected async Task Set_then_get_round_trips_value_core(CancellationToken ct = default)
{
    var store = CreateStore();
    var key = $"{KeyPrefix}.roundtrip";

    try
    {
        await store.SetAsync(key, "alpha", ct);
        // ... assertions ...
    }
    finally
    {
        await store.RemoveAsync(key, CancellationToken.None);
    }
}
```

Prefer suite-wide cleanup via `IPulseLifetime` for shared keys used across tests, and per-test cleanup for values that only exist within a single test.


## Naming Conventions

### Specifications

Name the specification for **what** it validates, not **where** it runs:

- `KeyValueStoreSpec` ✅
- `BrowserLocalStorageSpec` ❌ (platform-specific; the spec is reusable)

### Pulse adapters

Name the adapter for the **runtime that owns the boundary**:

- `BrowserStorageBoundarySuite` ✅
- `MauiPreferencesBoundarySuite` ✅
- `WinFormsUiThreadBoundarySuite` ✅
- `WpfDispatcherBoundarySuite` ✅
- `WpfFileStorageBoundarySuite` ✅

### Unit test adapters

Name the unit test adapter for the **fake implementation**:

- `InMemoryKeyValueStoreTests` ✅
- `FileSystemKeyValueStoreTests` ✅

Avoid names that describe only a mechanism (`AsyncSuite`, `TimeoutSuite`) unless the mechanism itself is the boundary being validated.


## Specification Evolution

Runtime Specifications should evolve as the system matures. The direction is: **more behavioral guarantees over time, without becoming platform-specific.**

### When to add a new behavioral guarantee

- A new implementation exposes a previously untested edge case
- A bug reveals missing behavioral coverage in the specification
- A new platform shows that an existing guarantee is underspecified

### How to evolve safely

1. Add the new `*_core` method to the abstract specification
2. Add corresponding test methods to **every** adapter — unit test and Pulse
3. Verify the new guarantee passes against all existing implementations

The specification should become stronger with each addition, but never narrower. A specification that only works on one platform is not a specification — it is a platform test.


## Quick Checklist

Before submitting a Runtime Conformance change, verify:

- [ ] The specification validates **observable behavior**, not implementation details
- [ ] The specification contains **zero references** to Pulse, xUnit, NUnit, MSTest, or assertion libraries
- [ ] The specification contains **zero references** to DI, `IServiceProvider`, or host frameworks
- [ ] The specification uses ordinary **BCL exceptions** for failure
- [ ] The **conformance target is real** in the Pulse adapter
- [ ] A matching **unit test adapter** exists for every specification method
- [ ] Cleanup is implemented for **every test that mutates real state**
- [ ] Async `*_core` methods accept a trailing `CancellationToken`
- [ ] `TimeoutMs` is set on Pulse adapter methods that perform I/O
- [ ] Matrix rows exercise **host-relevant behavior**, not padded logic
- [ ] Naming follows the conventions described above
