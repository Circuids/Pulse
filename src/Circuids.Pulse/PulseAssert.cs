using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Circuids.Pulse;

/// <summary>
/// Pulse's middle-ground assertion surface. Sits intentionally between a tiny per-consumer helper
/// and a full assertion library (xUnit.Assert, Shouldly, FluentAssertions). All members follow the
/// prefix-positional shape <c>Method(expected, actual, because)</c> and produce a single consistent
/// failure-message format. Failures throw <see cref="PulseAssertionException"/>; the Pulse executor
/// maps any exception to <see cref="TestOutcome.Failed"/>.
/// </summary>
/// <remarks>
/// Bar to add a new method: two unrelated consumers must independently hit the same gap. Keep this
/// surface deliberately small — Pulse is not an assertion library.
/// </remarks>
public static class PulseAssert
{
    private const int EnumerablePreviewLimit = 10;

    /// <summary>Asserts that <paramref name="condition"/> is <see langword="true"/>.</summary>
    public static void True(bool condition, string? because = null)
    {
        if (!condition)
            throw Failure(nameof(True), "value was false.", expected: "true", actual: "false", because);
    }

    /// <summary>Asserts that <paramref name="condition"/> is <see langword="false"/>.</summary>
    public static void False(bool condition, string? because = null)
    {
        if (condition)
            throw Failure(nameof(False), "value was true.", expected: "false", actual: "true", because);
    }

    /// <summary>Asserts that <paramref name="actual"/> equals <paramref name="expected"/> using the default comparer.</summary>
    public static void Equal<T>(T expected, T actual, string? because = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw Failure(nameof(Equal), "values differ.", Format(expected), Format(actual), because);
    }

    /// <summary>Asserts that <paramref name="actual"/> equals <paramref name="expected"/> using the supplied <paramref name="comparer"/>.</summary>
    public static void Equal<T>(T expected, T actual, IEqualityComparer<T> comparer, string? because = null)
    {
        if (comparer is null) throw new ArgumentNullException(nameof(comparer));
        if (!comparer.Equals(expected, actual))
            throw Failure(nameof(Equal), "values differ.", Format(expected), Format(actual), because);
    }

    /// <summary>Asserts that <paramref name="actual"/> does not equal <paramref name="expected"/>.</summary>
    public static void NotEqual<T>(T expected, T actual, string? because = null)
    {
        if (EqualityComparer<T>.Default.Equals(expected, actual))
            throw Failure(nameof(NotEqual), "values were equal.", $"not {Format(expected)}", Format(actual), because);
    }

    /// <summary>
    /// Asserts that <paramref name="actual"/> contains the same elements as <paramref name="expected"/>,
    /// regardless of order (multiset equality via the default comparer).
    /// </summary>
    public static void Equivalent<T>(IEnumerable<T> expected, IEnumerable<T> actual, string? because = null)
    {
        if (expected is null) throw new ArgumentNullException(nameof(expected));
        if (actual is null) throw new ArgumentNullException(nameof(actual));

        var expectedList = expected.ToList();
        var actualList = actual.ToList();

        if (expectedList.Count != actualList.Count
            || !expectedList.GroupBy(x => x).All(g =>
                actualList.Count(a => EqualityComparer<T>.Default.Equals(a, g.Key)) == g.Count()))
        {
            throw Failure(
                nameof(Equivalent),
                "sequences are not equivalent (order-insensitive multiset comparison).",
                Format(expectedList),
                Format(actualList),
                because);
        }
    }

    /// <summary>Asserts that <paramref name="actual"/> is the same instance as <paramref name="expected"/>.</summary>
    public static void Same(object? expected, object? actual, string? because = null)
    {
        if (!ReferenceEquals(expected, actual))
            throw Failure(nameof(Same), "instances differ.", Format(expected), Format(actual), because);
    }

    /// <summary>Asserts that <paramref name="actual"/> is not the same instance as <paramref name="expected"/>.</summary>
    public static void NotSame(object? expected, object? actual, string? because = null)
    {
        if (ReferenceEquals(expected, actual))
            throw Failure(nameof(NotSame), "instances were the same.", $"not {Format(expected)}", Format(actual), because);
    }

