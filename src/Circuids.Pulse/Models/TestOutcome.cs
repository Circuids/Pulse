namespace Circuids.Pulse;

/// <summary>
/// Possible terminal outcomes for a single test execution.
/// </summary>
public enum TestOutcome
{
    /// <summary>The test method completed without throwing.</summary>
    Passed,

    /// <summary>The test method threw an exception that was not a recognized skip signal.</summary>
    Failed,

    /// <summary>
    /// The test was skipped — either declaratively via the <c>Skip</c> property on
    /// <see cref="PulseCaseAttribute"/>, <see cref="PulseMatrixAttribute"/>, or
    /// <see cref="PulseRowAttribute"/>, or imperatively by throwing a <see cref="PulseSkipException"/>.
    /// </summary>
    Skipped,
}
