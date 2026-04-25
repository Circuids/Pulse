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

    public sealed class PassingSuite
    {
        [PulseCase] public void Always_passes() { }
    }

    public sealed class FailingSuite
    {
        [PulseCase] public void Always_fails() => throw new InvalidOperationException("boom");
    }

    public sealed class DeclarativelySkippedSuite
    {
        public static bool WasInvoked;
        [PulseCase(Skip = "not yet")]
        public void Skipped_test() => WasInvoked = true;
    }

    public sealed class RuntimeSkippedSuite
    {
        [PulseCase] public void Skip_in_body() => throw new PulseSkipException("dynamic skip");
    }

    public sealed class AsyncSuite
    {
        [PulseCase]
        public async Task Async_fails()
        {
            await Task.Yield();
            throw new InvalidOperationException("async-fail");
        }
    }

    public sealed class MatrixSuite
    {
        [PulseMatrix]
        [PulseRow(390)]
        [PulseRow(768)]
        [PulseRow(1920)]
        public void Width_is_positive(int width) => Assert.True(width > 0);
    }

    public interface ICounter { int Value { get; } void Increment(); }
    public sealed class Counter : ICounter
    {
        public int Value { get; private set; }
        public void Increment() => Value++;
    }

    public sealed class DependencySuite
    {
        private readonly ICounter _counter;
        public DependencySuite(ICounter counter) { _counter = counter; }
        [PulseCase] public void Counts() { _counter.Increment(); Assert.Equal(1, _counter.Value); }
    }
}
