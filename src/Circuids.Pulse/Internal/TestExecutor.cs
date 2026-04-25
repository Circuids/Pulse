using Microsoft.Testing.Platform.Builder;

namespace Circuids.Pulse.Internal;

/// <summary>
/// Default <see cref="ITestExecutor"/>. Each call to <see cref="RunAsync(CancellationToken)"/>
/// hosts an MTP <see cref="TestApplication"/> in-process, registers <see cref="PulseTestFramework"/>
/// as the test framework, and runs the session. Test results are captured into a per-run
/// <see cref="PulseRunContext"/> as the framework publishes them onto MTP's message bus, and
/// returned to the caller as a strongly-typed <see cref="TestRunReport"/>.
/// </summary>
internal sealed class TestExecutor : ITestExecutor
{
    // --no-banner suppresses MTP's startup banner (we are an embedded host, not a CLI).
    private static readonly string[] HostArgs = ["--no-banner"];

    private readonly IServiceProvider _services;
    private readonly PulseBuilder _builder;

    public TestExecutor(IServiceProvider services, PulseBuilder builder)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    public Task<TestRunReport> RunAsync(CancellationToken cancellationToken = default) =>
        RunCoreAsync(suiteFilter: null, cancellationToken);

    public Task<TestRunReport> RunAsync(string suiteName, CancellationToken cancellationToken = default)
    {
        if (suiteName is null) throw new ArgumentNullException(nameof(suiteName));
        return RunCoreAsync(suiteFilter: suiteName, cancellationToken);
    }

    private async Task<TestRunReport> RunCoreAsync(string? suiteFilter, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var runContext = new PulseRunContext
        {
            Builder = _builder,
            HostServices = _services,
            SuiteFilter = suiteFilter,
            OuterCancellation = cancellationToken,
        };

        var appBuilder = await TestApplication.CreateBuilderAsync(HostArgs).ConfigureAwait(false);

        appBuilder.RegisterTestFramework(
            _ => new PulseTestFrameworkCapabilities(),
            (_, _) => new PulseTestFramework(runContext));

        using var application = await appBuilder.BuildAsync().ConfigureAwait(false);

        // RunAsync's int return code reflects test success/failure (per MTP convention). We
        // surface that solely through the report (Failed > 0 ⇒ exit code != 0). The numeric
        // return is intentionally not exposed — TestRunReport.Success is the consumer contract.
        _ = await application.RunAsync().ConfigureAwait(false);

        return new TestRunReport
        {
            AssignedPlatform = string.IsNullOrWhiteSpace(_builder.AssignedPlatform)
                ? "(unassigned)"
                : _builder.AssignedPlatform!,
            RuntimeEnvironment = runContext.Environment,
            Timestamp = runContext.Timestamp,
            Results = runContext.Snapshot(),
        };
    }
}
