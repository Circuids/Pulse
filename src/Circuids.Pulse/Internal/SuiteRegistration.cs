using System.Diagnostics.CodeAnalysis;

namespace Circuids.Pulse.Internal;

/// <summary>Descriptor for a suite registered with <see cref="PulseBuilder"/>.</summary>
internal sealed class SuiteRegistration
{
    public SuiteRegistration(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] Type suiteType,
        Func<IServiceProvider, object>? factory)
    {
        SuiteType = suiteType;
        Factory = factory;
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type SuiteType { get; }

    /// <summary>Optional factory; when <see langword="null"/>, the suite is built via <c>ActivatorUtilities</c>.</summary>
    public Func<IServiceProvider, object>? Factory { get; }
}
