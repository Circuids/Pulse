namespace Circuids.Pulse.UnitTests.TestSuites.Scope;

/// <summary>
/// Shared trace for per-suite scope tests. Entries are appended by fixture constructors,
/// test bodies, and disposal hooks so tests can assert exact lifetime ordering.
/// </summary>
public static class ScopeTrace
{
    public static readonly List<string> Entries = new();

    public static void Reset() => Entries.Clear();
}

/// <summary>Scoped disposable service: traces construction and disposal with a unique instance id.</summary>
public sealed class ScopedTracker : IAsyncDisposable
{
    public Guid Id { get; } = Guid.NewGuid();

    public ScopedTracker() => ScopeTrace.Entries.Add($"create:{Id}");

    public ValueTask DisposeAsync()
    {
        ScopeTrace.Entries.Add($"dispose:{Id}");
        return ValueTask.CompletedTask;
    }
}

/// <summary>Singleton disposable service: proves scopes neither duplicate nor dispose singletons.</summary>
public sealed class SingletonTracker : IAsyncDisposable
{
    public Guid Id { get; } = Guid.NewGuid();

    public SingletonTracker() => ScopeTrace.Entries.Add("singleton-create");

    public ValueTask DisposeAsync()
    {
        ScopeTrace.Entries.Add("singleton-dispose");
        return ValueTask.CompletedTask;
    }
}

public sealed class TrackerSuiteA
{
    private readonly ScopedTracker _tracker;

    public TrackerSuiteA(ScopedTracker tracker) => _tracker = tracker;

    [PulseCase]
    public void Records_instance() => ScopeTrace.Entries.Add($"a:{_tracker.Id}");
}

public sealed class TrackerSuiteB
{
    private readonly ScopedTracker _tracker;

    public TrackerSuiteB(ScopedTracker tracker) => _tracker = tracker;

    [PulseCase]
    public void Records_instance() => ScopeTrace.Entries.Add($"b:{_tracker.Id}");
}

public sealed class SingletonSuiteA
{
    private readonly SingletonTracker _tracker;

    public SingletonSuiteA(SingletonTracker tracker) => _tracker = tracker;

    [PulseCase]
    public void Records_instance() => ScopeTrace.Entries.Add($"sa:{_tracker.Id}");
}

public sealed class SingletonSuiteB
{
    private readonly SingletonTracker _tracker;

    public SingletonSuiteB(SingletonTracker tracker) => _tracker = tracker;

    [PulseCase]
    public void Records_instance() => ScopeTrace.Entries.Add($"sb:{_tracker.Id}");
}

/// <summary>Implements every teardown hook so the full disposal order is observable.</summary>
public sealed class TeardownOrderSuite : IPulseLifetime, IAsyncDisposable
{
    public TeardownOrderSuite(ScopedTracker tracker) =>
        ScopeTrace.Entries.Add($"ctor:{tracker.Id}");

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        ScopeTrace.Entries.Add("init");
        return Task.CompletedTask;
    }

    public Task DisposeAsync(CancellationToken cancellationToken)
    {
        ScopeTrace.Entries.Add("lifetime-dispose");
        return Task.CompletedTask;
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        ScopeTrace.Entries.Add("suite-dispose");
        return ValueTask.CompletedTask;
    }

    [PulseCase]
    public void Runs() => ScopeTrace.Entries.Add("test");
}

/// <summary>Scoped service whose disposal throws; the failure must land in the report.</summary>
public sealed class ThrowingScopedService : IAsyncDisposable
{
    public ValueTask DisposeAsync() => throw new InvalidOperationException("scope-dispose-boom");
}

public sealed class ThrowingScopeSuite
{
    public ThrowingScopeSuite(ThrowingScopedService service) { _ = service; }

    [PulseCase]
    public void Ok() { }
}

public sealed class AfterThrowingScopeSuite
{
    public static bool Called;

    [PulseCase]
    public void Ok() => Called = true;
}

/// <summary>
/// Suite that is itself a scoped DI service. Pulse's explicit teardown and the scope's
/// disposal tracking both dispose it; idempotent disposal keeps that safe.
/// </summary>
public sealed class ScopedSuiteService : IAsyncDisposable
{
    public int DisposeCount;

    [PulseCase]
    public void Ok() { }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }
}
