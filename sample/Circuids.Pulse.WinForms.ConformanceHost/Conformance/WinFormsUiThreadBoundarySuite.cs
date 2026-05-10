namespace Circuids.Pulse.WinForms.ConformanceHost.Conformance;

public sealed class WinFormsUiThreadBoundarySuite
{
    private readonly WinFormsHostProbe _probe;

    public WinFormsUiThreadBoundarySuite(WinFormsHostProbe probe)
    {
        _probe = probe;
    }

    [PulseCase]
    public void Main_form_is_registered_as_the_host_control()
    {
        PulseAssert.NotNull(_probe.MainControl, "The WinForms host must register its main control for UI-thread checks.");
    }

    [PulseCase(TimeoutMs = 2000)]
    public async Task Control_InvokeAsync_runs_on_the_UI_thread(CancellationToken ct)
    {
        var control = _probe.MainControl ?? throw new InvalidOperationException("Main control is not registered.");
        var observedThreadId = await control.InvokeAsync(() => Environment.CurrentManagedThreadId, ct);

        PulseAssert.Equal(_probe.UiThreadId, observedThreadId, "Control.InvokeAsync must execute on the WinForms UI thread.");
    }

    [PulseMatrix(DisplayName = "Control.InvokeAsync value round-trip", TimeoutMs = 2000)]
    [PulseRow(1, 2)]
    [PulseRow(21, 42)]
    [PulseRow(-3, -6)]
    [PulseRow(0, 0)]
    public async Task Control_InvokeAsync_returns_delegate_value(int input, int expected, CancellationToken ct)
    {
        var control = _probe.MainControl ?? throw new InvalidOperationException("Main control is not registered.");
        var actual = await control.InvokeAsync(() => input * 2, ct);

        PulseAssert.Equal(expected, actual, "Control.InvokeAsync must marshal delegate return values.");
    }
}