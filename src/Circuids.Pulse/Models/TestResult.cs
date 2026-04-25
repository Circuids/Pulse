namespace Circuids.Pulse;

/// <summary>
/// The result of running a single test (one <see cref="PulseCaseAttribute"/> method
/// or one <see cref="PulseRowAttribute"/> row of a <see cref="PulseMatrixAttribute"/> method).
/// </summary>
public sealed record TestResult
{
    /// <summary>The display name of the suite that contained the test.</summary>
    public required string SuiteName { get; init; }

    /// <summary>
    /// The display name of the test. For matrix rows, this includes the row arguments
    /// formatted as <c>MethodName(arg1, arg2, ...)</c>.
    /// </summary>
    public required string TestName { get; init; }

    /// <summary>The terminal outcome.</summary>
    public required TestOutcome Outcome { get; init; }

    /// <summary>
    /// On <see cref="TestOutcome.Failed"/>: the exception's message.
    /// On <see cref="TestOutcome.Skipped"/>: the declared skip reason, if any.
    /// On <see cref="TestOutcome.Passed"/>: <see langword="null"/>.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>The captured stack trace on failure, otherwise <see langword="null"/>.</summary>
    public string? StackTrace { get; init; }

    /// <summary>How long the test took to execute. Zero for skipped tests.</summary>
    public TimeSpan Duration { get; init; }
}
