using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace Circuids.Pulse.Internal;

/// <summary>
/// Empty capability set for <see cref="PulseTestFramework"/>. Pulse v0.1 advertises no optional
/// capabilities — extensions that depend on capability checks must opt out gracefully (per the
/// MTP contract). Capabilities will be filled in as Pulse grows (e.g. trx report, banner owner).
/// </summary>
internal sealed class PulseTestFrameworkCapabilities : ITestFrameworkCapabilities
{
    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities { get; } =
        Array.Empty<ITestFrameworkCapability>();
}
