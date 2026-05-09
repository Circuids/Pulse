namespace Circuids.Pulse.Blazor.WebAssembly.Sample.Conformance;

/// <summary>
/// Demonstrates that Pulse runs inside the host's <strong>real DI graph</strong>: this suite
/// receives the very same <see cref="HttpClient"/> the rest of the app uses, configured with
/// the WebAssembly host's <c>BaseAddress</c> in <c>Program.cs</c>. No mocks, no fakes.
/// </summary>
public sealed class HttpClientSuite
{
    private readonly HttpClient _http;

    public HttpClientSuite(HttpClient http)
    {
        _http = http;
    }

    [PulseCase]
    public void HttpClient_is_resolved_from_host_DI()
    {
        PulseAssert.NotNull(_http, "HttpClient must be resolvable from the WASM host's IServiceProvider.");
    }

    [PulseCase]
    public void HttpClient_has_a_base_address()
    {
        // Program.cs sets BaseAddress = new Uri(builder.HostEnvironment.BaseAddress).
        PulseAssert.NotNull(
            _http.BaseAddress,
            "Program.cs must configure HttpClient.BaseAddress to the host environment's BaseAddress.");
    }

    [PulseCase]
    public void HttpClient_BaseAddress_is_absolute_http_or_https()
    {
        var uri = _http.BaseAddress!;
        PulseAssert.True(uri.IsAbsoluteUri, $"BaseAddress must be absolute, got '{uri}'.");
        PulseAssert.True(
            uri.Scheme is "http" or "https",
            $"BaseAddress scheme must be http(s), got '{uri.Scheme}'.");
    }

    [PulseCase]
    public async Task HttpClient_can_GET_the_app_index()
    {
        // Real network call against the host that's serving this WASM bundle. The DevServer
        // serves index.html at the base address; in published deployments the same path
        // returns the static fallback.
        using var response = await _http.GetAsync("");
        PulseAssert.True(
            response.IsSuccessStatusCode,
            $"GET / against the host must succeed; got {(int)response.StatusCode} {response.StatusCode}.");
    }
}
