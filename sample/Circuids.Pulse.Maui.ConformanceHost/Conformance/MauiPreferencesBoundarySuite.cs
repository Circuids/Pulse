using Circuids.Pulse.TestSupport.Storage;

namespace Circuids.Pulse.Maui.Sample.Conformance;

public sealed class MauiPreferencesBoundarySuite : KeyValueStoreSpec, IPulseLifetime
{
    protected override string KeyPrefix => "circuids.pulse.sample.maui.preferences";

    protected override IConformanceKeyValueStore CreateStore() => new MauiPreferencesStore();

    public Task InitializeAsync(CancellationToken cancellationToken) => ClearKnownKeysAsync(cancellationToken);

    public Task DisposeAsync(CancellationToken cancellationToken) => ClearKnownKeysAsync(cancellationToken);

    [PulseCase(TimeoutMs = 2000)]
    public Task Missing_key_returns_null(CancellationToken ct) => Missing_key_returns_null_core(ct);

    [PulseCase(TimeoutMs = 2000)]
    public Task Set_then_get_round_trips_value(CancellationToken ct) => Set_then_get_round_trips_value_core(ct);

    [PulseCase(TimeoutMs = 2000)]
    public Task Overwrite_replaces_existing_value(CancellationToken ct) => Overwrite_replaces_existing_value_core(ct);

    [PulseCase(TimeoutMs = 2000)]
    public Task Remove_clears_existing_value(CancellationToken ct) => Remove_clears_existing_value_core(ct);

    [PulseCase(TimeoutMs = 2000)]
    public Task Different_keys_are_isolated(CancellationToken ct) => Different_keys_are_isolated_core(ct);

    [PulseCase(TimeoutMs = 2000)]
    public Task Json_shaped_value_round_trips_unchanged(CancellationToken ct) => Json_shaped_value_round_trips_unchanged_core(ct);

    [PulseCase(TimeoutMs = 2000)]
    public Task Unicode_value_round_trips_unchanged(CancellationToken ct) => Unicode_value_round_trips_unchanged_core(ct);

    [PulseMatrix(DisplayName = "Preferences batch round-trip", TimeoutMs = 2000)]
    [PulseRow(1)]
    [PulseRow(5)]
    [PulseRow(20)]
    public async Task Preferences_batch_round_trips_under_budget(int count, CancellationToken ct)
    {
        var store = CreateStore();
        var keys = Enumerable.Range(0, count)
            .Select(index => $"{KeyPrefix}.batch.{index}")
            .ToArray();

        try
        {
            foreach (var key in keys)
            {
                await store.SetAsync(key, key, ct);
                PulseAssert.Equal(key, await store.GetAsync(key, ct), "Preferences must return the value just written.");
            }
        }
        finally
        {
            foreach (var key in keys)
            {
                await store.RemoveAsync(key, CancellationToken.None);
            }
        }
    }
}