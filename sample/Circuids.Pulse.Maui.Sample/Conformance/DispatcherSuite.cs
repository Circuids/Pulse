namespace Circuids.Pulse.Maui.Sample.Conformance;

/// <summary>
/// Exercises MAUI's real <see cref="MainThread"/> dispatcher. Pulse hosts in-process inside the
/// running app, so <c>MainThread</c> is the same dispatcher the rest of the UI uses — no fakes.
/// </summary>
public sealed class DispatcherSuite
{
    [PulseCase]
    public void MainThread_API_is_initialized()
    {
        PulseAssert.True(
            MainThread.IsMainThread || !MainThread.IsMainThread,
            "MainThread.IsMainThread must be callable (initialized).");
    }

    [PulseCase]
    public async Task InvokeOnMainThreadAsync_runs_callback_on_the_UI_thread(CancellationToken ct)
    {
        var observed = false;
        await MainThread.InvokeOnMainThreadAsync(() => observed = MainThread.IsMainThread);
        ct.ThrowIfCancellationRequested();

        PulseAssert.True(observed, "Inside InvokeOnMainThreadAsync the callback must observe IsMainThread == true.");
    }

    [PulseCase(TimeoutMs = 2000)]
    public async Task Dispatcher_returns_value_from_async_delegate(CancellationToken ct)
    {
        var sum = await MainThread.InvokeOnMainThreadAsync(() => 1 + 2);
        ct.ThrowIfCancellationRequested();

        PulseAssert.Equal(3, sum, "Async delegate must marshal its result back through the dispatcher.");
    }
}
