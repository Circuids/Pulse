namespace Circuids.Pulse.UnitTests.TestSuites.Execution;

internal sealed class PassingSuite
{
    [PulseCase] public void Always_passes() { }
}

internal sealed class FailingSuite
{
    [PulseCase] public void Always_fails() => throw new InvalidOperationException("boom");
}

internal sealed class DeclarativelySkippedSuite
{
    public static bool WasInvoked;

    [PulseCase(Skip = "not yet")]
    public void Skipped_test() => WasInvoked = true;
}

internal sealed class RuntimeSkippedSuite
{
    [PulseCase] public void Skip_in_body() => throw new PulseSkipException("dynamic skip");
}

internal sealed class AsyncSuite
{
    [PulseCase]
    public async Task Async_fails()
    {
        await Task.Yield();
        throw new InvalidOperationException("async-fail");
    }
}

internal sealed class MatrixSuite
{
    [PulseMatrix]
    [PulseRow(390)]
    [PulseRow(768)]
    [PulseRow(1920)]
    public void Width_is_positive(int width) => Assert.True(width > 0);
}
