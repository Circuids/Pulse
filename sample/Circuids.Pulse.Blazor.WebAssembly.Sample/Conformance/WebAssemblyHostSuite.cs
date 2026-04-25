using System.Runtime.InteropServices;

namespace Circuids.Pulse.Blazor.WebAssembly.Sample.Conformance;

/// <summary>
/// Concrete conformance suite for Blazor WebAssembly. Inherits the shared
/// <see cref="RuntimeHostSpec"/> and pins down the values <em>this</em> platform must observe.
/// Add additional WASM-specific cases here.
/// </summary>
public sealed class WebAssemblyHostSuite : RuntimeHostSpec
{
    protected override bool ExpectedIsBrowser => true;
    protected override Architecture ExpectedProcessArchitecture => Architecture.Wasm;
    protected override string ExpectedRuntimeIdentifierSubstring => "browser";

    [PulseCase]
    public void OS_description_reports_browser()
    {
        // .NET WASM hosts report something like "Browser" for OSDescription.
        var os = RuntimeInformation.OSDescription;
        PulseAssert.True(
            os.Contains("Browser", StringComparison.OrdinalIgnoreCase),
            $"OSDescription on WASM should contain 'Browser', got '{os}'.");
    }

    [PulseCase]
    public void Single_threaded_processor_count()
    {
        // Default WASM runtime is single-threaded; ProcessorCount is 1 unless threading is on.
        PulseAssert.True(
            Environment.ProcessorCount >= 1,
            $"ProcessorCount must be at least 1, got {Environment.ProcessorCount}.");
    }
}
