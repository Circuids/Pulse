namespace Circuids.Pulse.Maui.Sample.Conformance;

public sealed class MauiDeviceBoundarySuite
{
    [PulseCase]
    public void Device_platform_is_supported_by_this_sample()
    {
        var platform = DeviceInfo.Current.Platform;
        var supported = platform == DevicePlatform.Android
            || platform == DevicePlatform.iOS
            || platform == DevicePlatform.MacCatalyst
            || platform == DevicePlatform.WinUI;

        PulseAssert.True(supported, $"Unsupported device platform '{platform}'.");
    }

    [PulseCase]
    public void AppInfo_exposes_application_identity()
    {
        PulseAssert.False(string.IsNullOrWhiteSpace(AppInfo.Current.Name), "AppInfo must expose the running app name.");
        PulseAssert.False(string.IsNullOrWhiteSpace(AppInfo.Current.PackageName), "AppInfo must expose the running package name.");
    }

    [PulseMatrix(DisplayName = "current MAUI device facts")]
    [PulseRow("idiom")]
    [PulseRow("device-type")]
    public void Device_fact_is_reported_by_current_host(string factName)
    {
        var actual = factName switch
        {
            "idiom" => DeviceInfo.Current.Idiom.ToString(),
            "device-type" => DeviceInfo.Current.DeviceType.ToString(),
            _ => throw new InvalidOperationException($"Unknown device fact '{factName}'."),
        };

        PulseAssert.False(string.IsNullOrWhiteSpace(actual), $"DeviceInfo.Current must report '{factName}' in the live MAUI host.");
    }
}