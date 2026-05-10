namespace Circuids.Pulse.UnitTests.TestSuites.OutcomeAndDuration;

internal sealed class ValueTaskSuite
{
    [PulseCase]
    public async ValueTask Vt_passes()
    {
        await Task.Yield();
    }

    [PulseCase]
    public ValueTask Vt_fails() =>
        ValueTask.FromException(new InvalidOperationException("vt-fail"));
}

internal sealed class SyncFailureSuite
{
    [PulseCase]
    public void Throws_synchronously() => throw new InvalidOperationException("real-message");
}

internal sealed class SlowSuite
{
    [PulseCase]
    public async Task Sleeps_briefly() => await Task.Delay(30);
}

internal sealed class DeclarativeSkipSuite
{
    public static int Counter;

    [PulseCase(Skip = "wip")]
    public void Should_not_run() => Counter++;
}

internal sealed class PassSuite
{
    [PulseCase] public void Ok() { }
}

internal sealed class FailSuite
{
    [PulseCase] public void Bad() => throw new Exception("nope");
}

internal sealed class SkipSuite
{
    [PulseCase] public void S() => throw new PulseSkipException("nope");
}

internal sealed class TypeMismatchSuite
{
    [PulseMatrix]
    [PulseRow("not-an-int")]
    public void Expects_int(int n) => Assert.True(n > 0);
}