    /// <summary>Asserts that <paramref name="value"/> is <see langword="null"/>.</summary>
    public static void Null(object? value, string? because = null)
    {
        if (value is not null)
            throw Failure(nameof(Null), "value was not null.", "null", Format(value), because);
    }

    /// <summary>Asserts that <paramref name="value"/> is not <see langword="null"/>.</summary>
    public static void NotNull([NotNull] object? value, string? because = null)
    {
        if (value is null)
            throw Failure(nameof(NotNull), "value was null.", "non-null", "null", because);
    }

    /// <summary>Asserts that <paramref name="actual"/> contains <paramref name="substring"/> (ordinal).</summary>
    public static void Contains(string substring, string actual, string? because = null)
    {
        if (substring is null) throw new ArgumentNullException(nameof(substring));
        if (actual is null) throw new ArgumentNullException(nameof(actual));
        if (!actual.Contains(substring, StringComparison.Ordinal))
            throw Failure(nameof(Contains), "substring not found.", $"contains {Format(substring)}", Format(actual), because);
    }

    /// <summary>Asserts that <paramref name="actual"/> does not contain <paramref name="substring"/> (ordinal).</summary>
    public static void DoesNotContain(string substring, string actual, string? because = null)
    {
        if (substring is null) throw new ArgumentNullException(nameof(substring));
        if (actual is null) throw new ArgumentNullException(nameof(actual));
        if (actual.Contains(substring, StringComparison.Ordinal))
            throw Failure(nameof(DoesNotContain), "substring was found.", $"does not contain {Format(substring)}", Format(actual), because);
    }

    /// <summary>Asserts that <paramref name="actual"/> starts with <paramref name="prefix"/> (ordinal).</summary>
    public static void StartsWith(string prefix, string actual, string? because = null)
    {
        if (prefix is null) throw new ArgumentNullException(nameof(prefix));
        if (actual is null) throw new ArgumentNullException(nameof(actual));
        if (!actual.StartsWith(prefix, StringComparison.Ordinal))
            throw Failure(nameof(StartsWith), "prefix mismatch.", $"starts with {Format(prefix)}", Format(actual), because);
    }

    /// <summary>Asserts that <paramref name="actual"/> ends with <paramref name="suffix"/> (ordinal).</summary>
    public static void EndsWith(string suffix, string actual, string? because = null)
    {
        if (suffix is null) throw new ArgumentNullException(nameof(suffix));
        if (actual is null) throw new ArgumentNullException(nameof(actual));
        if (!actual.EndsWith(suffix, StringComparison.Ordinal))
            throw Failure(nameof(EndsWith), "suffix mismatch.", $"ends with {Format(suffix)}", Format(actual), because);
    }

    /// <summary>Asserts that <paramref name="actual"/> contains the element <paramref name="expected"/>.</summary>
    public static void Contains<T>(T expected, IEnumerable<T> actual, string? because = null)
    {
        if (actual is null) throw new ArgumentNullException(nameof(actual));
        if (!actual.Contains(expected!))
            throw Failure(nameof(Contains), "element not found.", $"contains {Format(expected)}", Format(actual), because);
    }

    /// <summary>Asserts that <paramref name="actual"/> does not contain the element <paramref name="expected"/>.</summary>
    public static void DoesNotContain<T>(T expected, IEnumerable<T> actual, string? because = null)
    {
        if (actual is null) throw new ArgumentNullException(nameof(actual));
        if (actual.Contains(expected!))
            throw Failure(nameof(DoesNotContain), "element was found.", $"does not contain {Format(expected)}", Format(actual), because);
    }

