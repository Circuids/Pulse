using Microsoft.Extensions.DependencyInjection;
using Microsoft.Testing.Platform.Builder;

namespace Circuids.Pulse.Internal;

/// <summary>
/// Default <see cref="ITestExecutor"/>. Each <see cref="RunAsync(CancellationToken)"/> call hosts
/// an MTP <see cref="TestApplication"/> in-process, registers <see cref="PulseTestFramework"/>,
/// and returns a strongly-typed <see cref="TestRunReport"/>.
/// </summary>
internal sealed class TestExecutor : ITestExecutor
{
    // --no-banner suppresses MTP's CLI banner; Pulse is embedded, not a console app.
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

        var environment = _services.GetService<RuntimeEnvironment>() ?? RuntimeEnvironmentProbe.Capture();

        var runContext = new PulseRunContext
        {
            Builder = _builder,
            HostServices = _services,
            SuiteFilter = suiteFilter,
            OuterCancellation = cancellationToken,
            Environment = environment,
        };

        var appBuilder = await TestApplication.CreateBuilderAsync(HostArgs).ConfigureAwait(false);

        appBuilder.RegisterTestFramework(
            _ => new PulseTestFrameworkCapabilities(),
            (_, _) => new PulseTestFramework(runContext));

        using var application = await appBuilder.BuildAsync().ConfigureAwait(false);

        // MTP's int return code is intentionally discarded; TestRunReport.Success is the contract.
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
