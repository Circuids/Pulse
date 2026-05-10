using System.Runtime.InteropServices;
using Circuids.Pulse.TestSupport.Runtime;

namespace Circuids.Pulse.Blazor.WebAssembly.Sample.Conformance;

public sealed class BrowserRuntimeBoundarySuite : RuntimeHostSpec
{
    private readonly RuntimeEnvironment _environment;

    public BrowserRuntimeBoundarySuite(RuntimeEnvironment environment)
    {
        _environment = environment;
    }

    protected override bool ExpectedIsBrowser => true;

    protected override Architecture? ExpectedProcessArchitecture => Architecture.Wasm;

    protected override string ExpectedRuntimeIdentifierSubstring => "browser";

    [PulseCase]
    public void Browser_flag_matches_host_expectation() => Browser_flag_matches_host_expectation_core();

    [PulseCase]
    public void Process_architecture_matches_host_expectation() => Process_architecture_matches_host_expectation_core();

    [PulseCase]
    public void Runtime_identifier_contains_expected_host_substring() => Runtime_identifier_contains_expected_host_substring_core();

    [PulseCase]
    public void Framework_description_starts_with_dotnet() => Framework_description_starts_with_dotnet_core();

    [PulseCase]
    public void Pulse_runtime_environment_matches_browser_host()
    {
        PulseAssert.True(_environment.IsBrowser, "Pulse RuntimeEnvironment must identify the browser host.");
        PulseAssert.True(_environment.IsWasm, "Pulse RuntimeEnvironment must identify the WebAssembly runtime.");
        PulseAssert.Equal(RuntimeInformation.ProcessArchitecture.ToString(), _environment.ProcessArchitecture);
    }
}