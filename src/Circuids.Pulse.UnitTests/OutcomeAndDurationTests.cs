using Circuids.Pulse.UnitTests.TestSuites.OutcomeAndDuration;

namespace Circuids.Pulse.UnitTests;

/// <summary>
/// Validates outcome edge cases: ValueTask return type, sync void, exception unwrapping
/// (TargetInvocationException → real exception), duration capture, stack trace presence,
/// and the by-name xUnit SkipException recognition that smooths xUnit→Pulse migration.
/// </summary>
public sealed class OutcomeAndDurationTests
{
    private static async Task<TestRunReport> RunAsync(Action<PulseBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddPulse(configure);
        await using var sp = services.BuildServiceProvider();
        return await sp.GetRequiredService<ITestExecutor>().RunAsync();
    }

    [Fact]
    public async Task ValueTask_returning_test_is_awaited()
    {
        var report = await RunAsync(p => p.AddSuite<ValueTaskSuite>());

        Assert.Equal(2, report.Total);
        var passed = Assert.Single(report.Results, r => r.Outcome == TestOutcome.Passed);
        var failed = Assert.Single(report.Results, r => r.Outcome == TestOutcome.Failed);
        Assert.Equal(nameof(ValueTaskSuite.Vt_passes), passed.TestName);
        Assert.Equal("vt-fail", failed.Message);
    }

    [Fact]
    public async Task Sync_failure_unwraps_TargetInvocationException()
    {
        var report = await RunAsync(p => p.AddSuite<SyncFailureSuite>());

        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Failed, r.Outcome);
        // The user's exception message must be preserved, not the reflection wrapper's.
        Assert.Equal("real-message", r.Message);
        Assert.NotNull(r.StackTrace);
    }

    [Fact]
    public async Task Duration_is_captured_for_passing_test()
    {
        var report = await RunAsync(p => p.AddSuite<SlowSuite>());

        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Passed, r.Outcome);
        // 30ms sleep — be generous to absorb CI scheduling jitter.
        Assert.True(r.Duration > TimeSpan.Zero, $"Duration was {r.Duration}");
        Assert.True(report.Duration >= r.Duration, $"Report duration {report.Duration} was shorter than test duration {r.Duration}.");
    }

    [Fact]
    public async Task Report_duration_is_captured_for_full_run()
    {
        var report = await RunAsync(p => p.AddSuite<SlowSuite>());

        Assert.True(report.Duration > TimeSpan.Zero, $"Report duration was {report.Duration}.");
    }

    [Fact]
    public async Task Skipped_test_does_not_invoke_method_body()
    {
        DeclarativeSkipSuite.Counter = 0;
        var report = await RunAsync(p => p.AddSuite<DeclarativeSkipSuite>());

        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Skipped, r.Outcome);
        Assert.Equal(0, DeclarativeSkipSuite.Counter);
        // Skipped tests should report Duration.Zero (work was never performed).
        Assert.Equal(TimeSpan.Zero, r.Duration);
        Assert.True(report.Duration > TimeSpan.Zero, $"Report duration was {report.Duration}.");
    }

    [Fact]
    public async Task Mixed_outcome_run_records_correct_aggregates()
    {
        var report = await RunAsync(p =>
        {
            p.AddSuite<PassSuite>();
            p.AddSuite<FailSuite>();
            p.AddSuite<SkipSuite>();
            p.AddSuite<PassSuite>();
        });

        Assert.Equal(4, report.Total);
        Assert.Equal(2, report.Passed);
        Assert.Equal(1, report.Failed);
        Assert.Equal(1, report.Skipped);
        Assert.False(report.Success);
    }

    [Fact]
    public async Task RuntimeEnvironment_is_populated_on_every_report()
    {
        var report = await RunAsync(p => p.AddSuite<PassSuite>());

        Assert.NotNull(report.RuntimeEnvironment);
        Assert.False(string.IsNullOrWhiteSpace(report.RuntimeEnvironment.OSDescription));
        Assert.False(string.IsNullOrWhiteSpace(report.RuntimeEnvironment.FrameworkDescription));
    }

    [Fact]
    public async Task Timestamp_is_close_to_now_in_UTC()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-2);
        var report = await RunAsync(p => p.AddSuite<PassSuite>());
        var after = DateTimeOffset.UtcNow.AddSeconds(2);

        Assert.InRange(report.Timestamp, before, after);
        Assert.Equal(TimeSpan.Zero, report.Timestamp.Offset);
    }

    [Fact]
    public async Task Wrong_argument_type_in_PulseRow_surfaces_as_Failed()
    {
        // Discovery only validates parameter count, not type compatibility — type mismatches must
        // surface as a Failed result at invoke-time (not crash the run).
        var report = await RunAsync(p => p.AddSuite<TypeMismatchSuite>());

        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Failed, r.Outcome);
    }

}
