namespace Circuids.Pulse.Maui.Sample.Conformance;

public sealed class MauiDispatcherBoundarySuite
{
    [PulseCase(TimeoutMs = 2000)]
    public async Task MainThread_dispatch_runs_callback_on_UI_thread(CancellationToken ct)
    {
        var observed = await MainThread.InvokeOnMainThreadAsync(() => MainThread.IsMainThread);
        ct.ThrowIfCancellationRequested();

        PulseAssert.True(observed, "MainThread dispatch must execute callbacks on the UI thread.");
    }

    [PulseCase(TimeoutMs = 2000)]
    public async Task MainThread_dispatch_returns_delegate_value(CancellationToken ct)
    {
        var result = await MainThread.InvokeOnMainThreadAsync(() => 21 * 2);
        ct.ThrowIfCancellationRequested();

        PulseAssert.Equal(42, result, "MainThread dispatch must marshal delegate return values.");
    }

    [PulseMatrix(DisplayName = "MainThread dispatch value round-trip", TimeoutMs = 2000)]
    [PulseRow(1, 2)]
    [PulseRow(21, 42)]
    [PulseRow(-3, -6)]
    [PulseRow(0, 0)]
    public async Task MainThread_dispatch_returns_matrix_value(int input, int expected, CancellationToken ct)
    {
        var actual = await MainThread.InvokeOnMainThreadAsync(() => input * 2);
        ct.ThrowIfCancellationRequested();

        PulseAssert.Equal(expected, actual, "MainThread dispatch must preserve matrix row return values.");
    }
}