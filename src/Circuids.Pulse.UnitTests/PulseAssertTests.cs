using System.Globalization;

namespace Circuids.Pulse.UnitTests;

public sealed class PulseAssertTests
{

    [Fact]
    public void True_passes_when_condition_is_true()
        => PulseAssert.True(true);

    [Fact]
    public void True_fails_when_condition_is_false()
    {
        var ex = Assert.Throws<PulseAssertionException>(() => PulseAssert.True(false, "needed truthy state"));
        Assert.Contains("PulseAssert.True failed", ex.Message);
        Assert.Contains("Expected: true", ex.Message);
        Assert.Contains("Actual:   false", ex.Message);
        Assert.Contains("Because:  needed truthy state", ex.Message);
    }

    [Fact]
    public void False_passes_when_condition_is_false()
        => PulseAssert.False(false);

    [Fact]
    public void False_fails_when_condition_is_true()
    {
        var ex = Assert.Throws<PulseAssertionException>(() => PulseAssert.False(true));
        Assert.Contains("PulseAssert.False failed", ex.Message);
        Assert.DoesNotContain("Because:", ex.Message);
    }

    [Fact]
    public void Equal_passes_for_equal_values()
        => PulseAssert.Equal(42, 42);

    [Fact]
    public void Equal_fails_for_different_values()
    {
        var ex = Assert.Throws<PulseAssertionException>(() => PulseAssert.Equal(1, 2));
        Assert.Contains("PulseAssert.Equal failed", ex.Message);
        Assert.Contains("Expected: 1", ex.Message);
        Assert.Contains("Actual:   2", ex.Message);
    }

    [Fact]
    public void Equal_with_comparer_uses_supplied_comparer()
    {
        PulseAssert.Equal("abc", "ABC", StringComparer.OrdinalIgnoreCase);
        Assert.Throws<PulseAssertionException>(() =>
            PulseAssert.Equal("abc", "ABC", StringComparer.Ordinal));
    }

    [Fact]
    public void NotEqual_passes_for_different_values()
        => PulseAssert.NotEqual(1, 2);

    [Fact]
    public void NotEqual_fails_for_equal_values()
        => Assert.Throws<PulseAssertionException>(() => PulseAssert.NotEqual(1, 1));

    [Fact]
    public void Equivalent_passes_regardless_of_order()
        => PulseAssert.Equivalent(new[] { 3, 1, 2 }, new[] { 1, 2, 3 });

    [Fact]
    public void Equivalent_respects_multiplicity()
    {
        Assert.Throws<PulseAssertionException>(() =>
            PulseAssert.Equivalent(new[] { 1, 1, 2 }, new[] { 1, 2, 2 }));
    }

    [Fact]
    public void Equivalent_fails_for_different_lengths()
    {
        Assert.Throws<PulseAssertionException>(() =>
            PulseAssert.Equivalent(new[] { 1, 2 }, new[] { 1, 2, 3 }));
    }

    [Fact]
    public void Same_passes_for_same_reference()
    {
        var o = new object();
        PulseAssert.Same(o, o);
    }

    [Fact]
    public void Same_fails_for_distinct_references()
        => Assert.Throws<PulseAssertionException>(() => PulseAssert.Same(new object(), new object()));

    [Fact]
    public void NotSame_passes_for_distinct_references()
        => PulseAssert.NotSame(new object(), new object());

    [Fact]
    public void NotSame_fails_for_same_reference()
    {
        var o = new object();
        Assert.Throws<PulseAssertionException>(() => PulseAssert.NotSame(o, o));
    }

    [Fact]
    public void Null_passes_for_null()
        => PulseAssert.Null(null);

    [Fact]
    public void Null_fails_for_non_null()
        => Assert.Throws<PulseAssertionException>(() => PulseAssert.Null("x"));

    [Fact]
    public void NotNull_passes_for_non_null()
        => PulseAssert.NotNull("x");

    [Fact]
    public void NotNull_fails_for_null()
        => Assert.Throws<PulseAssertionException>(() => PulseAssert.NotNull(null));

    [Fact]
    public void String_Contains_passes_when_present()
        => PulseAssert.Contains("ell", "hello");

    [Fact]
    public void String_Contains_fails_when_absent()
        => Assert.Throws<PulseAssertionException>(() => PulseAssert.Contains("xyz", "hello"));

    [Fact]
    public void String_Contains_is_ordinal()
        => Assert.Throws<PulseAssertionException>(() => PulseAssert.Contains("ELL", "hello"));

    [Fact]
    public void String_DoesNotContain_passes_when_absent()
        => PulseAssert.DoesNotContain("xyz", "hello");

    [Fact]
    public void String_DoesNotContain_fails_when_present()
        => Assert.Throws<PulseAssertionException>(() => PulseAssert.DoesNotContain("ell", "hello"));

    [Fact]
    public void StartsWith_passes_when_prefix_matches()
        => PulseAssert.StartsWith("hel", "hello");

    [Fact]
    public void StartsWith_fails_when_prefix_does_not_match()
        => Assert.Throws<PulseAssertionException>(() => PulseAssert.StartsWith("xyz", "hello"));

    [Fact]
    public void EndsWith_passes_when_suffix_matches()
        => PulseAssert.EndsWith("llo", "hello");

    [Fact]
    public void EndsWith_fails_when_suffix_does_not_match()
        => Assert.Throws<PulseAssertionException>(() => PulseAssert.EndsWith("xyz", "hello"));

    [Fact]
    public void Sequence_Contains_passes_when_element_present()
        => PulseAssert.Contains(2, new[] { 1, 2, 3 });

    [Fact]
    public void Sequence_Contains_fails_when_element_absent()
        => Assert.Throws<PulseAssertionException>(() => PulseAssert.Contains(99, new[] { 1, 2, 3 }));

