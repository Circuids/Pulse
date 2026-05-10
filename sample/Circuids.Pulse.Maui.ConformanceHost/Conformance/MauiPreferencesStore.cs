using Circuids.Pulse.TestSupport.Storage;

namespace Circuids.Pulse.Maui.Sample.Conformance;

internal sealed class MauiPreferencesStore : IConformanceKeyValueStore
{
    public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Preferences.Default.ContainsKey(key))
        {
            return ValueTask.FromResult<string?>(null);
        }

        return ValueTask.FromResult<string?>(Preferences.Default.Get(key, string.Empty));
    }

    public ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Preferences.Default.Set(key, value);
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Preferences.Default.Remove(key);
        return ValueTask.CompletedTask;
    }
}