using Circuids.Pulse.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Circuids.Pulse;

/// <summary>
/// Configuration surface for Pulse, supplied to
/// <see cref="Extensions.PulseServiceCollectionExtensions.AddPulse"/>.
/// </summary>
public sealed class PulseBuilder
{
    private readonly List<SuiteRegistration> _suites = new();

    /// <summary>
    /// Initializes a new <see cref="PulseBuilder"/>. Consumers normally do not construct this
    /// directly; Pulse's DI registration creates it and passes it to the configuration delegate.
    /// </summary>
    public PulseBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>The service collection Pulse is registering into.</summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Optional freeform label written verbatim to <see cref="TestRunReport.AssignedPlatform"/>.
    /// Pulse does not interpret this value. If left <see langword="null"/>, the report carries
    /// the literal string <c>"(unassigned)"</c>.
    /// </summary>
    public string? AssignedPlatform { get; set; }

    /// <summary>
    /// Default cooperative timeout applied to tests that don't set
    /// <see cref="PulseCaseAttribute.TimeoutMs"/> or <see cref="PulseMatrixAttribute.TimeoutMs"/>.
    /// <see langword="null"/> (the default) means "no clock". Enforcement requires the test
    /// method to accept a trailing <see cref="CancellationToken"/> parameter and honor it.
    /// </summary>
    public TimeSpan? DefaultTestTimeout { get; set; }

    /// <summary>
    /// Registers a suite type that Pulse will resolve from the host's
    /// <see cref="IServiceProvider"/> (via <see cref="ActivatorUtilities"/>) at run time.
    /// </summary>
    public PulseBuilder AddSuite<TSuite>() where TSuite : class
    {
        _suites.Add(new SuiteRegistration(typeof(TSuite), null));
        return this;
    }

    /// <summary>
    /// Registers a suite type with an explicit factory. Use this when the suite has dependencies
    /// that aren't (or shouldn't be) registered in the host's <see cref="IServiceProvider"/>.
    /// </summary>
    public PulseBuilder AddSuite<TSuite>(Func<IServiceProvider, TSuite> factory) where TSuite : class
    {
        if (factory is null) throw new ArgumentNullException(nameof(factory));
        _suites.Add(new SuiteRegistration(typeof(TSuite), sp => factory(sp)));
        return this;
    }

    /// <summary>
    /// Registers a suite by <see cref="Type"/> with an explicit factory. The factory must return
    /// an instance assignable to <paramref name="suiteType"/>.
    /// </summary>
    public PulseBuilder AddSuite(Type suiteType, Func<IServiceProvider, object> factory)
    {
        if (suiteType is null) throw new ArgumentNullException(nameof(suiteType));
        if (factory is null) throw new ArgumentNullException(nameof(factory));
        _suites.Add(new SuiteRegistration(suiteType, factory));
        return this;
    }

    internal IReadOnlyList<SuiteRegistration> Suites => _suites;
}
