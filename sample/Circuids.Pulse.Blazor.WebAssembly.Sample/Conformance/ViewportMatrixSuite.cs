namespace Circuids.Pulse.Blazor.WebAssembly.Sample.Conformance;

/// <summary>
/// Demonstrates <see cref="PulseMatrixAttribute"/> + <see cref="PulseRowAttribute"/>: one method,
/// many parameter rows, each reported as an independent test result. A natural fit for sweeping
/// across viewport sizes, locale codes, theme variants, etc.
/// </summary>
public sealed class ViewportMatrixSuite
{
    [PulseMatrix(DisplayName = "Viewport width is in supported range")]
    [PulseRow(360)]   // small phone
    [PulseRow(390)]   // iPhone 15
    [PulseRow(768)]   // tablet portrait
    [PulseRow(1024)]  // tablet landscape / small laptop
    [PulseRow(1440)]  // laptop
    [PulseRow(1920)]  // desktop
    public void Viewport_width_is_within_supported_range(int width)
    {
        PulseAssert.True(width is >= 320 and <= 4096,
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
