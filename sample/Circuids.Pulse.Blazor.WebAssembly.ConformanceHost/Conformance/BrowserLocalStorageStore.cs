using Circuids.Pulse.TestSupport.Storage;
using Microsoft.JSInterop;

namespace Circuids.Pulse.Blazor.WebAssembly.Sample.Conformance;

internal sealed class BrowserLocalStorageStore : IConformanceKeyValueStore
{
    private readonly IJSRuntime _js;

    public BrowserLocalStorageStore(IJSRuntime js)
    {
        _js = js;
    }

    public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _js.InvokeAsync<string?>("localStorage.getItem", cancellationToken, key);
    }

    public ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _js.InvokeVoidAsync("localStorage.setItem", cancellationToken, key, value);
    }

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _js.InvokeVoidAsync("localStorage.removeItem", cancellationToken, key);
    }
}