    [Fact]
    public void Sequence_DoesNotContain_passes_when_absent()
        => PulseAssert.DoesNotContain(99, new[] { 1, 2, 3 });

    [Fact]
    public void Sequence_DoesNotContain_fails_when_present()
        => Assert.Throws<PulseAssertionException>(() => PulseAssert.DoesNotContain(2, new[] { 1, 2, 3 }));

    [Fact]
    public void Empty_passes_for_empty_array()
        => PulseAssert.Empty(Array.Empty<int>());

    [Fact]
    public void Empty_passes_for_empty_string()
        => PulseAssert.Empty("");

    [Fact]
    public void Empty_fails_for_non_empty()
        => Assert.Throws<PulseAssertionException>(() => PulseAssert.Empty(new[] { 1 }));

    [Fact]
    public void NotEmpty_passes_for_non_empty()
        => PulseAssert.NotEmpty(new[] { 1 });

    [Fact]
    public void NotEmpty_fails_for_empty()
        => Assert.Throws<PulseAssertionException>(() => PulseAssert.NotEmpty(Array.Empty<int>()));

    [Fact]
    public void InRange_passes_at_lower_bound()
        => PulseAssert.InRange(0, 0, 10);

    [Fact]
    public void InRange_passes_at_upper_bound()
        => PulseAssert.InRange(10, 0, 10);

    [Fact]
    public void InRange_passes_inside()
        => PulseAssert.InRange(5, 0, 10);

    [Fact]
    public void InRange_fails_below()
        => Assert.Throws<PulseAssertionException>(() => PulseAssert.InRange(-1, 0, 10));

    [Fact]
    public void InRange_fails_above()
        => Assert.Throws<PulseAssertionException>(() => PulseAssert.InRange(11, 0, 10));

    [Fact]
    public void Throws_returns_caught_exception()
    {
        var caught = PulseAssert.Throws<InvalidOperationException>(
            () => throw new InvalidOperationException("nope"));
        Assert.Equal("nope", caught.Message);
    }

    [Fact]
    public void Throws_fails_when_no_exception()
    {
        var ex = Assert.Throws<PulseAssertionException>(() =>
            PulseAssert.Throws<InvalidOperationException>(() => { }));
        Assert.Contains("no exception was thrown", ex.Message);
    }

    [Fact]
    public void Throws_fails_when_wrong_type()
    {
        var ex = Assert.Throws<PulseAssertionException>(() =>
            PulseAssert.Throws<InvalidOperationException>(() => throw new ArgumentException("x")));
        Assert.Contains("wrong exception type", ex.Message);
    }

    [Fact]
    public async Task ThrowsAsync_returns_caught_exception()
    {
        var caught = await PulseAssert.ThrowsAsync<InvalidOperationException>(
            () => throw new InvalidOperationException("nope"));
        Assert.Equal("nope", caught.Message);
    }

    [Fact]
    public async Task ThrowsAsync_fails_when_no_exception()
    {
        await Assert.ThrowsAsync<PulseAssertionException>(() =>
            PulseAssert.ThrowsAsync<InvalidOperationException>(() => Task.CompletedTask));
    }

    [Fact]
    public async Task ThrowsAsync_fails_when_wrong_type()
    {
        await Assert.ThrowsAsync<PulseAssertionException>(() =>
            PulseAssert.ThrowsAsync<InvalidOperationException>(() => throw new ArgumentException("x")));
    }

    [Fact]
    public void Skip_throws_PulseSkipException()
    {
        var ex = Assert.Throws<PulseSkipException>(() => PulseAssert.Skip("not applicable here"));
        Assert.Equal("not applicable here", ex.Message);
    }

    [Fact]
    public void Fail_throws_PulseAssertionException_with_reason()
    {
        var ex = Assert.Throws<PulseAssertionException>(() => PulseAssert.Fail("blew up"));
        Assert.Contains("PulseAssert.Fail", ex.Message);
        Assert.Contains("blew up", ex.Message);
    }

    [Fact]
    public void Because_line_is_omitted_when_null()
    {
        var ex = Assert.Throws<PulseAssertionException>(() => PulseAssert.Equal(1, 2));
        Assert.DoesNotContain("Because:", ex.Message);
    }

    [Fact]
    public void Strings_are_quoted_in_failure_message()
    {
        var ex = Assert.Throws<PulseAssertionException>(() => PulseAssert.Equal("a", "b"));
        Assert.Contains("\"a\"", ex.Message);
        Assert.Contains("\"b\"", ex.Message);
    }

    [Fact]
    public void Enumerables_render_as_brackets()
    {
        var ex = Assert.Throws<PulseAssertionException>(() =>
            PulseAssert.Equivalent(new[] { 1, 2 }, new[] { 9, 8 }));
        Assert.Contains("[1, 2]", ex.Message);
        Assert.Contains("[9, 8]", ex.Message);
    }

    [Fact]
    public void Enumerables_truncate_past_ten_items()
    {
        var big = Enumerable.Range(1, 50).ToArray();
        var ex = Assert.Throws<PulseAssertionException>(() =>
            PulseAssert.Equivalent(big, Array.Empty<int>()));
        Assert.Contains(", …", ex.Message);
    }

    [Fact]
    public void Numeric_values_render_in_invariant_culture()
    {
        var prev = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // uses ',' as decimal separator
        try
        {
            var ex = Assert.Throws<PulseAssertionException>(() => PulseAssert.Equal(1.5, 2.5));
            Assert.Contains("1.5", ex.Message);
            Assert.Contains("2.5", ex.Message);
        }
        finally
        {
            CultureInfo.CurrentCulture = prev;
        }
    }
}
