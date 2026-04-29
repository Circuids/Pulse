using Circuids.Pulse.UnitTests.TestSuites.Lifetime;

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
        InitFailingSuite.TestCalled = false;
        var report = await RunAsync(p => p.AddSuite<InitFailingSuite>());

        var initFailure = Assert.Single(
            report.Results,
            r => r.TestName == "(suite InitializeAsync)");
        Assert.Equal(TestOutcome.Failed, initFailure.Outcome);
        Assert.Contains("init-boom", initFailure.Message ?? "");

        var skipped = Assert.Single(report.Results, r => r.TestName == nameof(InitFailingSuite.Dummy));
        Assert.Equal(TestOutcome.Skipped, skipped.Outcome);
        Assert.Equal("Suite initialization failed: init-boom", skipped.Message);
        Assert.False(InitFailingSuite.TestCalled, "Tests must not run after suite initialization fails.");
        Assert.True(InitFailingSuite.DisposeCalled, "Dispose must run even after init failure.");
    }

    [Fact]
    public async Task Initialize_failure_does_not_stop_following_suites()
    {
        InitFailingSuite.DisposeCalled = false;
        InitFailingSuite.TestCalled = false;
        FollowingSuite.Called = false;

        var report = await RunAsync(p =>
        {
            p.AddSuite<InitFailingSuite>();
            p.AddSuite<FollowingSuite>();
        });

        Assert.Contains(report.Results, r =>
            r.SuiteName == typeof(InitFailingSuite).FullName
            && r.TestName == nameof(InitFailingSuite.Dummy)
            && r.Outcome == TestOutcome.Skipped);
        Assert.Contains(report.Results, r =>
            r.SuiteName == typeof(FollowingSuite).FullName
            && r.TestName == nameof(FollowingSuite.Ok)
            && r.Outcome == TestOutcome.Passed);
        Assert.True(FollowingSuite.Called);
    }

    [Fact]
    public async Task IDisposable_only_suite_is_disposed()
    {
        DisposableOnlySuite.DisposeCount = 0;
        await RunAsync(p => p.AddSuite<DisposableOnlySuite>());

        Assert.Equal(1, DisposableOnlySuite.DisposeCount);
    }

}
