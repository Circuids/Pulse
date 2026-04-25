using System.Text.Json;

namespace Circuids.Pulse.UnitTests;

public sealed class ReportSerializationTests
{
    [Fact]
    public async Task TestRunReport_round_trips_through_PulseJsonContext()
    {
        var services = new ServiceCollection();
        services.AddPulse(p =>
        {
            p.AssignedPlatform = "RoundTrip";
            p.AddSuite<TinySuite>();
        });
        await using var sp = services.BuildServiceProvider();
        var report = await sp.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);

        var json = JsonSerializer.Serialize(report, PulseJsonContext.Default.TestRunReport);
        Assert.Contains("\"assignedPlatform\":\"RoundTrip\"", json);
        Assert.Contains("\"runtimeEnvironment\"", json);
        Assert.Contains("\"results\"", json);

        var deserialized = JsonSerializer.Deserialize(json, PulseJsonContext.Default.TestRunReport);
        Assert.NotNull(deserialized);
        Assert.Equal("RoundTrip", deserialized!.AssignedPlatform);
        Assert.Equal(report.Total, deserialized.Total);
    }

    [Fact]
    public async Task RuntimeEnvironment_is_populated_from_BCL()
    {
        var services = new ServiceCollection();
        services.AddPulse(p => p.AddSuite<TinySuite>());
        await using var sp = services.BuildServiceProvider();
        var report = await sp.GetRequiredService<ITestExecutor>().RunAsync(TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(report.RuntimeEnvironment.FrameworkDescription));
        Assert.False(string.IsNullOrWhiteSpace(report.RuntimeEnvironment.OSDescription));
        Assert.True(report.RuntimeEnvironment.ProcessorCount > 0);
    }

    public sealed class TinySuite
    {
        [PulseCase] public void Ok() { }
    }
}
