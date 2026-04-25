namespace Circuids.Pulse.Maui.Sample.Conformance;

/// <summary>
/// One method, many parameter rows. Each row is reported as an independent test result.
/// Mirrors the Blazor sample's matrix so the same pattern is visible on both platforms.
/// </summary>
public sealed class ViewportMatrixSuite
{
    [PulseMatrix(DisplayName = "Window width is in supported range")]
    [PulseRow(360)]
    [PulseRow(390)]
    [PulseRow(768)]
    [PulseRow(1024)]
    [PulseRow(1440)]
    [PulseRow(1920)]
    public void Window_width_is_within_supported_range(int width)
    {
        PulseAssert.True(
            width is >= 320 and <= 4096,
            $"Width {width} must be within the supported responsive range [320, 4096].");
    }

    [PulseMatrix(DisplayName = "Aspect ratio classification")]
    [PulseRow(390, 844, "portrait")]
    [PulseRow(1920, 1080, "landscape")]
    [PulseRow(768, 768, "square")]
    public void Aspect_ratio_is_classified(int width, int height, string expected)
    {
        var actual = width > height ? "landscape"
                  : width < height ? "portrait"
                  : "square";
        PulseAssert.Equal(expected, actual, $"Classification for {width}x{height}.");
    }
}
