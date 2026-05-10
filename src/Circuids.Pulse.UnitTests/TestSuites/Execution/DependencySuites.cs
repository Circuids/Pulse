namespace Circuids.Pulse.UnitTests.TestSuites.Execution;

internal interface ICounter
{
    int Value { get; }

    void Increment();
}

internal sealed class Counter : ICounter
{
    public int Value { get; private set; }

    public void Increment() => Value++;
}

internal sealed class DependencySuite
{
    private readonly ICounter _counter;

    public DependencySuite(ICounter counter)
    {
        _counter = counter;
    }

    [PulseCase]
    public void Counts()
    {
        _counter.Increment();
        Assert.Equal(1, _counter.Value);
    }
}
