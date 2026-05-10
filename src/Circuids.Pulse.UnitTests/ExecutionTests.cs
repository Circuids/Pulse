using Circuids.Pulse.UnitTests.TestSuites.Execution;

namespace Circuids.Pulse.UnitTests;

public sealed class ExecutionTests
{
    private static async Task<TestRunReport> RunAsync(Action<PulseBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddPulse(configure);
        await using var sp = services.BuildServiceProvider();
        return await sp.GetRequiredService<ITestExecutor>().RunAsync();
    }

    [Fact]
    public async Task Passing_case_is_reported_as_passed()
    {
        var report = await RunAsync(p => p.AddSuite<PassingSuite>());

        Assert.Equal(1, report.Total);
        Assert.True(report.Success);
        Assert.Equal(TestOutcome.Passed, report.Results[0].Outcome);
        Assert.Equal(nameof(PassingSuite.Always_passes), report.Results[0].TestName);
    }

    [Fact]
    public async Task Failing_case_is_reported_as_failed_with_message_and_stack()
    {
        var report = await RunAsync(p => p.AddSuite<FailingSuite>());

        Assert.False(report.Success);
        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Failed, r.Outcome);
        Assert.Equal("boom", r.Message);
        Assert.NotNull(r.StackTrace);
    }

    [Fact]
    public async Task Skip_attribute_skips_without_invoking()
    {
        var report = await RunAsync(p => p.AddSuite<DeclarativelySkippedSuite>());

        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Skipped, r.Outcome);
        Assert.Equal("not yet", r.Message);
        Assert.False(DeclarativelySkippedSuite.WasInvoked);
    }

    [Fact]
    public async Task PulseSkipException_skips_at_runtime()
    {
        var report = await RunAsync(p => p.AddSuite<RuntimeSkippedSuite>());
        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Skipped, r.Outcome);
        Assert.Equal("dynamic skip", r.Message);
    }

    [Fact]
    public async Task Async_case_awaits_returned_task()
    {
        var report = await RunAsync(p => p.AddSuite<AsyncSuite>());
        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Failed, r.Outcome);
        Assert.Equal("async-fail", r.Message);
    }

    [Fact]
    public async Task Matrix_expands_one_result_per_row()
    {
        var report = await RunAsync(p => p.AddSuite<MatrixSuite>());

        Assert.Equal(3, report.Total);
        Assert.All(report.Results, r => Assert.Equal(TestOutcome.Passed, r.Outcome));
        Assert.Contains(report.Results, r => r.TestName.Contains("390"));
        Assert.Contains(report.Results, r => r.TestName.Contains("768"));
        Assert.Contains(report.Results, r => r.TestName.Contains("1920"));
    }

    [Fact]
    public async Task Cancellation_between_tests_throws_OperationCanceled()
    {
        var services = new ServiceCollection();
        services.AddPulse(p => p.AddSuite<PassingSuite>().AddSuite<FailingSuite>());
        await using var sp = services.BuildServiceProvider();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sp.GetRequiredService<ITestExecutor>().RunAsync(cts.Token));
    }

    [Fact]
    public async Task RunAsync_with_suite_filter_runs_only_matching_suite()
    {
        var services = new ServiceCollection();
        services.AddPulse(p =>
        {
            p.AddSuite<PassingSuite>();
            p.AddSuite<FailingSuite>();
        });
        await using var sp = services.BuildServiceProvider();

        var report = await sp.GetRequiredService<ITestExecutor>()
            .RunAsync(typeof(PassingSuite).FullName!, TestContext.Current.CancellationToken);

        var r = Assert.Single(report.Results);
        Assert.Equal(typeof(PassingSuite).FullName, r.SuiteName);
    }

    [Fact]
    public async Task AssignedPlatform_defaults_to_unassigned_when_not_set()
    {
        var report = await RunAsync(p => p.AddSuite<PassingSuite>());
        Assert.Equal("(unassigned)", report.AssignedPlatform);
    }

    [Fact]
    public async Task AssignedPlatform_is_passed_through_verbatim()
    {
        var report = await RunAsync(p =>
        {
            p.AssignedPlatform = "Test-Platform-X";
            p.AddSuite<PassingSuite>();
        });
        Assert.Equal("Test-Platform-X", report.AssignedPlatform);
    }

    [Fact]
    public async Task Suite_with_DI_dependency_is_resolved_via_ActivatorUtilities()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICounter, Counter>();
        services.AddPulse(p => p.AddSuite<DependencySuite>());
        await using var sp = services.BuildServiceProvider();

        var report = await sp.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);
        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Passed, r.Outcome);
    }

    [Fact]
    public async Task Suite_with_explicit_factory_uses_factory()
    {
        var counter = new Counter();
        var services = new ServiceCollection();
        services.AddPulse(p => p.AddSuite(_ => new DependencySuite(counter)));
        await using var sp = services.BuildServiceProvider();

        await sp.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, counter.Value);
    }

    [Fact]
    public async Task Concurrent_runs_on_same_executor_are_rejected()
    {
        LongRunningSuite.Reset();
        var services = new ServiceCollection();
        services.AddPulse(p => p.AddSuite<LongRunningSuite>());
        await using var sp = services.BuildServiceProvider();
        var executor = sp.GetRequiredService<ITestExecutor>();

        var firstRun = executor.RunAsync(TestContext.Current.CancellationToken);
        await LongRunningSuite.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.RunAsync(TestContext.Current.CancellationToken));

        Assert.Contains("already running", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("shared application runtime", ex.Message, StringComparison.OrdinalIgnoreCase);

        LongRunningSuite.Release();
        var report = await firstRun;
        Assert.True(report.Success);
    }

    [Fact]
    public async Task Concurrent_runs_from_different_scopes_are_rejected()
    {
        LongRunningSuite.Reset();
        var services = new ServiceCollection();
        services.AddPulse(p => p.AddSuite<LongRunningSuite>());
        await using var sp = services.BuildServiceProvider();
        await using var firstScope = sp.CreateAsyncScope();
        await using var secondScope = sp.CreateAsyncScope();

        var firstExecutor = firstScope.ServiceProvider.GetRequiredService<ITestExecutor>();
        var secondExecutor = secondScope.ServiceProvider.GetRequiredService<ITestExecutor>();

        var firstRun = firstExecutor.RunAsync(TestContext.Current.CancellationToken);
        await LongRunningSuite.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => secondExecutor.RunAsync(TestContext.Current.CancellationToken));

        LongRunningSuite.Release();
        var report = await firstRun;
        Assert.True(report.Success);
    }

    [Fact]
    public async Task Run_can_start_again_after_successful_run()
    {
        var services = new ServiceCollection();
        services.AddPulse(p => p.AddSuite<PassingSuite>());
        await using var sp = services.BuildServiceProvider();
        var executor = sp.GetRequiredService<ITestExecutor>();

        var first = await executor.RunAsync(TestContext.Current.CancellationToken);
        var second = await executor.RunAsync(TestContext.Current.CancellationToken);

        Assert.True(first.Success);
        Assert.True(second.Success);
    }

    [Fact]
    public async Task Run_can_start_again_after_failed_results()
    {
        var services = new ServiceCollection();
        services.AddPulse(p => p.AddSuite<FailingSuite>());
        await using var sp = services.BuildServiceProvider();
        var executor = sp.GetRequiredService<ITestExecutor>();

        var first = await executor.RunAsync(TestContext.Current.CancellationToken);
        var second = await executor.RunAsync(TestContext.Current.CancellationToken);

        Assert.False(first.Success);
        Assert.False(second.Success);
    }

    [Fact]
    public async Task Run_can_start_again_after_suite_construction_failure()
    {
        var services = new ServiceCollection();
        services.AddPulse(p => p.AddSuite<ConstructionFailingSuite>());
        await using var sp = services.BuildServiceProvider();
        var executor = sp.GetRequiredService<ITestExecutor>();

        var first = await executor.RunAsync(TestContext.Current.CancellationToken);
        var second = await executor.RunAsync(TestContext.Current.CancellationToken);

        Assert.Contains(first.Results, r => r.TestName == "(suite construction)" && r.Outcome == TestOutcome.Failed);
        Assert.Contains(second.Results, r => r.TestName == "(suite construction)" && r.Outcome == TestOutcome.Failed);
    }

    [Fact]
    public async Task Run_can_start_again_after_cancelled_running_test()
    {
        CancellableLongRunningSuite.Reset();
        var services = new ServiceCollection();
        services.AddPulse(p =>
        {
            p.AddSuite<PassingSuite>();
            p.AddSuite<CancellableLongRunningSuite>();
        });
        await using var sp = services.BuildServiceProvider();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var executor = sp.GetRequiredService<ITestExecutor>();

        var firstRun = executor.RunAsync(cts.Token);
        await CancellableLongRunningSuite.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        var first = await firstRun;
        var second = await executor.RunAsync(typeof(PassingSuite).FullName!, TestContext.Current.CancellationToken);

        Assert.False(first.Success);
        Assert.True(second.Success);
    }

}
