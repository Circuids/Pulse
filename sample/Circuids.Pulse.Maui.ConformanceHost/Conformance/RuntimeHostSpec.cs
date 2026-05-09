using System.Runtime.InteropServices;

namespace Circuids.Pulse.Maui.Sample.Conformance;

/// <summary>
/// Shared "what does the runtime host look like?" spec. In a real codebase this lives in a
/// platform-neutral contracts assembly (e.g. <c>Circuids.Bridge.Conformance</c>); each concrete
/// platform sample inherits and pins down the values it expects to observe at runtime. The
/// duplicated copy here keeps the sample self-contained.
/// </summary>
public abstract class RuntimeHostSpec
{
    /// <summary>What the platform expects <see cref="OperatingSystem.IsBrowser"/> to return.</summary>
    protected abstract bool ExpectedIsBrowser { get; }

    /// <summary>What the platform expects the process architecture to be.</summary>
    protected abstract Architecture ExpectedProcessArchitecture { get; }

    /// <summary>Substring the runtime identifier must contain (RIDs vary across SDKs).</summary>
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
