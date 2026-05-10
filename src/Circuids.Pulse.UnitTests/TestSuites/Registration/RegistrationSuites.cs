namespace Circuids.Pulse.UnitTests.TestSuites.Registration;

internal sealed class EmptySuite
{
    [PulseCase] public void NoOp() { }
}

internal sealed class SuiteA
{
    [PulseCase] public void A() { }
}

internal sealed class SuiteB
{
    [PulseCase] public void B() { }
}

internal sealed class SuiteC
{
    [PulseCase] public void C() { }
}

internal sealed class ThrowsInCtorSuite
{
    public ThrowsInCtorSuite() => throw new InvalidOperationException("ctor boom");

    [PulseCase] public void NoOp() { }
}
