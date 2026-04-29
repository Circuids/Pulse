namespace Circuids.Pulse.UnitTests.TestSuites.DiscoveryValidation;

internal sealed class BothAttributesSuite
{
    [PulseCase, PulseMatrix]
    [PulseRow(1)]
    public void Both(int x) { }
}

internal sealed class CaseWithParameters
{
    [PulseCase] public void Has_args(int x) { }
}

internal sealed class MatrixWithoutRows
{
    [PulseMatrix] public void No_rows(int x) { }
}

internal sealed class RowArgCountMismatch
{
    [PulseMatrix]
    [PulseRow(1, 2, 3)]
    public void One_arg_method(int x) { }
}

internal sealed class NonVoidReturn
{
    [PulseCase] public int Returns_int() => 42;
}

internal sealed class DisposableInvalidSuite : IDisposable
{
    public static int DisposeCount;

    public void Dispose() => DisposeCount++;

    [PulseCase] public int Returns_int() => 42;
}
