namespace Circuids.Pulse;

/// <summary>
/// Throw this from a test method body to mark the current test as skipped at runtime
/// (as opposed to declaratively via the <c>Skip</c> attribute property). Pulse maps this
/// exception to <see cref="TestOutcome.Skipped"/>; xUnit's <c>SkipException</c> (if present)
/// is also recognized by name.
/// </summary>
public sealed class PulseSkipException : Exception
{
    /// <summary>Initializes the exception with a skip reason.</summary>
    public PulseSkipException(string reason) : base(reason) { }
}
