namespace Circuids.Pulse.UnitTests.TestSuites.MatrixDetails;

internal sealed class PerRowSkipSuite
{
    [PulseMatrix]
    [PulseRow(1)]
    [PulseRow(99, Skip = "flaky platform")]
    [PulseRow(2)]
    public void Width(int w) => Assert.True(w > 0);
}

internal sealed class MatrixLevelSkipSuite
{
    [PulseMatrix(Skip = "temporarily disabled")]
    [PulseRow(1)]
    [PulseRow(2)]
    [PulseRow(3)]
    public void Width(int w) => throw new InvalidOperationException("must not run");
}

internal sealed class DisplayNameCaseSuite
{
    [PulseCase(DisplayName = "renders correctly on phone")]
    public void Method_name_should_be_replaced() { }
}

internal sealed class DisplayNameMatrixSuite
{
    [PulseMatrix(DisplayName = "viewport spec")]
    [PulseRow(390)]
    [PulseRow(768)]
    public void Method_name_replaced(int w) => Assert.True(w > 0);
}

internal sealed class ArgumentFormattingSuite
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

internal sealed class MultiParamSuite
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

internal sealed class MixedOutcomeMatrixSuite
{
    [PulseMatrix]
    [PulseRow(1)]
    [PulseRow(0)]
    [PulseRow(2)]
    public void Width_must_be_positive(int w) => Assert.True(w > 0, $"w={w}");
}
