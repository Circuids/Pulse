namespace Circuids.Pulse;

/// <summary>
/// The full report of a Pulse test run. This is the only stability contract Pulse
/// exposes to consumer UIs and CI tooling — its JSON shape (via <see cref="PulseJsonContext"/>)
/// is what downstream renderers and parsers bind to.
/// </summary>
public sealed record TestRunReport
{
    /// <summary>
    /// The report schema identifier. This value changes only when the JSON contract needs a new
    /// compatibility boundary; v1 reports use <c>"pulse/v1"</c>.
    /// </summary>
    public string Schema { get; init; } = "pulse/v1";

    /// <summary>
    /// The freeform consumer-supplied label set via <see cref="PulseBuilder.AssignedPlatform"/>.
    /// If unset, this is the literal string <c>"(unassigned)"</c>. Pulse does not interpret it.
    /// </summary>
    public required string AssignedPlatform { get; init; }

    /// <summary>Auto-detected ground-truth runtime facts.</summary>
    public required RuntimeEnvironment RuntimeEnvironment { get; init; }

    /// <summary>The instant the run started (UTC).</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>One entry per executed test (or one entry per matrix row).</summary>
    public required IReadOnlyList<TestResult> Results { get; init; }

    /// <summary>
    /// Total elapsed time for the full Pulse run, including runner setup and report construction.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Total number of tests run.</summary>
    public int Total => Results.Count;

    /// <summary>Number of tests with <see cref="TestOutcome.Passed"/>.</summary>
    public int Passed => Results.Count(r => r.Outcome == TestOutcome.Passed);

    /// <summary>Number of tests with <see cref="TestOutcome.Failed"/>.</summary>
    public int Failed => Results.Count(r => r.Outcome == TestOutcome.Failed);

    /// <summary>Number of tests with <see cref="TestOutcome.Skipped"/>.</summary>
    public int Skipped => Results.Count(r => r.Outcome == TestOutcome.Skipped);

    /// <summary><see langword="true"/> when no tests failed.</summary>
    public bool Success => Failed == 0;
}
