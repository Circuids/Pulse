using Microsoft.JSInterop;

namespace Circuids.Pulse.Blazor.WebAssembly.Sample.Conformance;

public sealed class BrowserInteropBoundarySuite
{
    private readonly IJSRuntime _js;

    public BrowserInteropBoundarySuite(IJSRuntime js)
    {
        _js = js;
    }

    [PulseCase(TimeoutMs = 2000)]
    public async Task Browser_user_agent_is_available(CancellationToken ct)
    {
        await using var module = await ImportModuleAsync(ct);
        var userAgent = await module.InvokeAsync<string>("getUserAgent", ct);

        PulseAssert.False(string.IsNullOrWhiteSpace(userAgent), "The live browser must expose a user agent string.");
    }

    [PulseCase(TimeoutMs = 2000)]
    public async Task Browser_viewport_dimensions_are_available(CancellationToken ct)
    {
        await using var module = await ImportModuleAsync(ct);
        var viewport = await module.InvokeAsync<ViewportSnapshot>("getViewport", ct);

        PulseAssert.True(viewport.Width > 0, $"Viewport width must be positive, got {viewport.Width}.");
        PulseAssert.True(viewport.Height > 0, $"Viewport height must be positive, got {viewport.Height}.");
    }

    [PulseCase(TimeoutMs = 2000)]
    public async Task JS_module_round_trips_value_through_browser_runtime(CancellationToken ct)
    {
        await using var module = await ImportModuleAsync(ct);
        var actual = await module.InvokeAsync<string>("roundTrip", ct, "pulse-js-boundary");

        PulseAssert.Equal("pulse-js-boundary", actual, "IJSRuntime must serialize arguments into the imported browser module and deserialize the return value.");
    }

    [PulseMatrix(DisplayName = "JS display width classifier", TimeoutMs = 2000)]
    [PulseRow(375, "compact")]
    [PulseRow(768, "medium")]
    [PulseRow(1440, "expanded")]
    [PulseRow(0, "unavailable")]
    public async Task JS_module_classifies_display_width(int width, string expected, CancellationToken ct)
    {
        await using var module = await ImportModuleAsync(ct);
        var actual = await module.InvokeAsync<string>("classifyDisplayWidth", ct, width);

        PulseAssert.Equal(expected, actual, $"The imported browser module must classify display width {width}.");
    }

    private ValueTask<IJSObjectReference> ImportModuleAsync(CancellationToken cancellationToken)
        => _js.InvokeAsync<IJSObjectReference>("import", cancellationToken, "./pulse-sample.js");

    private sealed class ViewportSnapshot
    {
        public int Width { get; set; }

        public int Height { get; set; }
    }
}