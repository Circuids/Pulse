using System.Runtime.InteropServices;

namespace Circuids.Pulse.Maui.Sample.Conformance;

/// <summary>
/// Concrete <see cref="RuntimeHostSpec"/> for .NET MAUI. The expected RID substring switches per
/// target platform via the MAUI build symbols (<c>ANDROID</c>, <c>IOS</c>, <c>MACCATALYST</c>,
/// <c>WINDOWS</c>) so the same suite works across every head this binary is shipped to.
/// </summary>
public sealed class MauiHostSuite : RuntimeHostSpec
{
    protected override bool ExpectedIsBrowser => false;

    // MAUI runs natively on the host CPU; we accept whatever RuntimeInformation reports,
    // and the inherited assertion proves it's the non-Wasm path.
    protected override Architecture ExpectedProcessArchitecture => RuntimeInformation.ProcessArchitecture;

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
        "";
#endif

    [PulseCase]
    public void Process_architecture_is_not_wasm()
    {
        PulseAssert.NotEqual(
            Architecture.Wasm,
            RuntimeInformation.ProcessArchitecture,
            "MAUI must never report Wasm as ProcessArchitecture.");
    }

    [PulseCase]
    public void OperatingSystem_matches_one_of_the_supported_MAUI_targets()
    {
        var supported =
            OperatingSystem.IsAndroid()
            || OperatingSystem.IsIOS()
            || OperatingSystem.IsMacCatalyst()
            || OperatingSystem.IsWindows();

        PulseAssert.True(
            supported,
            "Expected one of: Android, iOS, MacCatalyst, Windows.");
    }
}
