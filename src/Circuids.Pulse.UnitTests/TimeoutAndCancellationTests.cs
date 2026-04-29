using Circuids.Pulse.UnitTests.TestSuites.TimeoutAndCancellation;

namespace Circuids.Pulse.UnitTests;

/// <summary>
/// per-test cooperative timeouts and CancellationToken parameter injection on
/// <c>[PulseCase]</c> / <c>[PulseMatrix]</c> methods.
/// </summary>
public sealed class TimeoutAndCancellationTests
{
    private static async Task<TestRunReport> RunAsync(Action<PulseBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddPulse(configure);
        await using var sp = services.BuildServiceProvider();
        return await sp.GetRequiredService<ITestExecutor>().RunAsync();
    }

    [Fact]
    public async Task PulseCase_with_CancellationToken_parameter_is_invoked_with_a_token()
    {
        TokenAcceptingSuite.Received = default;
        var report = await RunAsync(p => p.AddSuite<TokenAcceptingSuite>());

        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Passed, r.Outcome);
        Assert.True(TokenAcceptingSuite.Received.CanBeCanceled);
    }

    [Fact]
    public async Task PulseCase_TimeoutMs_attribute_fires_and_reports_timeout_failure()
    {
        var report = await RunAsync(p => p.AddSuite<AttributeTimeoutSuite>());

        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Failed, r.Outcome);
        Assert.Contains("exceeded timeout", r.Message ?? "");
    }

    [Fact]
    public async Task DefaultTestTimeout_on_builder_is_inherited_by_tests_without_TimeoutMs()
    {
        var report = await RunAsync(p =>
        {
            p.DefaultTestTimeout = TimeSpan.FromMilliseconds(50);
            p.AddSuite<InheritedTimeoutSuite>();
        });

        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Failed, r.Outcome);
        Assert.Contains("exceeded timeout", r.Message ?? "");
    }

    [Fact]
    public async Task PulseMatrix_with_trailing_CancellationToken_runs_each_row()
    {
        var report = await RunAsync(p => p.AddSuite<MatrixWithTokenSuite>());

        Assert.Equal(2, report.Total);
        Assert.All(report.Results, r => Assert.Equal(TestOutcome.Passed, r.Outcome));
    }

    [Fact]
    public async Task Test_without_CancellationToken_parameter_ignores_TimeoutMs()
    {
        // Timeout is cooperative; if the body doesn't accept a token, the clock can't bite.
        var report = await RunAsync(p => p.AddSuite<UncooperativeSuite>());

        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Passed, r.Outcome);
    }

}
