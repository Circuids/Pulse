namespace Circuids.Pulse.Blazor.WebAssembly.Sample.Conformance;

public sealed class HttpHostBoundarySuite
{
    private readonly HttpClient _http;

    public HttpHostBoundarySuite(HttpClient http)
    {
        _http = http;
    }

    [PulseCase]
    public void HttpClient_is_resolved_from_host_DI()
    {
        PulseAssert.NotNull(_http, "HttpClient must be resolved from the WebAssembly host service provider.");
    }

    [PulseCase]
    public void HttpClient_base_address_is_absolute_http_or_https()
    {
        var baseAddress = _http.BaseAddress;

        PulseAssert.NotNull(baseAddress, "The WebAssembly host must configure HttpClient.BaseAddress.");
        PulseAssert.True(baseAddress!.IsAbsoluteUri, $"BaseAddress must be absolute, got '{baseAddress}'.");
        PulseAssert.True(baseAddress.Scheme is "http" or "https", $"BaseAddress scheme must be http(s), got '{baseAddress.Scheme}'.");
    }

    [PulseCase(TimeoutMs = 5000)]
    public async Task App_root_returns_success(CancellationToken ct)
    {
        using var response = await _http.GetAsync("", ct);

        PulseAssert.True(response.IsSuccessStatusCode, $"GET / must succeed, got {(int)response.StatusCode} {response.StatusCode}.");
    }

    [PulseCase(TimeoutMs = 5000)]
    public async Task App_root_returns_html(CancellationToken ct)
    {
        var html = await _http.GetStringAsync("", ct);

        PulseAssert.Contains("<html", html, "The app root must return the host document.");
    }
}