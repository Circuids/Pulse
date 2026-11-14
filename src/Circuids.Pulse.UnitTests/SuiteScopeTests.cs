using Circuids.Pulse.UnitTests.TestSuites.Scope;

namespace Circuids.Pulse.UnitTests;

/// <summary>
/// Per-suite AsyncServiceScope: construction from a scoped provider, teardown order,
/// scope-disposal failure reporting, singleton preservation, and filter interaction.
/// </summary>
public sealed class SuiteScopeTests
{
    private static async Task<TestRunReport> RunAsync(Action<PulseBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedTracker>();
        services.AddPulse(configure);
        await using var sp = services.BuildServiceProvider();
        return await sp.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);
    }

    private static Guid TrackedId(string entry) => Guid.Parse(entry[(entry.IndexOf(':') + 1)..]);

    [Fact]
    public async Task Scoped_service_is_distinct_per_suite_and_disposed_after_each()
    {
        ScopeTrace.Reset();
        var report = await RunAsync(p =>
        {
            p.AddSuite<TrackerSuiteA>();
            p.AddSuite<TrackerSuiteB>();
        });

        Assert.Equal(2, report.Passed);
        var aId = TrackedId(Assert.Single(ScopeTrace.Entries, e => e.StartsWith("a:", StringComparison.Ordinal)));
        var bId = TrackedId(Assert.Single(ScopeTrace.Entries, e => e.StartsWith("b:", StringComparison.Ordinal)));
        Assert.NotEqual(aId, bId);

        // Each suite's scoped instance is created before its test and disposed before the next suite starts.
        Assert.Equal(
            new[] { $"create:{aId}", $"a:{aId}", $"dispose:{aId}", $"create:{bId}", $"b:{bId}", $"dispose:{bId}" },
            ScopeTrace.Entries);
    }

    [Fact]
    public async Task Scoped_service_is_not_captured_from_executor_resolution_scope()
    {
        ScopeTrace.Reset();
        var services = new ServiceCollection();
        services.AddScoped<ScopedTracker>();
        services.AddPulse(p => p.AddSuite<TrackerSuiteA>());
        await using var sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        await using var executorScope = sp.CreateAsyncScope();
        var outerInstance = executorScope.ServiceProvider.GetRequiredService<ScopedTracker>();
        var report = await executorScope.ServiceProvider
            .GetRequiredService<ITestExecutor>()
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, report.Passed);
        var suiteId = TrackedId(Assert.Single(ScopeTrace.Entries, e => e.StartsWith("a:", StringComparison.Ordinal)));
        Assert.NotEqual(outerInstance.Id, suiteId);
        Assert.Contains($"dispose:{suiteId}", ScopeTrace.Entries);
        Assert.DoesNotContain($"dispose:{outerInstance.Id}", ScopeTrace.Entries);
    }

    [Fact]
    public async Task Teardown_order_is_lifetime_then_suite_then_scope()
    {
        ScopeTrace.Reset();
        var report = await RunAsync(p => p.AddSuite<TeardownOrderSuite>());

        Assert.Equal(1, report.Passed);
        Assert.Collection(ScopeTrace.Entries,
            e => Assert.StartsWith("create:", e, StringComparison.Ordinal),
            e => Assert.StartsWith("ctor:", e, StringComparison.Ordinal),
            e => Assert.Equal("init", e),
            e => Assert.Equal("test", e),
            e => Assert.Equal("lifetime-dispose", e),
            e => Assert.Equal("suite-dispose", e),
            e => Assert.StartsWith("dispose:", e, StringComparison.Ordinal));
        Assert.Equal(7, ScopeTrace.Entries.Count);
    }

    [Fact]
    public async Task Factory_receives_per_suite_scoped_provider()
    {
        ScopeTrace.Reset();
        var services = new ServiceCollection();
        services.AddScoped<ScopedTracker>();
        services.AddPulse(p =>
        {
            p.AddSuite(sp => new TrackerSuiteA(sp.GetRequiredService<ScopedTracker>()));
            p.AddSuite(sp => new TrackerSuiteB(sp.GetRequiredService<ScopedTracker>()));
        });
        await using var sp = services.BuildServiceProvider();
        var report = await sp.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, report.Passed);
        var aId = TrackedId(Assert.Single(ScopeTrace.Entries, e => e.StartsWith("a:", StringComparison.Ordinal)));
        var bId = TrackedId(Assert.Single(ScopeTrace.Entries, e => e.StartsWith("b:", StringComparison.Ordinal)));
        Assert.NotEqual(aId, bId);
        Assert.Equal(
            new[] { $"create:{aId}", $"a:{aId}", $"dispose:{aId}", $"create:{bId}", $"b:{bId}", $"dispose:{bId}" },
            ScopeTrace.Entries);
    }

    [Fact]
    public async Task Scope_dispose_failure_reports_synthetic_row_and_run_continues()
    {
        AfterThrowingScopeSuite.Called = false;
        var services = new ServiceCollection();
        services.AddScoped<ThrowingScopedService>();
        services.AddPulse(p =>
        {
            p.AddSuite<ThrowingScopeSuite>();
            p.AddSuite<AfterThrowingScopeSuite>();
        });
        await using var sp = services.BuildServiceProvider();
        var report = await sp.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, report.Total);
        Assert.Equal(2, report.Passed);
        Assert.Equal(1, report.Failed);

        var failure = Assert.Single(report.Results, r => r.TestName == "(suite scope dispose)");
        Assert.Equal(typeof(ThrowingScopeSuite).FullName, failure.SuiteName);
        Assert.Equal(TestOutcome.Failed, failure.Outcome);
        Assert.Contains("scope-dispose-boom", failure.Message ?? "");
        Assert.Equal(TimeSpan.Zero, failure.Duration);

        Assert.True(AfterThrowingScopeSuite.Called, "The next suite must run after a scope disposal failure.");
        Assert.Contains(report.Results, r =>
            r.SuiteName == typeof(AfterThrowingScopeSuite).FullName && r.Outcome == TestOutcome.Passed);
    }

    [Fact]
    public async Task Singleton_services_remain_shared_across_suites()
    {
        ScopeTrace.Reset();
        var services = new ServiceCollection();
        services.AddSingleton<SingletonTracker>();
        services.AddPulse(p =>
        {
            p.AddSuite<SingletonSuiteA>();
            p.AddSuite<SingletonSuiteB>();
        });

        TestRunReport report;
        await using (var sp = services.BuildServiceProvider())
        {
            report = await sp.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain("singleton-dispose", ScopeTrace.Entries);
        }

        Assert.Equal(2, report.Passed);
        Assert.Equal("singleton-create", ScopeTrace.Entries[0]);
        Assert.Equal(1, ScopeTrace.Entries.Count(e => e == "singleton-create"));
        Assert.Equal(
            TrackedId(Assert.Single(ScopeTrace.Entries, e => e.StartsWith("sa:", StringComparison.Ordinal))),
            TrackedId(Assert.Single(ScopeTrace.Entries, e => e.StartsWith("sb:", StringComparison.Ordinal))));
        Assert.Equal("singleton-dispose", ScopeTrace.Entries[^1]);
        Assert.Equal(4, ScopeTrace.Entries.Count);
    }

    [Fact]
    public async Task Filtered_run_creates_scope_only_for_matching_suite()
    {
        ScopeTrace.Reset();
        var services = new ServiceCollection();
        services.AddScoped<ScopedTracker>();
        services.AddPulse(p =>
        {
            p.AddSuite<TrackerSuiteA>();
            p.AddSuite<TrackerSuiteB>();
        });
        await using var sp = services.BuildServiceProvider();
        var report = await sp.GetRequiredService<ITestExecutor>()
            .RunAsync(typeof(TrackerSuiteB).FullName!, TestContext.Current.CancellationToken);

        Assert.Equal(1, report.Passed);
        Assert.Single(ScopeTrace.Entries, e => e.StartsWith("create:", StringComparison.Ordinal));
        var bId = TrackedId(Assert.Single(ScopeTrace.Entries, e => e.StartsWith("b:", StringComparison.Ordinal)));
        Assert.Equal(new[] { $"create:{bId}", $"b:{bId}", $"dispose:{bId}" }, ScopeTrace.Entries);
    }

    [Fact]
    public async Task Scope_tracked_suite_dispose_is_safe_when_idempotent()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedSuiteService>();
        services.AddPulse(p => p.AddSuite(sp => sp.GetRequiredService<ScopedSuiteService>()));
        await using var sp = services.BuildServiceProvider();
        var report = await sp.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);

        // Pulse's explicit suite teardown and the scope's disposal tracking both dispose the
        // suite instance; an idempotent DisposeAsync makes the second call safe.
        Assert.Equal(1, report.Passed);
        Assert.Equal(0, report.Failed);
        Assert.DoesNotContain(report.Results, r => r.TestName == "(suite scope dispose)" || r.TestName == "(suite Dispose)");
    }
}
