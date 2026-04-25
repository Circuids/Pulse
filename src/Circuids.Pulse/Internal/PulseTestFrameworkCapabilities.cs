using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace Circuids.Pulse.Internal;

/// <summary>No-op MTP capabilities; Pulse declares none today.</summary>
internal sealed class PulseTestFrameworkCapabilities : ITestFrameworkCapabilities
{
    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities { get; } =
        Array.Empty<ITestFrameworkCapability>();
}
