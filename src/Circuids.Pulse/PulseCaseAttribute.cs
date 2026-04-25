namespace Circuids.Pulse;

/// <summary>
/// Marks a single conformance case. Mutually exclusive with <see cref="PulseMatrixAttribute"/>:
/// a method tagged with both attributes is rejected at registration time.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PulseCaseAttribute : Attribute
{
    /// <summary>Optional override for the test's display name. Defaults to the method name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// When set, the test is reported as <see cref="TestOutcome.Skipped"/> with this string as
    /// the message and the test method is not invoked.
    /// </summary>
    public string? Skip { get; init; }
}