    /// <summary>Asserts that <paramref name="actual"/> is empty.</summary>
    public static void Empty(IEnumerable actual, string? because = null)
    {
        if (actual is null) throw new ArgumentNullException(nameof(actual));
        var enumerator = actual.GetEnumerator();
        try
        {
            if (enumerator.MoveNext())
                throw Failure(nameof(Empty), "sequence was not empty.", "empty", Format(actual), because);
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    /// <summary>Asserts that <paramref name="actual"/> is not empty.</summary>
    public static void NotEmpty(IEnumerable actual, string? because = null)
    {
        if (actual is null) throw new ArgumentNullException(nameof(actual));
        var enumerator = actual.GetEnumerator();
        try
        {
            if (!enumerator.MoveNext())
                throw Failure(nameof(NotEmpty), "sequence was empty.", "non-empty", "empty", because);
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Asserts that <paramref name="actual"/> is within the inclusive range
    /// [<paramref name="low"/>, <paramref name="high"/>].
    /// </summary>
    public static void InRange<T>(T actual, T low, T high, string? because = null) where T : IComparable<T>
    {
        if (actual is null) throw new ArgumentNullException(nameof(actual));
        if (low is null) throw new ArgumentNullException(nameof(low));
        if (high is null) throw new ArgumentNullException(nameof(high));
        if (actual.CompareTo(low) < 0 || actual.CompareTo(high) > 0)
            throw Failure(nameof(InRange), "value out of range.",
                $"[{Format(low)}, {Format(high)}]", Format(actual), because);
    }

    /// <summary>
    /// Asserts that invoking <paramref name="action"/> throws an exception assignable to <typeparamref name="T"/>,
    /// and returns it for further inspection.
    /// </summary>
    public static T Throws<T>(Action action, string? because = null) where T : Exception
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        try { action(); }
        catch (T expected) { return expected; }
        catch (Exception other)
        {
            throw Failure(nameof(Throws), "wrong exception type.",
                $"throws {typeof(T).FullName}", other.GetType().FullName ?? "(unknown)", because);
        }

        throw Failure(nameof(Throws), "no exception was thrown.",
            $"throws {typeof(T).FullName}", "no exception", because);
    }

    /// <summary>
    /// Asserts that awaiting <paramref name="action"/> throws an exception assignable to <typeparamref name="T"/>,
    /// and returns it for further inspection.
    /// </summary>
    public static async Task<T> ThrowsAsync<T>(Func<Task> action, string? because = null) where T : Exception
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        try { await action().ConfigureAwait(false); }
        catch (T expected) { return expected; }
        catch (Exception other)
        {
            throw Failure(nameof(ThrowsAsync), "wrong exception type.",
                $"throws {typeof(T).FullName}", other.GetType().FullName ?? "(unknown)", because);
        }

        throw Failure(nameof(ThrowsAsync), "no exception was thrown.",
            $"throws {typeof(T).FullName}", "no exception", because);
    }

    /// <summary>
    /// Skips the current test from inside its body. Equivalent to throwing
    /// <see cref="PulseSkipException"/> with the supplied <paramref name="reason"/>.
    /// </summary>
    [DoesNotReturn]
    public static void Skip(string reason)
    {
        if (reason is null) throw new ArgumentNullException(nameof(reason));
        throw new PulseSkipException(reason);
    }

    /// <summary>Fails the current test unconditionally with the supplied reason.</summary>
    [DoesNotReturn]
    public static void Fail(string because)
    {
        if (because is null) throw new ArgumentNullException(nameof(because));
        throw new PulseAssertionException($"PulseAssert.Fail: {because}");
    }

    private static PulseAssertionException Failure(
        string method, string description, string expected, string actual, string? because)
    {
        var sb = new StringBuilder();
        sb.Append("PulseAssert.").Append(method).Append(" failed: ").AppendLine(description);
        sb.Append("  Expected: ").AppendLine(expected);
        sb.Append("  Actual:   ").Append(actual);
        if (!string.IsNullOrEmpty(because))
        {
            sb.AppendLine();
            sb.Append("  Because:  ").Append(because);
        }
        return new PulseAssertionException(sb.ToString());
    }

    private static string Format(object? value)
    {
        if (value is null) return "null";
        if (value is string s) return $"\"{s}\"";
        if (value is char c) return $"'{c}'";
        if (value is bool b) return b ? "true" : "false";
        if (value is IEnumerable enumerable && value is not string)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            var count = 0;
            var truncated = false;
            foreach (var item in enumerable)
            {
                if (count >= EnumerablePreviewLimit) { truncated = true; break; }
                if (count > 0) sb.Append(", ");
                sb.Append(Format(item));
                count++;
            }
            if (truncated) sb.Append(", …");
            sb.Append(']');
            return sb.ToString();
        }
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.GetType().Name;
    }
}
