namespace Circuids.Pulse.UnitTests;

/// <summary>
/// RuntimeEnvironment is registered as a DI singleton by AddPulse and can be
/// injected into suites; the same instance flows into TestRunReport.RuntimeEnvironment.
/// </summary>
public sealed class RuntimeEnvironmentDiTests
{
    [Fact]
    public async Task AddPulse_registers_RuntimeEnvironment_as_singleton()
    {
        var services = new ServiceCollection();
        services.AddPulse(p => { });
        await using var sp = services.BuildServiceProvider();

        var first = sp.GetRequiredService<RuntimeEnvironment>();
        var second = sp.GetRequiredService<RuntimeEnvironment>();

        Assert.Same(first, second);
        Assert.False(string.IsNullOrEmpty(first.FrameworkDescription));
    }

    [Fact]
    public async Task Suite_can_inject_RuntimeEnvironment_and_report_uses_same_instance()
    {
        InjectingSuite.Captured = null;

        var services = new ServiceCollection();
        services.AddPulse(p => p.AddSuite<InjectingSuite>());
        await using var sp = services.BuildServiceProvider();

        var diInstance = sp.GetRequiredService<RuntimeEnvironment>();
        var report = await sp.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(InjectingSuite.Captured);
        Assert.Same(diInstance, InjectingSuite.Captured);
        Assert.Same(diInstance, report.RuntimeEnvironment);
    }

    private sealed class InjectingSuite
    {
        public static RuntimeEnvironment? Captured;
        private readonly RuntimeEnvironment _env;
        public InjectingSuite(RuntimeEnvironment env) => _env = env;

        [PulseCase]
        public void Captures_environment() => Captured = _env;
    }
}
