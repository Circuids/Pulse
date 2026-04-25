namespace Circuids.Pulse.Internal;

/// <summary>
/// Per-run state shared between <see cref="TestExecutor"/> and <see cref="PulseTestFramework"/>.
/// One instance is created per call to <see cref="ITestExecutor.RunAsync(System.Threading.CancellationToken)"/>;
/// it is captured by closure when the test framework is registered with MTP and used to feed the
/// executor's <see cref="TestRunReport"/> back to the caller.
/// </summary>
internal sealed class PulseRunContext
{
    private readonly object _lock = new();
    private readonly List<TestResult> _results = new();

    public required PulseBuilder Builder { get; init; }
    public required IServiceProvider HostServices { get; init; }
    public required string? SuiteFilter { get; init; }
    public required CancellationToken OuterCancellation { get; init; }
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    public RuntimeEnvironment Environment { get; } = RuntimeEnvironmentProbe.Capture();

    public void Add(TestResult result)
    {
        lock (_lock) _results.Add(result);
    }

    public IReadOnlyList<TestResult> Snapshot()
    {
        lock (_lock) return _results.ToArray();
    }
}
