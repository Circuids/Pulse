namespace Circuids.Pulse;

/// <summary>
/// Marks a parameterized conformance matrix. Each <see cref="PulseRowAttribute"/> on the same
/// method becomes one independently reported test case. Mutually exclusive with
/// <see cref="PulseCaseAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PulseMatrixAttribute : Attribute
{
    /// <summary>
    /// Optional override for the matrix's display-name root. Each row's display name is built
    /// as <c>{DisplayName ?? MethodName}({arg1}, {arg2}, ...)</c>.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// When set, every row in the matrix is reported as <see cref="TestOutcome.Skipped"/> with
    /// this string as the message.
    /// </summary>
    public string? Skip { get; init; }

    /// <summary>
    /// Per-row cooperative timeout in milliseconds. <c>0</c> means "inherit
    /// <see cref="PulseBuilder.DefaultTestTimeout"/>". Enforcement requires the matrix method
    /// to accept a trailing <see cref="CancellationToken"/> parameter and honor it.
    /// </summary>
    public int TimeoutMs { get; init; }
}
