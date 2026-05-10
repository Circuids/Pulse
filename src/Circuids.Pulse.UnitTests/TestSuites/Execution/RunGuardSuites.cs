namespace Circuids.Pulse.UnitTests.TestSuites.Execution;

internal sealed class LongRunningSuite
{
    private static TaskCompletionSource _started = CreateCompletionSource();
    private static TaskCompletionSource _release = CreateCompletionSource();

    public static TaskCompletionSource Started => _started;

    public static void Reset()
    {
        _started = CreateCompletionSource();
        _release = CreateCompletionSource();
    }

    public static void Release() => _release.TrySetResult();

    [PulseCase]
    public async Task Waits_until_released()
    {
        _started.TrySetResult();
        await _release.Task;
    }

    private static TaskCompletionSource CreateCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal sealed class CancellableLongRunningSuite
{
    private static TaskCompletionSource _started = CreateCompletionSource();

    public static TaskCompletionSource Started => _started;

    public static void Reset() => _started = CreateCompletionSource();

    [PulseCase]
    public async Task Waits_until_cancelled(CancellationToken cancellationToken)
    {
        _started.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private static TaskCompletionSource CreateCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal sealed class ConstructionFailingSuite
{
    public ConstructionFailingSuite() => throw new InvalidOperationException("construct-boom");

    [PulseCase] public void Never_runs() { }
}
