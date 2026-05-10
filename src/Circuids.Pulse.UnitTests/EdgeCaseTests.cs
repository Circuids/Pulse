using Circuids.Pulse.UnitTests.TestSuites.EdgeCases;

namespace Circuids.Pulse.UnitTests;

/// <summary>
/// Edge-case behavior tests covering contracts that aren't fully exercised by other suites:
/// mid-run cancellation, filter case-sensitivity, fluent-builder identity, row order across
/// multiple matrix methods, synchronously-completing ValueTask, scope isolation between runs,
/// the null-ness of Message/StackTrace on passed results, and the silent-ignore behavior of
/// [PulseRow] when paired with [PulseCase].
/// </summary>
public sealed class EdgeCaseTests
{
    private static async Task<TestRunReport> RunAsync(Action<PulseBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddPulse(configure);
        await using var sp = services.BuildServiceProvider();
        return await sp.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Mid_run_cancellation_aborts_subsequent_tests()
    {
        // Cancel as soon as the first test runs; second suite must NOT execute.
        SecondSuite.WasInvoked = false;

        var services = new ServiceCollection();
        using var cts = new CancellationTokenSource();
        services.AddSingleton(cts);
        services.AddPulse(p =>
        {
            p.AddSuite<CancelTriggerSuite>();
            p.AddSuite<SecondSuite>();
        });
        await using var sp = services.BuildServiceProvider();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sp.GetRequiredService<ITestExecutor>().RunAsync(cts.Token));

        Assert.False(SecondSuite.WasInvoked,
            "Test in the second suite must not run after mid-run cancellation.");
    }

    [Fact]
    public async Task Filter_match_is_case_sensitive_Ordinal()
    {
        // Contract: PulseTestFramework.MatchesFilter uses StringComparison.Ordinal — different
        // casing must NOT match.
        var fullName = typeof(PassSuite).FullName!;
        var miscased = fullName.ToUpperInvariant();
        Assert.NotEqual(fullName, miscased); // sanity

        var services = new ServiceCollection();
        services.AddPulse(p => p.AddSuite<PassSuite>());
        await using var sp = services.BuildServiceProvider();

        var report = await sp.GetRequiredService<ITestExecutor>()
            .RunAsync(miscased, TestContext.Current.CancellationToken);

        Assert.Equal(0, report.Total);
    }

    [Fact]
    public void Builder_AddSuite_returns_same_instance_for_fluent_chaining()
    {
        var services = new ServiceCollection();
        PulseBuilder? captured = null;
        services.AddPulse(p =>
        {
            captured = p;
            var a = p.AddSuite<PassSuite>();
            var b = a.AddSuite<PassSuite>(_ => new PassSuite());
            var c = b.AddSuite(typeof(PassSuite), _ => new PassSuite());
            Assert.Same(p, a);
            Assert.Same(p, b);
            Assert.Same(p, c);
        });
        Assert.NotNull(captured);
    }

    [Fact]
    public async Task Multiple_matrix_methods_preserve_per_method_row_order()
    {
        var report = await RunAsync(p => p.AddSuite<TwoMatrixMethodsSuite>());

        // Two methods, three rows each — six total. Within each method, row order must follow
        // the source-order of [PulseRow] attributes.
        Assert.Equal(6, report.Total);

        var byMethod = report.Results.GroupBy(r =>
            r.TestName.StartsWith("Width(", StringComparison.Ordinal) ? "Width" : "Height").ToDictionary(g => g.Key, g => g.ToList());

        Assert.Equal(new[] { "Width(1)", "Width(2)", "Width(3)" },
            byMethod["Width"].Select(r => r.TestName).ToArray());
        Assert.Equal(new[] { "Height(10)", "Height(20)", "Height(30)" },
            byMethod["Height"].Select(r => r.TestName).ToArray());
    }

    [Fact]
    public async Task Synchronously_completed_ValueTask_passes()
    {
        // ValueTask.CompletedTask never allocates a Task — AwaitIfNeeded must still treat it
        // as a successful completion.
        var report = await RunAsync(p => p.AddSuite<SyncValueTaskSuite>());

        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Passed, r.Outcome);
    }

    [Fact]
    public async Task Two_runs_from_separate_scopes_produce_independent_reports()
    {
        // The executor is Scoped (RegistrationTests verifies the lifetime). Each scope must
        // produce a report with its own RunContext — no leaked state from prior runs.
        var services = new ServiceCollection();
        services.AddPulse(p =>
        {
            p.AddSuite<PassSuite>();
            p.AddSuite<PassSuite>();
        });
        await using var sp = services.BuildServiceProvider();

        await using var s1 = sp.CreateAsyncScope();
        await using var s2 = sp.CreateAsyncScope();

        var r1 = await s1.ServiceProvider.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);
        var r2 = await s2.ServiceProvider.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, r1.Total);
        Assert.Equal(2, r2.Total);
        Assert.NotSame(r1, r2);
        Assert.NotSame(r1.Results, r2.Results);
        // Both reports captured independently — neither sees double the count.
        Assert.All(r1.Results, x => Assert.Equal(TestOutcome.Passed, x.Outcome));
        Assert.All(r2.Results, x => Assert.Equal(TestOutcome.Passed, x.Outcome));
    }

    [Fact]
    public async Task Passed_TestResult_has_null_Message_and_StackTrace()
    {
        var report = await RunAsync(p => p.AddSuite<PassSuite>());

        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Passed, r.Outcome);
        Assert.Null(r.Message);
        Assert.Null(r.StackTrace);
    }

    [Fact]
    public async Task Skipped_TestResult_has_skip_reason_and_null_StackTrace()
    {
        var report = await RunAsync(p => p.AddSuite<DeclarativeSkipSuite>());

        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Skipped, r.Outcome);
        Assert.Equal("not yet", r.Message);
        Assert.Null(r.StackTrace);
    }

    [Fact]
    public async Task PulseRow_on_PulseCase_method_is_silently_ignored()
    {
        // Current contract: [PulseCase] takes precedence; [PulseRow] is ignored on a case method
        // (no exception thrown). Pulse only inspects PulseRow when a PulseMatrix is present.
        // This locks in the documented behavior — if it ever changes to throw, this test will
        // catch it.
        var report = await RunAsync(p => p.AddSuite<CaseWithStrayRowSuite>());

        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Passed, r.Outcome);
        Assert.Equal(nameof(CaseWithStrayRowSuite.Plain_case), r.TestName);
    }

    [Fact]
    public async Task Empty_string_skip_reason_is_preserved_verbatim()
    {
        // Skip = "" is a valid (if unusual) declaration; Pulse should not coerce it to null.
        var report = await RunAsync(p => p.AddSuite<EmptySkipReasonSuite>());

        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Skipped, r.Outcome);
        Assert.Equal(string.Empty, r.Message);
    }

    [Fact]
    public async Task Suite_factory_receives_host_service_provider()
    {
        // The factory's IServiceProvider argument must be the host's — used by consumers that
        // want to resolve a partial set of dependencies and override the rest.
        var services = new ServiceCollection();
        services.AddSingleton<IMarker>(new Marker("marker-value"));

        IServiceProvider? captured = null;
        services.AddPulse(p => p.AddSuite(sp =>
        {
            captured = sp;
            return new MarkerSuite(sp.GetRequiredService<IMarker>());
        }));

        await using var sp = services.BuildServiceProvider();
        var report = await sp.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Passed, r.Outcome);
    }

}
