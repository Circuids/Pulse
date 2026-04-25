namespace Circuids.Pulse.Blazor.WebAssembly.Sample.Conformance;

/// <summary>
/// Tiny sample-only assertion helper. Pulse does not ship its own assertion library — any
/// exception that escapes a test body is surfaced as <see cref="TestOutcome.Failed"/> with the
/// exception's message. Suites in real projects typically reuse xUnit / FluentAssertions / etc.
/// </summary>
internal static class PulseAssert
{
    public static void True(bool condition, string because)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {because}");
    }

    public static void Equal<T>(T expected, T actual, string because)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException(
                $"Expected '{expected}' but found '{actual}': {because}");
    }

    public static void NotNull(object? value, string because)
    {
        if (value is null)
            throw new InvalidOperationException($"Expected non-null: {because}");
    }
}
