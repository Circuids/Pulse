namespace Circuids.Pulse.UnitTests.TestSuites.RuntimeEnvironmentDi;

internal sealed class InjectingSuite
{
    public static Circuids.Pulse.RuntimeEnvironment? Captured;

    private readonly Circuids.Pulse.RuntimeEnvironment _environment;

    public InjectingSuite(Circuids.Pulse.RuntimeEnvironment environment)
    {
        _environment = environment;
    }

    [PulseCase]
    public void Captures_environment() => Captured = _environment;
}
