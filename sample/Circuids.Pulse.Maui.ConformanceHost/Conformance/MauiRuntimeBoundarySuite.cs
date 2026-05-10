using System.Runtime.InteropServices;
using Circuids.Pulse.TestSupport.Runtime;

namespace Circuids.Pulse.Maui.Sample.Conformance;

public sealed class MauiRuntimeBoundarySuite : RuntimeHostSpec
{
    private readonly RuntimeEnvironment _environment;

    public MauiRuntimeBoundarySuite(RuntimeEnvironment environment)
    {
        _environment = environment;
    }

    protected override bool ExpectedIsBrowser => false;

    protected override Architecture? DisallowedProcessArchitecture => Architecture.Wasm;

    protected override string ExpectedRuntimeIdentifierSubstring =>
#if ANDROID
        "android";
#elif IOS
        "ios";
#elif MACCATALYST
        "maccatalyst";
#elif WINDOWS
        "win";
#else
        "unknown";
#endif

    [PulseCase]
    public void Browser_flag_matches_host_expectation() => Browser_flag_matches_host_expectation_core();

    [PulseCase]
    public void Process_architecture_matches_host_expectation() => Process_architecture_matches_host_expectation_core();

    [PulseCase]
    public void Runtime_identifier_contains_expected_host_substring() => Runtime_identifier_contains_expected_host_substring_core();

    [PulseCase]
    public void Framework_description_starts_with_dotnet() => Framework_description_starts_with_dotnet_core();

    [PulseCase]
    public void Pulse_runtime_environment_matches_native_host()
    {
        PulseAssert.False(_environment.IsBrowser, "MAUI must not report a browser host.");
        PulseAssert.False(_environment.IsWasm, "MAUI must not report a WebAssembly runtime.");
        PulseAssert.Equal(RuntimeInformation.ProcessArchitecture.ToString(), _environment.ProcessArchitecture);
    }
}