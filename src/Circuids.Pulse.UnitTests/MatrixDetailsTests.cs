namespace Circuids.Pulse.UnitTests;

/// <summary>
/// Validates [PulseMatrix] / [PulseRow] semantics: per-row vs matrix-level skip, display name
/// override, argument formatting in the synthesized test name, and per-row independence.
/// </summary>
public sealed class MatrixDetailsTests
{
    private static async Task<TestRunReport> RunAsync(Action<PulseBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddPulse(configure);
        await using var sp = services.BuildServiceProvider();
        return await sp.GetRequiredService<ITestExecutor>().RunAsync();
    }

    [Fact]
    public async Task Per_row_skip_skips_only_that_row()
    {
        var report = await RunAsync(p => p.AddSuite<PerRowSkipSuite>());

        Assert.Equal(3, report.Total);
        Assert.Equal(2, report.Passed);
        Assert.Equal(1, report.Skipped);

        var skipped = Assert.Single(report.Results, r => r.Outcome == TestOutcome.Skipped);
        Assert.Contains("99", skipped.TestName);
        Assert.Equal("flaky platform", skipped.Message);
    }

    [Fact]
    public async Task Matrix_level_skip_skips_every_row()
    {
        var report = await RunAsync(p => p.AddSuite<MatrixLevelSkipSuite>());

        Assert.Equal(3, report.Total);
        Assert.Equal(0, report.Passed);
        Assert.Equal(3, report.Skipped);
        Assert.All(report.Results, r => Assert.Equal("temporarily disabled", r.Message));
    }

    [Fact]
    public async Task DisplayName_on_PulseCase_replaces_method_name()
    {
        var report = await RunAsync(p => p.AddSuite<DisplayNameCaseSuite>());
        var r = Assert.Single(report.Results);
        Assert.Equal("renders correctly on phone", r.TestName);
    }

    [Fact]
    public async Task DisplayName_on_PulseMatrix_replaces_root_in_row_names()
    {
        var report = await RunAsync(p => p.AddSuite<DisplayNameMatrixSuite>());

        Assert.Equal(2, report.Total);
        Assert.All(report.Results, r => Assert.StartsWith("viewport spec(", r.TestName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Argument_formatting_quotes_strings_chars_and_renders_null_and_bool()
    {
        var report = await RunAsync(p => p.AddSuite<ArgumentFormattingSuite>());

        var names = report.Results.Select(r => r.TestName).ToArray();
        Assert.Contains(names, n => n.Contains("\"hello\""));
        Assert.Contains(names, n => n.Contains("'x'"));
        Assert.Contains(names, n => n.Contains("true"));
        Assert.Contains(names, n => n.Contains("null"));
    }

    [Fact]
    public async Task Multiple_parameter_row_passes_all_arguments_in_order()
    {
        var report = await RunAsync(p => p.AddSuite<MultiParamSuite>());

        var r = Assert.Single(report.Results);
        Assert.Equal(TestOutcome.Passed, r.Outcome);
        Assert.Contains("1", r.TestName);
        Assert.Contains("\"two\"", r.TestName);
        Assert.Contains("true", r.TestName);
    }

    [Fact]
    public async Task Row_failure_is_isolated_from_other_rows()
    {
        var report = await RunAsync(p => p.AddSuite<MixedOutcomeMatrixSuite>());

        Assert.Equal(3, report.Total);
        Assert.Equal(2, report.Passed);
        Assert.Equal(1, report.Failed);
        var failed = Assert.Single(report.Results, r => r.Outcome == TestOutcome.Failed);
        Assert.Contains("0", failed.TestName);
    }

    // ---- fixtures ----

    public sealed class PerRowSkipSuite
    {
        [PulseMatrix]
        [PulseRow(1)]
        [PulseRow(99, Skip = "flaky platform")]
        [PulseRow(2)]
        public void Width(int w) => Assert.True(w > 0);
    }

    public sealed class MatrixLevelSkipSuite
    {
        // Matrix-level Skip overrides every row's Skip behavior.
        [PulseMatrix(Skip = "temporarily disabled")]
        [PulseRow(1)]
        [PulseRow(2)]
        [PulseRow(3)]
        public void Width(int w) => throw new InvalidOperationException("must not run");
    }

    public sealed class DisplayNameCaseSuite
    {
        [PulseCase(DisplayName = "renders correctly on phone")]
        public void Method_name_should_be_replaced() { }
    }

    public sealed class DisplayNameMatrixSuite
    {
        [PulseMatrix(DisplayName = "viewport spec")]
        [PulseRow(390)]
        [PulseRow(768)]
        public void Method_name_replaced(int w) => Assert.True(w > 0);
    }

    public sealed class ArgumentFormattingSuite
    {
        [PulseMatrix]
        [PulseRow("hello")]
        public void StringArg(string s) => Assert.NotNull(s);

        [PulseMatrix]
        [PulseRow('x')]
        public void CharArg(char c) => Assert.NotEqual('\0', c);

        [PulseMatrix]
        [PulseRow(true)]
        public void BoolArg(bool b) => Assert.True(b);

        [PulseMatrix]
        [PulseRow(new object?[] { null })]
        public void NullArg(string? s) => Assert.Null(s);
    }

    public sealed class MultiParamSuite
    {
        [PulseMatrix]
        [PulseRow(1, "two", true)]
        public void ThreeArgs(int a, string b, bool c)
        {
            Assert.Equal(1, a);
            Assert.Equal("two", b);
            Assert.True(c);
        }
    }

    public sealed class MixedOutcomeMatrixSuite
    {
        [PulseMatrix]
        [PulseRow(1)]
        [PulseRow(0)]
        [PulseRow(2)]
        public void Width_must_be_positive(int w) => Assert.True(w > 0, $"w={w}");
    }
}
