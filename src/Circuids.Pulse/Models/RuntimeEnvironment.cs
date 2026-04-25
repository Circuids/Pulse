namespace Circuids.Pulse;

/// <summary>
/// Auto-detected ground-truth facts about the runtime in which a test run executed.
/// Pulse populates this once per <see cref="ITestExecutor.RunAsync(System.Threading.CancellationToken)"/>
/// call. Consumers cannot override it.
/// </summary>
public sealed record RuntimeEnvironment
{
    /// <summary>e.g. <c>.NET 10.0.2</c>.</summary>
    public required string FrameworkDescription { get; init; }

    /// <summary>e.g. <c>browser-wasm</c>, <c>win-x64</c>, <c>android-arm64</c>.</summary>
    public required string RuntimeIdentifier { get; init; }

    /// <summary>e.g. <c>Microsoft Windows 10.0.26100</c>.</summary>
    public required string OSDescription { get; init; }

    /// <summary>The OS architecture (e.g. <c>X64</c>, <c>Arm64</c>, <c>Wasm</c>).</summary>
    public required string OSArchitecture { get; init; }

    /// <summary>The current process architecture (e.g. <c>X64</c>, <c>Arm64</c>, <c>Wasm</c>).</summary>
    public required string ProcessArchitecture { get; init; }

    /// <summary><see langword="true"/> when the runtime is a browser host (WASM in a browser tab).</summary>
    public required bool IsBrowser { get; init; }

    /// <summary><see langword="true"/> when the runtime is WebAssembly (browser or WASI).</summary>
    public required bool IsWasm { get; init; }

    /// <summary>The machine name as reported by <see cref="System.Environment.MachineName"/>.</summary>
    public required string MachineName { get; init; }

    /// <summary>The processor count as reported by <see cref="System.Environment.ProcessorCount"/>.</summary>
    public required int ProcessorCount { get; init; }
}
