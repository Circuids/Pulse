namespace Circuids.Pulse.UnitTests;

/// <summary>
/// IPulseLifetime hooks plus IDisposable / IAsyncDisposable suite tear-down.
/// </summary>
public sealed class LifetimeTests
{
    private static async Task<TestRunReport> RunAsync(Action<PulseBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddPulse(configure);
        await using var sp = services.BuildServiceProvider();
        return await sp.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Initialize_runs_before_first_test_and_Dispose_after_last()
    {
        LifecycleSuite.Trace.Clear();
        var report = await RunAsync(p => p.AddSuite<LifecycleSuite>());

        Assert.Equal(2, report.Total);
        Assert.Equal(
            new[] { "init", "test:a", "test:b", "dispose-async-lifetime", "dispose-async-disposable" },
            LifecycleSuite.Trace);
    }

    [Fact]
    public async Task Initialize_failure_is_reported_and_tests_still_dispose()
    {
        InitFailingSuite.DisposeCalled = false;
        var report = await RunAsync(p => p.AddSuite<InitFailingSuite>());

        // Init failure surfaces as a synthetic failed entry; suite tests are still discovered/run
        // (lifetime spec lets the suite report the failure but doesn't gate execution).
        var initFailure = Assert.Single(
            report.Results,
            r => r.TestName == "(suite InitializeAsync)");
        Assert.Equal(TestOutcome.Failed, initFailure.Outcome);
        Assert.Contains("init-boom", initFailure.Message ?? "");
        Assert.True(InitFailingSuite.DisposeCalled, "Dispose must run even after init failure.");
    }

    [Fact]
    public async Task IDisposable_only_suite_is_disposed()
    {
        DisposableOnlySuite.DisposeCount = 0;
        await RunAsync(p => p.AddSuite<DisposableOnlySuite>());

        Assert.Equal(1, DisposableOnlySuite.DisposeCount);
    }

    private sealed class LifecycleSuite : IPulseLifetime, IAsyncDisposable
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

    private sealed class InitFailingSuite : IPulseLifetime
    {
        public static bool DisposeCalled;

        public Task InitializeAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("init-boom");

        public Task DisposeAsync(CancellationToken cancellationToken)
        {
            DisposeCalled = true;
            return Task.CompletedTask;
        }

        [PulseCase] public void Dummy() { }
    }

    private sealed class DisposableOnlySuite : IDisposable
    {
        public static int DisposeCount;
        public void Dispose() => DisposeCount++;

        [PulseCase] public void Trivial() { }
    }
}
