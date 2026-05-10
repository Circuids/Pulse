using Circuids.Pulse.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Circuids.Pulse.Extensions;

/// <summary>DI extensions that register the Pulse runtime test executor with the host app.</summary>
public static class PulseServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITestExecutor"/> and the Pulse builder configuration with the host's
    /// <see cref="IServiceCollection"/>. The executor is registered as <see cref="ServiceLifetime.Scoped"/>;
    /// the captured <see cref="RuntimeEnvironment"/> is registered as a singleton so consumer
    /// suites can inject it directly instead of calling <c>RuntimeInformation</c> themselves.
    /// </summary>
    public static IServiceCollection AddPulse(
        this IServiceCollection services,
        Action<PulseBuilder> configure)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var builder = new PulseBuilder(services);
        configure(builder);

        services.TryAddSingleton(builder);
        services.TryAddSingleton(RuntimeEnvironmentProbe.Capture());
        services.TryAddSingleton<PulseRunCoordinator>();
        services.TryAddScoped<ITestExecutor, TestExecutor>();
        return services;
    }
}
