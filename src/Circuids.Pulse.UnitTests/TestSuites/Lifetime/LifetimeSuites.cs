namespace Circuids.Pulse.UnitTests.TestSuites.Lifetime;

internal sealed class LifecycleSuite : IPulseLifetime, IAsyncDisposable
{
    public static readonly List<string> Trace = [];

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        Trace.Add("init");
        return Task.CompletedTask;
    }

    public Task DisposeAsync(CancellationToken cancellationToken)
    {
        Trace.Add("dispose-async-lifetime");
        return Task.CompletedTask;
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        Trace.Add("dispose-async-disposable");
        return ValueTask.CompletedTask;
    }

    [PulseCase] public void A() => Trace.Add("test:a");
    [PulseCase] public void B() => Trace.Add("test:b");
}

internal sealed class InitFailingSuite : IPulseLifetime
{
    public static bool DisposeCalled;
    public static bool TestCalled;

    public Task InitializeAsync(CancellationToken cancellationToken)
        => throw new InvalidOperationException("init-boom");

    public Task DisposeAsync(CancellationToken cancellationToken)
    {
        DisposeCalled = true;
        return Task.CompletedTask;
    }

    [PulseCase] public void Dummy() => TestCalled = true;
}

internal sealed class FollowingSuite
{
    public static bool Called;

    [PulseCase] public void Ok() => Called = true;
}

internal sealed class DisposableOnlySuite : IDisposable
{
    public static int DisposeCount;

    public void Dispose() => DisposeCount++;

    [PulseCase] public void Trivial() { }
}
