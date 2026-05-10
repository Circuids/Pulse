namespace Circuids.Pulse.TestSupport.Storage;

public abstract class KeyValueStoreSpec
{
    protected virtual string KeyPrefix => $"circuids.pulse.sample.{GetType().Name}";

    protected abstract IConformanceKeyValueStore CreateStore();

    protected async Task ClearKnownKeysAsync(CancellationToken cancellationToken = default)
    {
        var store = CreateStore();

        foreach (var key in KnownKeys())
        {
            await store.RemoveAsync(key, cancellationToken);
        }
    }

    protected async Task Missing_key_returns_null_core(CancellationToken cancellationToken = default)
    {
        var store = CreateStore();
        var key = Key("missing");

        await store.RemoveAsync(key, cancellationToken);
        var actual = await store.GetAsync(key, cancellationToken);

        Equal<string?>(null, actual, "A missing key must read as null.");
    }

    protected async Task Set_then_get_round_trips_value_core(CancellationToken cancellationToken = default)
    {
        var store = CreateStore();
        var key = Key("roundtrip");

        try
        {
            await store.SetAsync(key, "alpha", cancellationToken);
            var actual = await store.GetAsync(key, cancellationToken);

            Equal("alpha", actual, "A stored value must round-trip unchanged.");
        }
        finally
        {
            await store.RemoveAsync(key, CancellationToken.None);
        }
    }

    protected async Task Overwrite_replaces_existing_value_core(CancellationToken cancellationToken = default)
    {
        var store = CreateStore();
        var key = Key("overwrite");

        try
        {
            await store.SetAsync(key, "before", cancellationToken);
            await store.SetAsync(key, "after", cancellationToken);

            var actual = await store.GetAsync(key, cancellationToken);
            Equal("after", actual, "The second write must replace the first value.");
        }
        finally
        {
            await store.RemoveAsync(key, CancellationToken.None);
        }
    }

    protected async Task Remove_clears_existing_value_core(CancellationToken cancellationToken = default)
    {
        var store = CreateStore();
        var key = Key("remove");

        await store.SetAsync(key, "remove-me", cancellationToken);
        await store.RemoveAsync(key, cancellationToken);

        var actual = await store.GetAsync(key, cancellationToken);
        Equal<string?>(null, actual, "A removed key must read as null.");
    }

    protected async Task Different_keys_are_isolated_core(CancellationToken cancellationToken = default)
    {
        var store = CreateStore();
        var firstKey = Key("isolation.first");
        var secondKey = Key("isolation.second");

        try
        {
            await store.SetAsync(firstKey, "first", cancellationToken);
            await store.SetAsync(secondKey, "second", cancellationToken);

            Equal("first", await store.GetAsync(firstKey, cancellationToken), "The first key must keep its value.");
            Equal("second", await store.GetAsync(secondKey, cancellationToken), "The second key must keep its value.");
        }
        finally
        {
            await store.RemoveAsync(firstKey, CancellationToken.None);
            await store.RemoveAsync(secondKey, CancellationToken.None);
        }
    }

    protected async Task Json_shaped_value_round_trips_unchanged_core(CancellationToken cancellationToken = default)
    {
        var store = CreateStore();
        var key = Key("json");
        const string expected = "{\"name\":\"Pulse\",\"enabled\":true,\"count\":3}";

        try
        {
            await store.SetAsync(key, expected, cancellationToken);
            var actual = await store.GetAsync(key, cancellationToken);

            Equal(expected, actual, "Storage must not mutate JSON-shaped string values.");
        }
        finally
        {
            await store.RemoveAsync(key, CancellationToken.None);
        }
    }

    protected async Task Unicode_value_round_trips_unchanged_core(CancellationToken cancellationToken = default)
    {
        var store = CreateStore();
        var key = Key("unicode");
        const string expected = "Pulse - cafe - 東京 - مرحبا";

        try
        {
            await store.SetAsync(key, expected, cancellationToken);
            var actual = await store.GetAsync(key, cancellationToken);

            Equal(expected, actual, "Storage must preserve Unicode text values.");
        }
        finally
        {
            await store.RemoveAsync(key, CancellationToken.None);
        }
    }

    private string Key(string scenario) => $"{KeyPrefix}.{scenario}";

    private IEnumerable<string> KnownKeys()
    {
        yield return Key("missing");
        yield return Key("roundtrip");
        yield return Key("overwrite");
        yield return Key("remove");
        yield return Key("isolation.first");
        yield return Key("isolation.second");
        yield return Key("json");
        yield return Key("unicode");
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected '{expected}', got '{actual}'.");
        }
    }
}