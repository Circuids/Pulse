namespace Circuids.Pulse;

/// <summary>
/// Provides one row of arguments for a <see cref="PulseMatrixAttribute"/>-tagged method.
/// Argument values must be compile-time constants — the same restriction xUnit's
/// <c>InlineData</c> imposes.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class PulseRowAttribute : Attribute
{
    /// <summary>Initializes a row with positional arguments bound to the matrix method's parameters.</summary>
    public PulseRowAttribute(params object?[] arguments)
    {
        Arguments = arguments ?? Array.Empty<object?>();
    }

    /// <summary>The positional arguments for this row.</summary>
    public object?[] Arguments { get; }

    /// <summary>
    /// When set, this single row is reported as <see cref="TestOutcome.Skipped"/> with this
    /// string as the message.
    /// </summary>
    public string? Skip { get; init; }
}
