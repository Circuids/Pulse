namespace Circuids.Pulse.UnitTests.TestSuites.EdgeCases;

internal sealed class PassSuite
{
    [PulseCase] public void Ok() { }
}

internal sealed class CancelTriggerSuite
{
    private readonly CancellationTokenSource _cts;

    public CancelTriggerSuite(CancellationTokenSource cts)
    {
        _cts = cts;
    }

    [PulseCase]
    public void Cancels_after_running() => _cts.Cancel();
}

internal sealed class SecondSuite
{
    public static bool WasInvoked;

    [PulseCase] public void Should_not_run() => WasInvoked = true;
}

internal sealed class TwoMatrixMethodsSuite
{
    [PulseMatrix]
    [PulseRow(1)]
    [PulseRow(2)]
    [PulseRow(3)]
    public void Width(int w) => Assert.True(w > 0);

    [PulseMatrix]
    [PulseRow(10)]
    [PulseRow(20)]
    [PulseRow(30)]
    public void Height(int h) => Assert.True(h > 0);
}

internal sealed class SyncValueTaskSuite
{
    [PulseCase]
    public ValueTask Sync_complete() => ValueTask.CompletedTask;
}

internal sealed class DeclarativeSkipSuite
{
    [PulseCase(Skip = "not yet")] public void S() { }
}

internal sealed class CaseWithStrayRowSuite
{
    [PulseCase]
    [PulseRow(1)]
    public void Plain_case() { }
}

internal sealed class EmptySkipReasonSuite
{
    [PulseCase(Skip = "")] public void Empty_skip() { }
}

internal interface IMarker
{
    string Value { get; }
}

internal sealed class Marker : IMarker
{
    public Marker(string value)
    {
        Value = value;
    }

    public string Value { get; }
}

internal sealed class MarkerSuite
{
    private readonly IMarker _marker;

    public MarkerSuite(IMarker marker)
    {
        _marker = marker;
    }

    [PulseCase] public void Has_marker() => Assert.Equal("marker-value", _marker.Value);
}
