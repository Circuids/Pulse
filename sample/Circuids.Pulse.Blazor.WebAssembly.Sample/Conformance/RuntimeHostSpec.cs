using System.Runtime.InteropServices;

namespace Circuids.Pulse.Blazor.WebAssembly.Sample.Conformance;

/// <summary>
/// The abstract conformance spec for "what does the runtime host look like?". This base lives
/// in shared code in real projects (e.g. a separate <c>Circuids.Bridge.Conformance</c> RCL);
/// each concrete platform — Blazor WASM, MAUI, WPF — inherits and asserts the values it expects
/// to see when this binary is running on that platform.
/// <para>
/// This is the load-bearing pattern Pulse v0.1 was designed for: <em>one spec, many hosts</em>.
/// </para>
/// </summary>
public abstract class RuntimeHostSpec
{
    /// <summary>What the concrete platform expects <see cref="OperatingSystem.IsBrowser"/> to return.</summary>
    protected abstract bool ExpectedIsBrowser { get; }

    /// <summary>What the concrete platform expects the process architecture to be.</summary>
    protected abstract Architecture ExpectedProcessArchitecture { get; }

    /// <summary>
    /// What substring the runtime identifier must contain. Pulse spec assertions deliberately
    /// avoid full equality on RIDs because version suffixes (e.g. <c>browser-wasm</c> vs
    /// <c>browser-wasm-net10.0</c>) drift across SDKs.
    /// </summary>
    protected abstract string ExpectedRuntimeIdentifierSubstring { get; }

    [PulseCase]
    public virtual void Host_browser_flag_matches_expectation()
    {
        PulseAssert.Equal(
            ExpectedIsBrowser,
            OperatingSystem.IsBrowser(),
            "OperatingSystem.IsBrowser() must agree with the platform's expectation.");
    }

    [PulseCase]
    public virtual void Host_process_architecture_matches_expectation()
    {
        PulseAssert.Equal(
            ExpectedProcessArchitecture,
            RuntimeInformation.ProcessArchitecture,
            "RuntimeInformation.ProcessArchitecture must match the platform.");
    }

    [PulseCase]
    public virtual void Host_runtime_identifier_contains_expected_substring()
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        PulseAssert.True(
            rid.Contains(ExpectedRuntimeIdentifierSubstring, StringComparison.OrdinalIgnoreCase),
            $"RID '{rid}' must contain '{ExpectedRuntimeIdentifierSubstring}'.");
    }

    [PulseCase]
    public virtual void Framework_description_starts_with_dotnet()
    {
        var desc = RuntimeInformation.FrameworkDescription;
        PulseAssert.True(
            desc.StartsWith(".NET", StringComparison.OrdinalIgnoreCase),
            $"FrameworkDescription '{desc}' should start with '.NET'.");
    }
}
