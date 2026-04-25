namespace Circuids.Pulse.Internal;

/// <summary>Descriptor for a suite registered with <see cref="PulseBuilder"/>.</summary>
internal sealed class SuiteRegistration
{
    public SuiteRegistration(Type suiteType, Func<IServiceProvider, object>? factory)
    {
        SuiteType = suiteType;
        Factory = factory;
    }

    public Type SuiteType { get; }

    /// <summary>Optional factory; when <see langword="null"/>, the suite is built via <c>ActivatorUtilities</c>.</summary>
    public Func<IServiceProvider, object>? Factory { get; }
}
