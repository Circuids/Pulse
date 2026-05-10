namespace Circuids.Pulse.WPF.ConformanceHost.Conformance;

public sealed class WpfDispatcherBoundarySuite
{
    private readonly WpfHostProbe _probe;

    public WpfDispatcherBoundarySuite(WpfHostProbe probe)
    {
        _probe = probe;
    }

    [PulseCase]
    public void Main_window_is_registered_as_dispatch_target()
    {
        PulseAssert.NotNull(_probe.MainWindow, "The WPF host must register its main window for dispatcher checks.");
    }

    [PulseCase(TimeoutMs = 2000)]
    public async Task Dispatcher_runs_callback_on_UI_thread(CancellationToken ct)
    {
        var dispatcher = _probe.Dispatcher ?? throw new InvalidOperationException("Dispatcher is not registered.");
        var operation = dispatcher.InvokeAsync(() => Environment.CurrentManagedThreadId);
        var observedThreadId = await operation.Task.WaitAsync(ct);

        PulseAssert.Equal(_probe.UiThreadId, observedThreadId, "Dispatcher must execute callbacks on the WPF UI thread.");
    }

    [PulseMatrix(DisplayName = "Dispatcher value round-trip", TimeoutMs = 2000)]
    [PulseRow(1, 2)]
    [PulseRow(21, 42)]
    [PulseRow(-3, -6)]
    [PulseRow(0, 0)]
    public async Task Dispatcher_returns_matrix_value(int input, int expected, CancellationToken ct)
    {
        var dispatcher = _probe.Dispatcher ?? throw new InvalidOperationException("Dispatcher is not registered.");
        var operation = dispatcher.InvokeAsync(() => input * 2);
        var actual = await operation.Task.WaitAsync(ct);

        PulseAssert.Equal(expected, actual, "Dispatcher must marshal delegate return values.");
    }
}