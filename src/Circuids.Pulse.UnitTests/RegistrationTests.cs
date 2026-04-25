namespace Circuids.Pulse.UnitTests;

/// <summary>
/// Validates DI registration: argument validation, lifetimes (scoped executor, singleton builder),
/// multi-suite ordering, empty registrations, and filter semantics.
/// </summary>
public sealed class RegistrationTests
{
    [Fact]
    public void AddPulse_with_null_services_throws()
    {
        IServiceCollection? services = null;
        Assert.Throws<ArgumentNullException>(() => services!.AddPulse(_ => { }));
    }

    [Fact]
    public void AddPulse_with_null_configure_throws()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddPulse(null!));
    }

    [Fact]
    public void AddSuite_generic_with_null_factory_throws()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            services.AddPulse(p => p.AddSuite<EmptySuite>(null!)));
    }

    [Fact]
    public void AddSuite_type_with_null_type_throws()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            services.AddPulse(p => p.AddSuite(null!, _ => new EmptySuite())));
    }

    [Fact]
    public void AddSuite_type_with_null_factory_throws()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            services.AddPulse(p => p.AddSuite(typeof(EmptySuite), null!)));
    }

    [Fact]
    public void Executor_is_registered_as_Scoped()
    {
        var services = new ServiceCollection();
        services.AddPulse(p => p.AddSuite<EmptySuite>());

        var descriptor = services.Single(d => d.ServiceType == typeof(ITestExecutor));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void Builder_is_registered_as_Singleton_and_shared()
    {
        var services = new ServiceCollection();
        services.AddPulse(p => p.AddSuite<EmptySuite>());

        using var sp = services.BuildServiceProvider();
        var b1 = sp.GetRequiredService<PulseBuilder>();
        var b2 = sp.GetRequiredService<PulseBuilder>();
        Assert.Same(b1, b2);
    }

    [Fact]
    public async Task Empty_registration_returns_empty_successful_report()
    {
        var services = new ServiceCollection();
        services.AddPulse(_ => { });
        await using var sp = services.BuildServiceProvider();

        var report = await sp.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, report.Total);
        Assert.True(report.Success);
        Assert.Empty(report.Results);
    }

    [Fact]
    public async Task Suites_run_in_registration_order()
    {
        var services = new ServiceCollection();
        services.AddPulse(p =>
        {
            p.AddSuite<SuiteB>();
            p.AddSuite<SuiteA>();
            p.AddSuite<SuiteC>();
        });
        await using var sp = services.BuildServiceProvider();

        var report = await sp.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);
        var order = report.Results.Select(r => r.SuiteName).ToArray();

        Assert.Equal(typeof(SuiteB).FullName, order[0]);
        Assert.Equal(typeof(SuiteA).FullName, order[1]);
        Assert.Equal(typeof(SuiteC).FullName, order[2]);
    }

    [Fact]
    public async Task Same_suite_registered_twice_runs_twice()
    {
        var services = new ServiceCollection();
        services.AddPulse(p =>
        {
            p.AddSuite<SuiteA>();
            p.AddSuite<SuiteA>();
        });
        await using var sp = services.BuildServiceProvider();

        var report = await sp.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, report.Total);
    }

    [Fact]
    public async Task Filter_with_no_matching_suite_returns_empty_report()
    {
        var services = new ServiceCollection();
        services.AddPulse(p =>
        {
            p.AddSuite<SuiteA>();
            p.AddSuite<SuiteB>();
        });
        await using var sp = services.BuildServiceProvider();

        var report = await sp.GetRequiredService<ITestExecutor>()
            .RunAsync("Some.Other.Suite.That.Does.Not.Exist", TestContext.Current.CancellationToken);

        Assert.Equal(0, report.Total);
        Assert.True(report.Success);
    }

    [Fact]
    public async Task Whitespace_AssignedPlatform_falls_back_to_unassigned()
    {
        var services = new ServiceCollection();
        services.AddPulse(p =>
        {
            p.AssignedPlatform = "   ";
            p.AddSuite<SuiteA>();
        });
        await using var sp = services.BuildServiceProvider();

        var report = await sp.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);
        Assert.Equal("(unassigned)", report.AssignedPlatform);
    }

    [Fact]
    public async Task Suite_construction_failure_reports_a_Failed_result_and_continues()
    {
        var services = new ServiceCollection();
        services.AddPulse(p =>
        {
            p.AddSuite<ThrowsInCtorSuite>();
            p.AddSuite<SuiteA>();
        });
        await using var sp = services.BuildServiceProvider();

        var report = await sp.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, report.Total);
        Assert.Equal(1, report.Failed);
        Assert.Equal(1, report.Passed);
        var failed = Assert.Single(report.Results, r => r.Outcome == TestOutcome.Failed);
        Assert.Contains("Failed to construct suite", failed.Message);
    }

    public sealed class EmptySuite
    {
        [PulseCase] public void NoOp() { }
    }

    public sealed class SuiteA { [PulseCase] public void A() { } }
    public sealed class SuiteB { [PulseCase] public void B() { } }
    public sealed class SuiteC { [PulseCase] public void C() { } }

    public sealed class ThrowsInCtorSuite
    {
        public ThrowsInCtorSuite() => throw new InvalidOperationException("ctor boom");
        [PulseCase] public void NoOp() { }
    }
}
