using System.Runtime.InteropServices;
using Circuids.Pulse.TestSupport.Runtime;

namespace Circuids.Pulse.WinForms.ConformanceHost.Conformance;

public sealed class WinFormsRuntimeBoundarySuite : RuntimeHostSpec
{
    private readonly RuntimeEnvironment _environment;

    public WinFormsRuntimeBoundarySuite(RuntimeEnvironment environment)
    {
        _environment = environment;
    }

    protected override bool ExpectedIsBrowser => false;

    protected override Architecture? DisallowedProcessArchitecture => Architecture.Wasm;

    protected override string ExpectedRuntimeIdentifierSubstring => "win";

    [PulseCase]
    public void Browser_flag_matches_host_expectation() => Browser_flag_matches_host_expectation_core();

    [PulseCase]
    public void Process_architecture_matches_host_expectation() => Process_architecture_matches_host_expectation_core();

    [PulseCase]
    public void Runtime_identifier_contains_expected_host_substring() => Runtime_identifier_contains_expected_host_substring_core();

    [PulseCase]
    public void Framework_description_starts_with_dotnet() => Framework_description_starts_with_dotnet_core();

    [PulseCase]
    public void Pulse_runtime_environment_matches_desktop_host()
    {
        PulseAssert.False(_environment.IsBrowser, "WinForms must not report a browser host.");
        PulseAssert.False(_environment.IsWasm, "WinForms must not report a WebAssembly runtime.");
        PulseAssert.Equal(RuntimeInformation.ProcessArchitecture.ToString(), _environment.ProcessArchitecture);
    }
}