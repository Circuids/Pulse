using System.Runtime.InteropServices;

namespace Circuids.Pulse.Internal;

/// <summary>Builds a <see cref="RuntimeEnvironment"/> snapshot from BCL APIs.</summary>
internal static class RuntimeEnvironmentProbe
{
    public static RuntimeEnvironment Capture()
    {
        return new RuntimeEnvironment
        {
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
            OSDescription = RuntimeInformation.OSDescription,
            OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            IsBrowser = OperatingSystem.IsBrowser(),
            IsWasm = IsWasmRuntime(),
            MachineName = SafeMachineName(),
            ProcessorCount = Environment.ProcessorCount,
        };
    }

    private static bool IsWasmRuntime()
    {
        if (OperatingSystem.IsBrowser()) return true;

        // OperatingSystem.IsWasi exists on net8+ but is gated by [SupportedOSPlatformGuard].
        // Fall back to RID inspection for older targets / non-browser WASM.
        var rid = RuntimeInformation.RuntimeIdentifier;
        return rid.Contains("wasm", StringComparison.OrdinalIgnoreCase)
            || rid.StartsWith("browser", StringComparison.OrdinalIgnoreCase)
            || rid.StartsWith("wasi", StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeMachineName()
    {
        try { return Environment.MachineName; }
        catch { return "(unknown)"; }
    }
}
