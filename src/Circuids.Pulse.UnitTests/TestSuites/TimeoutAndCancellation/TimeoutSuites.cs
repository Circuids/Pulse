namespace Circuids.Pulse.UnitTests.TestSuites.TimeoutAndCancellation;

internal sealed class TokenAcceptingSuite
{
    public static CancellationToken Received;

    [PulseCase]
    public void Accepts_token(CancellationToken ct) => Received = ct;
}

internal sealed class AttributeTimeoutSuite
{
    [PulseCase(TimeoutMs = 50)]
    public async Task Sleeps_too_long(CancellationToken ct)
        => await Task.Delay(TimeSpan.FromSeconds(5), ct);
}

internal sealed class InheritedTimeoutSuite
{
    [PulseCase]
    public async Task Sleeps_too_long(CancellationToken ct)
        => await Task.Delay(TimeSpan.FromSeconds(5), ct);
}

internal sealed class MatrixWithTokenSuite
{
    [PulseMatrix]
    [PulseRow(1)]
    [PulseRow(2)]
    public Task Row_with_token(int n, CancellationToken ct)
    {
        Assert.True(n > 0);
        Assert.True(ct.CanBeCanceled);
        return Task.CompletedTask;
    }
}

internal sealed class UncooperativeSuite
{
    [PulseCase(TimeoutMs = 50)]
    public void No_token_no_enforcement() { }
}
