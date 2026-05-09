namespace Circuids.Pulse.Maui.Sample.Conformance;

/// <summary>
/// Demonstrates the per-test cooperative timeout and per-suite lifetime hooks shipped in
/// Circuids.Pulse §9.2 / §9.3. Mirrors the Blazor sample's identically-named suite.
/// </summary>
public sealed class LifetimeAndTimeoutSuite : IPulseLifetime, IAsyncDisposable
{
    private SemaphoreSlim? _gate;

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        _gate = new SemaphoreSlim(initialCount: 1, maxCount: 1);
        return Task.CompletedTask;
    }

    public Task DisposeAsync(CancellationToken cancellationToken)
    {
        _gate?.Dispose();
        _gate = null;
        return Task.CompletedTask;
    }

    ValueTask IAsyncDisposable.DisposeAsync() => ValueTask.CompletedTask;

    [PulseCase]
    public void Initialize_provisioned_the_gate()
        => PulseAssert.NotNull(_gate, "InitializeAsync must run before the first test.");

    [PulseCase(TimeoutMs = 250)]
    public async Task Slow_work_honors_timeout(CancellationToken ct)
    {
        // The token is forwarded by Pulse; if we exceed TimeoutMs the linked CTS fires and the
        // test is reported as a timeout failure. This call completes well under the budget.
        await Task.Delay(50, ct);
        PulseAssert.True(_gate is not null, "Gate must still be available mid-test.");
    }
}
