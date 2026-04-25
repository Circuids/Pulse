namespace Circuids.Pulse.Maui.Sample.Conformance;

/// <summary>
/// Exercises <see cref="Preferences.Default"/> against the platform's real key-value store
/// (NSUserDefaults / SharedPreferences / WinRT ApplicationDataContainer). Demonstrates
/// <see cref="IPulseLifetime"/>: the suite snapshots the prior value once before its first test
/// and restores it after the last so the device's preferences are never permanently mutated.
/// </summary>
public sealed class PreferencesSuite : IPulseLifetime
{
    private const string Key = "circuids.pulse.sample.testkey";
    private string? _backup;
    private bool _hadValue;

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        _hadValue = Preferences.Default.ContainsKey(Key);
        _backup = _hadValue ? Preferences.Default.Get<string>(Key, string.Empty) : null;
        Preferences.Default.Remove(Key);
        return Task.CompletedTask;
    }

    public Task DisposeAsync(CancellationToken cancellationToken)
    {
        if (_hadValue && _backup is not null) Preferences.Default.Set(Key, _backup);
        else Preferences.Default.Remove(Key);
        return Task.CompletedTask;
    }

    [PulseCase]
    public void Set_then_Get_round_trips_a_value()
    {
        Preferences.Default.Set(Key, "hello");
        var read = Preferences.Default.Get(Key, "fallback");
        PulseAssert.Equal("hello", read, "Preferences must round-trip the value just written.");
    }

    [PulseCase]
    public void Get_returns_default_when_key_missing()
    {
        Preferences.Default.Remove(Key);
        var read = Preferences.Default.Get(Key, "fallback");
        PulseAssert.Equal("fallback", read, "Preferences must return the supplied default when key is absent.");
    }

    [PulseCase]
    public void Remove_clears_a_previously_set_key()
    {
        Preferences.Default.Set(Key, "to-be-removed");
        Preferences.Default.Remove(Key);
        PulseAssert.False(
            Preferences.Default.ContainsKey(Key),
            "ContainsKey must return false after Remove.");
    }
}
